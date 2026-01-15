using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SpellforceDataEditor.OblivionScripts
{
    /// <summary>
    /// Merchant inventory (c2042) updater that expands each sold item to include all tier variants + original copy,
    /// based on a precomputed name-based variant table from c2003/c2016.
    /// </summary>
    public static class MerchantInventoryVarianting
    {
        // ------------------------------------------------------------
        // Data model: "Item1Name, Original1ID, Var1ID..VarNID"
        // ------------------------------------------------------------

        public sealed class ItemVariantRow
        {
            public string BaseName = "";
            public ushort OriginalCopyItemID; // unsuffixed copy, if found
            public Dictionary<string, ushort> VariantBySuffix = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

            public IEnumerable<ushort> EnumerateAllIDsInOrder(IReadOnlyList<string> suffixOrder)
            {
                if (OriginalCopyItemID != 0)
                    yield return OriginalCopyItemID;

                if (suffixOrder != null)
                {
                    for (int i = 0; i < suffixOrder.Count; i++)
                    {
                        var s = suffixOrder[i];
                        if (VariantBySuffix.TryGetValue(s, out ushort id) && id != 0)
                            yield return id;
                    }
                }
                else
                {
                    foreach (var kv in VariantBySuffix)
                        if (kv.Value != 0) yield return kv.Value;
                }
            }
        }

        public sealed class ItemVariantTable
        {
            // BaseName -> Row
            public Dictionary<string, ItemVariantRow> ByBaseName = new Dictionary<string, ItemVariantRow>(StringComparer.OrdinalIgnoreCase);

            // Any ItemID (original copy OR variant) -> Row
            public Dictionary<ushort, ItemVariantRow> ByAnyItemID = new Dictionary<ushort, ItemVariantRow>();

            // The suffix order used to display/emit IDs in a stable way
            public List<string> SuffixOrder = new List<string>();
        }

        // ------------------------------------------------------------
        // 1) Build the table once
        // ------------------------------------------------------------

        /// <summary>
        /// Builds a table mapping base item name to: original copy ID (unsuffixed) + all variant IDs (suffixed).
        /// This scans c2003 once and uses c2016 for names.
        ///
        /// Important:
        /// - Pass ALL suffixes you use in your mod (item tiers + spell tiers), otherwise rows won't be detected.
        /// </summary>
        public static ItemVariantTable BuildItemVariantTableByName(
            SFGameDataNew gd,
            IEnumerable<string> allTierSuffixes,
            int languageId,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            // Normalize suffix list (preserve order, de-dupe)
            var suffixOrder = new List<string>();
            var suffixSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in allTierSuffixes ?? Array.Empty<string>())
            {
                var ss = (s ?? "").Trim();
                if (ss.Length == 0) continue;
                if (suffixSet.Add(ss))
                    suffixOrder.Add(ss);
            }

            var table = new ItemVariantTable();
            table.SuffixOrder = suffixOrder;

            // Build a fast TextID -> string map for the chosen language.
            // (Avoid calling GetEnglishItemName in a loop.)
            var textMap = BuildLocalisationMap(gd, languageId);

            // Map of unsuffixed exact-name -> smallest ItemID (used to locate original copies)
            var unsuffixedNameToMinItemID = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

            var items = gd.c2003.Items;
            int total = items.Count;
            int interval = Math.Max(1, ProgressInfo.ProgressUpdateInterval);

            progress?.Report(new ProgressInfo
            {
                Phase = "Build variants table (items)",
                Detail = $"Scanning c2003 ({total} items)...",
                Current = 0,
                Total = total
            });

            // Pass 1: collect all suffixed variants into rows; also collect unsuffixed name->id for later.
            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (progress != null && (i % interval == 0 || i == total - 1))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Build variants table (items)",
                        Detail = $"Item {i + 1}/{total}",
                        Current = i,
                        Total = total
                    });
                }

                var it = items[i];
                if (it.NameID == 0) continue;

                string name = ResolveText(textMap, it.NameID);
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (TryStripKnownSuffix(name, suffixOrder, out string baseName, out string suffix))
                {
                    if (!table.ByBaseName.TryGetValue(baseName, out var row))
                    {
                        row = new ItemVariantRow { BaseName = baseName };
                        table.ByBaseName[baseName] = row;
                    }

                    // Keep the smallest ID if duplicates exist (defensive)
                    if (!row.VariantBySuffix.TryGetValue(suffix, out ushort existing) || it.ItemID < existing)
                        row.VariantBySuffix[suffix] = it.ItemID;
                }
                else
                {
                    // Track unsuffixed by exact name
                    if (!unsuffixedNameToMinItemID.TryGetValue(name, out ushort cur) || it.ItemID < cur)
                        unsuffixedNameToMinItemID[name] = it.ItemID;
                }
            }

            // Pass 2: for each row, find its unsuffixed "original copy" by exact base name
            foreach (var kv in table.ByBaseName)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = kv.Value;
                if (unsuffixedNameToMinItemID.TryGetValue(row.BaseName, out ushort origID))
                    row.OriginalCopyItemID = origID;

                // Build reverse index: any item ID -> row
                if (row.OriginalCopyItemID != 0)
                    table.ByAnyItemID[row.OriginalCopyItemID] = row;

                foreach (var v in row.VariantBySuffix.Values)
                    if (v != 0) table.ByAnyItemID[v] = row;
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Build variants table (items)",
                Detail = $"Done. Rows: {table.ByBaseName.Count}, indexed IDs: {table.ByAnyItemID.Count}",
                Current = total,
                Total = total
            });

            return table;
        }

        // ------------------------------------------------------------
        // 2) Expand merchants (c2042)
        // ------------------------------------------------------------

        /// <summary>
        /// For each merchant inventory row in c2042, if the ItemID belongs to a variant row,
        /// add all missing IDs from that row (original copy + all variants).
        /// Re-sorts c2042 and rebuilds Indices at the end.
        /// </summary>
        public static SFGameDataNew ExpandMerchantsWithAllVariants(
            SFGameDataNew gd,
            ItemVariantTable table,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (table == null) throw new ArgumentNullException(nameof(table));

            var inv = gd.c2042;
            var items = inv.Items;

            // Build (MerchantID -> existing ItemIDs) once
            var existingByMerchant = new Dictionary<ushort, HashSet<ushort>>();
            for (int i = 0; i < items.Count; i++)
            {
                var r = items[i];
                if (!existingByMerchant.TryGetValue(r.MerchantID, out var set))
                {
                    set = new HashSet<ushort>();
                    existingByMerchant[r.MerchantID] = set;
                }
                set.Add(r.ItemID);
            }

            // To avoid processing same (merchant, group) many times
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect new rows
            var toAdd = new List<Category2042Item>();

            int total = items.Count;
            int interval = Math.Max(1, ProgressInfo.ProgressUpdateInterval);

            progress?.Report(new ProgressInfo
            {
                Phase = "Expand merchants (c2042)",
                Detail = $"Scanning {total} merchant entries...",
                Current = 0,
                Total = total
            });

            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (progress != null && (i % interval == 0 || i == total - 1))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Expand merchants (c2042)",
                        Detail = $"Entry {i + 1}/{total}",
                        Current = i,
                        Total = total
                    });
                }

                var row = items[i];

                if (!table.ByAnyItemID.TryGetValue(row.ItemID, out var group))
                    continue;

                // key = MerchantID + BaseName
                string key = row.MerchantID.ToString() + "|" + group.BaseName;
                if (!processed.Add(key))
                    continue;

                if (!existingByMerchant.TryGetValue(row.MerchantID, out var existingSet))
                {
                    existingSet = new HashSet<ushort>();
                    existingByMerchant[row.MerchantID] = existingSet;
                }

                // Add missing IDs for this group
                foreach (ushort id in group.EnumerateAllIDsInOrder(table.SuffixOrder))
                {
                    if (id == 0) continue;
                    if (existingSet.Contains(id)) continue;

                    toAdd.Add(new Category2042Item
                    {
                        MerchantID = row.MerchantID,
                        ItemID = id,
                        Stock = row.Stock // Stock doesn't matter; copy for consistency
                    });

                    existingSet.Add(id);
                }
            }

            if (toAdd.Count > 0)
            {
                items.AddRange(toAdd);

                // Keep CategoryBaseMultiple invariants: sort by MerchantID and rebuild Indices.
                items.Sort((a, b) =>
                {
                    int c = a.MerchantID.CompareTo(b.MerchantID);
                    if (c != 0) return c;
                    return a.ItemID.CompareTo(b.ItemID);
                });

                RebuildIndices(inv);
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Expand merchants (c2042)",
                Detail = $"Done. Added {toAdd.Count} entries.",
                Current = total,
                Total = total
            });

            return gd;
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        private static Dictionary<ushort, string> BuildLocalisationMap(SFGameDataNew gd, int languageId)
        {
            var map = new Dictionary<ushort, string>();
            var loc = gd.c2016.Items;

            for (int i = 0; i < loc.Count; i++)
            {
                var r = loc[i];
                if (r.LanguageID != languageId) continue;

                var copy = r;
                map[r.TextID] = SharedHelperScripts.ReadContent512(ref copy) ?? "";
            }

            return map;
        }

        private static string ResolveText(Dictionary<ushort, string> map, ushort textID)
        {
            if (textID == 0) return "";
            if (map.TryGetValue(textID, out var s)) return (s ?? "").Trim();
            return "";
        }

        private static bool TryStripKnownSuffix(
            string fullName,
            IReadOnlyList<string> suffixOrder,
            out string baseName,
            out string suffix
        )
        {
            baseName = fullName.Trim();
            suffix = "";

            for (int i = 0; i < suffixOrder.Count; i++)
            {
                string s = suffixOrder[i];
                string token = " [" + s + "]";

                if (baseName.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName.Substring(0, baseName.Length - token.Length).TrimEnd();
                    suffix = s;
                    return true;
                }
            }

            return false;
        }

        private static void RebuildIndices<T>(CategoryBaseMultiple<T> cat)
            where T : struct, ICategorySubItem
        {
            cat.Indices.Clear();

            int cur = int.MinValue;
            for (int i = 0; i < cat.Items.Count; i++)
            {
                int id = cat.Items[i].GetID();
                if (id != cur)
                {
                    cur = id;
                    cat.Indices.Add(i);
                }
            }
        }
    }
}
