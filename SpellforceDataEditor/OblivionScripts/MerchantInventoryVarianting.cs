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

        // ------------------------------------------------------------
        // MAIN ENTRY (call this from your existing merchant patch flow)
        // ------------------------------------------------------------
        public static SFGameDataNew AddItemVariantsToMerchants_InsertNearOriginal(
            SFGameDataNew gd,
            Func<ushort, IReadOnlyList<ushort>?> tryGetVariantChainByAnyItemID,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (tryGetVariantChainByAnyItemID == null) throw new ArgumentNullException(nameof(tryGetVariantChainByAnyItemID));

            var cat = gd.c2042;
            if (cat == null) throw new Exception("gd.c2042 is null.");

            int total = cat.Items.Count;
            int changedMerchants = 0;

            // We must avoid re-processing the same chain multiple times per merchant.
            // Key: MerchantID + ChainSignature (joined IDs).
            var processed = new HashSet<string>(StringComparer.Ordinal);

            // We mutate the list; use index loop.
            for (int i = 0; i < cat.Items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (progress != null && (i % ProgressInfo.ProgressUpdateInterval == 0))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Merchant inventory: insert variants",
                        Detail = $"Scanning c2042 row {i}/{cat.Items.Count} (changed merchants: {changedMerchants})",
                        Current = i,
                        Total = cat.Items.Count
                    });
                }

                var row = cat.Items[i];
                ushort merchantID = row.MerchantID;
                ushort anchorItemID = row.ItemID;

                var chain = tryGetVariantChainByAnyItemID(anchorItemID);
                if (chain == null || chain.Count <= 1)
                    continue;

                // Filter invalid ids, keep order (weak -> strong as provided)
                var ordered = chain.Where(id => id != 0).Distinct().ToList();
                if (ordered.Count <= 1)
                    continue;

                // Build processing key: merchant + chain IDs
                string sig = merchantID.ToString() + ":" + string.Join(",", ordered);
                if (!processed.Add(sig))
                    continue;

                // Normalize the merchant’s rows for this chain around current index
                bool changed = NormalizeMerchantChainBlock(cat, merchantID, i, ordered);

                if (changed) changedMerchants++;

                // After normalization, keep i in a safe region:
                // Move i to end of inserted block minus 1, so loop continues after it.
                // Find first occurrence of any of these ids for this merchant.
                int first = FindFirstIndex(cat.Items, merchantID, ordered);
                if (first >= 0)
                {
                    int last = FindLastIndex(cat.Items, merchantID, ordered);
                    if (last >= 0) i = last; // continue after block
                }
            }

            // Safety: keep category grouped & indices consistent
            StableSortC2042ByMerchantID(cat);
            RebuildC2042Indices_FromMerchantGroups(cat);

            progress?.Report(new ProgressInfo
            {
                Phase = "Merchant inventory: insert variants",
                Detail = $"Done. Changed merchants: {changedMerchants}",
                Current = 1,
                Total = 1
            });

            return gd;
        }

        // ------------------------------------------------------------
        // Core: remove existing scattered rows for this chain (merchant),
        //       then insert full chain at anchor position.
        // ------------------------------------------------------------
        private static bool NormalizeMerchantChainBlock(
            Category2042 cat,
            ushort merchantID,
            int anchorIndex,
            List<ushort> orderedItemIDs
        )
        {
            var items = cat.Items;
            var idSet = new HashSet<ushort>(orderedItemIDs);

            // Determine insertion position = earliest occurrence of ANY chain item for this merchant,
            // but at least the anchorIndex if that's earlier in the current scan.
            int insertAt = int.MaxValue;
            for (int k = 0; k < items.Count; k++)
            {
                if (items[k].MerchantID == merchantID && idSet.Contains(items[k].ItemID))
                {
                    insertAt = Math.Min(insertAt, k);
                }
            }
            if (insertAt == int.MaxValue)
                insertAt = anchorIndex;

            // Collect existing rows for this merchant+chain (preserve per-item Stock if already present)
            var existingByItemID = new Dictionary<ushort, Category2042Item>();
            int removed = 0;

            for (int k = items.Count - 1; k >= 0; k--)
            {
                var r = items[k];
                if (r.MerchantID != merchantID) continue;
                if (!idSet.Contains(r.ItemID)) continue;

                existingByItemID[r.ItemID] = r; // last wins; fine
                items.RemoveAt(k);
                removed++;

                if (k < insertAt) insertAt--; // list shifted left before insertion point
            }

            // Template row: use any removed row (prefer the one that was at anchor if possible)
            Category2042Item template;
            if (!existingByItemID.TryGetValue(orderedItemIDs[orderedItemIDs.Count - 1], out template)) // strongest
            {
                // fall back to any
                template = existingByItemID.Count > 0 ? existingByItemID.Values.First() : default;
                template.MerchantID = merchantID;
                template.Stock = 1;
            }

            // Now insert full chain in order, reusing existing rows if present
            int inserted = 0;
            foreach (ushort id in orderedItemIDs)
            {
                Category2042Item nr;
                if (existingByItemID.TryGetValue(id, out var ex))
                {
                    nr = ex;
                }
                else
                {
                    nr = template; // copy merchant + stock etc.
                    nr.ItemID = id;
                }

                nr.MerchantID = merchantID;
                nr.ItemID = id;

                items.Insert(insertAt + inserted, nr);
                inserted++;
            }

            // Changed if we added missing variants or we re-ordered them (scattered -> contiguous).
            // A simple proxy: if removed != inserted, we added/removed; else still might have re-ordered.
            // We’ll conservatively return true if there was any removal (means it was present and now normalized)
            // or if any id was missing (removed < inserted).
            return removed > 0 || removed != inserted;
        }

        private static int FindFirstIndex(List<Category2042Item> items, ushort merchantID, List<ushort> ids)
        {
            var set = new HashSet<ushort>(ids);
            for (int i = 0; i < items.Count; i++)
                if (items[i].MerchantID == merchantID && set.Contains(items[i].ItemID))
                    return i;
            return -1;
        }

        private static int FindLastIndex(List<Category2042Item> items, ushort merchantID, List<ushort> ids)
        {
            var set = new HashSet<ushort>(ids);
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].MerchantID == merchantID && set.Contains(items[i].ItemID))
                    return i;
            return -1;
        }

        // ------------------------------------------------------------
        // Keep merchant groups stable (do NOT randomize within merchant).
        // We stable-sort by MerchantID, preserving current per-merchant order.
        // ------------------------------------------------------------
        private static void StableSortC2042ByMerchantID(Category2042 cat)
        {
            var old = cat.Items;
            var rebuilt = old
                .Select((x, idx) => new { x, idx })
                .OrderBy(t => t.x.MerchantID)
                .ThenBy(t => t.idx)
                .Select(t => t.x)
                .ToList();

            cat.Items.Clear();
            cat.Items.AddRange(rebuilt);
        }

        // ------------------------------------------------------------
        // Rebuild Indices to match current MerchantID grouping.
        // Indices = start index of each new MerchantID block in Items.
        // ------------------------------------------------------------
        private static void RebuildC2042Indices_FromMerchantGroups(Category2042 cat)
        {
            cat.Indices.Clear();

            ushort last = 0;
            bool hasLast = false;

            for (int i = 0; i < cat.Items.Count; i++)
            {
                ushort mid = cat.Items[i].MerchantID;
                if (!hasLast || mid != last)
                {
                    cat.Indices.Add(i);
                    last = mid;
                    hasLast = true;
                }
            }
        }
    }
}
