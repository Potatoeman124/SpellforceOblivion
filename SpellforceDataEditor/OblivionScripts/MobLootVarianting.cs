using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;

namespace SpellforceDataEditor.OblivionScripts
{
    /// <summary>
    /// Ensures each mob variant UnitID has its own c2040 loot block and that
    /// loot items are re-tiered according to mob tier -> item/spell tier mapping.
    /// </summary>
    public static class MobLootVarianting
    {
        // ---------------------------------------------
        // Variant tables (by base name + suffix)
        // ---------------------------------------------
        public sealed class NameVariantTable
        {
            // BaseName -> (Suffix -> ItemID)
            public Dictionary<string, Dictionary<string, ushort>> ByBaseName { get; } =
                new Dictionary<string, Dictionary<string, ushort>>(StringComparer.OrdinalIgnoreCase);

            // AnyItemID -> (BaseName, CurrentSuffix)
            public Dictionary<ushort, (string BaseName, string Suffix)> ByAnyId { get; } =
                new Dictionary<ushort, (string BaseName, string Suffix)>();
        }

        public sealed class UnitVariantTable
        {
            // BaseName -> (TierIndex -> UnitID). TierIndex: 0=Normal/OriginalCopy, 1..N = mobSuffixes order.
            public Dictionary<string, Dictionary<int, ushort>> ByBaseName { get; } =
                new Dictionary<string, Dictionary<int, ushort>>(StringComparer.OrdinalIgnoreCase);

            // UnitID -> (BaseName, TierIndex)
            public Dictionary<ushort, (string BaseName, int TierIndex)> ByUnitId { get; } =
                new Dictionary<ushort, (string BaseName, int TierIndex)>();
        }

        // ---------------------------------------------
        // Public entry
        // ---------------------------------------------
        public static SFGameDataNew ExpandMobLootTablesForUnitVariants_c2040(
            SFGameDataNew gd,
            IReadOnlyList<UnitVarianting.MobModifierStructure> mobTierTable,
            IReadOnlyList<ItemVarianting.ItemModifierStructure> itemTierTable,
            IReadOnlyList<SpellVarianting.SpellModifierStructure> spellTierTable,
            IProgress<ProgressInfo> progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (mobTierTable == null) throw new ArgumentNullException(nameof(mobTierTable));
            if (itemTierTable == null) throw new ArgumentNullException(nameof(itemTierTable));
            if (spellTierTable == null) throw new ArgumentNullException(nameof(spellTierTable));

            // Suffix lists LOW->HIGH, excluding normal
            var mobSuffixes = mobTierTable.Select(t => (t.Suffix ?? "").Trim())
                                          .Where(s => s.Length > 0)
                                          .ToList();
            var itemSuffixes = itemTierTable.Select(t => (t.Suffix ?? "").Trim())
                                            .Where(s => s.Length > 0)
                                            .ToList();
            var spellSuffixes = spellTierTable.Select(t => (t.Suffix ?? "").Trim())
                                              .Where(s => s.Length > 0)
                                              .ToList();

            // Nothing to do if no mob suffix tiers exist
            if (mobSuffixes.Count == 0) return gd;

            // Build scroll set from c2013 (inventory scroll link)
            var scrollItemIds = CollectInventoryScrollItemIDs_FromC2013(gd);

            // Build variant name tables once (no hardcoding)
            progress?.Report(new ProgressInfo { Phase = "MobLoot", Detail = "Building item/spell variant lookup tables", Current = 0, Total = 4 });

            var equippableVariants = BuildItemVariantTableByName(
                gd,
                suffixesLowToHighExcludingNormal: itemSuffixes,
                include: itemId => SharedHelperScripts.IsEquippableItem(gd, itemId),
                phase: "MobLoot",
                detail: "Indexing equippable item variants",
                progress: progress,
                cancellationToken: cancellationToken
            );

            var scrollVariants = BuildItemVariantTableByName(
                gd,
                suffixesLowToHighExcludingNormal: spellSuffixes,
                include: itemId => scrollItemIds.Contains(itemId),
                phase: "MobLoot",
                detail: "Indexing scroll variants",
                progress: progress,
                cancellationToken: cancellationToken
            );

            var unitVariants = BuildUnitVariantTableByName_FromC2024(
                gd,
                mobSuffixesLowToHighExcludingNormal: mobSuffixes,
                phase: "MobLoot",
                detail: "Indexing unit variants",
                progress: progress,
                cancellationToken: cancellationToken
            );

            // Index existing c2040 rows by UnitID (so we can clone or retier)
            var lootCat = gd.c2040;
            var lootRowIndicesByUnitId = new Dictionary<ushort, List<int>>();
            for (int i = 0; i < lootCat.Items.Count; i++)
            {
                ushort uid = lootCat.Items[i].UnitID;
                if (!lootRowIndicesByUnitId.TryGetValue(uid, out var list))
                {
                    list = new List<int>();
                    lootRowIndicesByUnitId[uid] = list;
                }
                list.Add(i);
            }

            // Tier counts include "normal" at index 0
            int mobTierCount = 1 + mobSuffixes.Count;
            int itemTierCount = 1 + itemSuffixes.Count;
            int spellTierCount = 1 + spellSuffixes.Count;

            // Iterate each baseName group of unit variants and ensure c2040 blocks exist for each tier
            int groupsDone = 0;
            int groupsTotal = unitVariants.ByBaseName.Count;

            foreach (var kv in unitVariants.ByBaseName)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string baseName = kv.Key;
                var tierToUnitId = kv.Value; // TierIndex -> UnitID

                // Find a template UnitID that already has c2040 rows (prefer highest tier)
                ushort templateUnitId = FindBestTemplateUnitId(
                    tierToUnitId,
                    lootRowIndicesByUnitId,
                    preferHighestTierIndex: mobTierCount - 1
                );

                if (templateUnitId == 0)
                    continue; // no loot defined for this mob family, skip

                var templateRowIndices = lootRowIndicesByUnitId[templateUnitId];
                var templateRows = templateRowIndices.Select(idx => lootCat.Items[idx]).ToList();

                // For each tier in this unit-variant group: ensure block exists and retier
                for (int mobTierIndex = 0; mobTierIndex < mobTierCount; mobTierIndex++)
                {
                    if (!tierToUnitId.TryGetValue(mobTierIndex, out ushort targetUnitId) || targetUnitId == 0)
                        continue;

                    // Map mob tier -> item/spell tier index
                    int mappedItemTierIndex = MapTierIndex(mobTierIndex, mobTierCount, itemTierCount);
                    int mappedSpellTierIndex = MapTierIndex(mobTierIndex, mobTierCount, spellTierCount);

                    if (!lootRowIndicesByUnitId.TryGetValue(targetUnitId, out var existingRowIndices))
                    {
                        // Create a new block by cloning template rows
                        int newBlockStart = lootCat.Items.Count;
                        lootCat.Indices.Add(newBlockStart);

                        var newIndices = new List<int>(templateRows.Count);

                        foreach (var tr in templateRows)
                        {
                            var nr = tr;
                            nr.UnitID = targetUnitId;

                            nr.ItemID1 = RetierLootItemID(nr.ItemID1, mappedItemTierIndex, mappedSpellTierIndex, itemSuffixes, spellSuffixes, equippableVariants, scrollVariants, scrollItemIds);
                            nr.ItemID2 = RetierLootItemID(nr.ItemID2, mappedItemTierIndex, mappedSpellTierIndex, itemSuffixes, spellSuffixes, equippableVariants, scrollVariants, scrollItemIds);
                            nr.ItemID3 = RetierLootItemID(nr.ItemID3, mappedItemTierIndex, mappedSpellTierIndex, itemSuffixes, spellSuffixes, equippableVariants, scrollVariants, scrollItemIds);

                            lootCat.Items.Add(nr);
                            newIndices.Add(lootCat.Items.Count - 1);
                        }

                        lootRowIndicesByUnitId[targetUnitId] = newIndices;
                    }
                    else
                    {
                        // Retier existing rows in-place
                        foreach (int idx in existingRowIndices)
                        {
                            var r = lootCat.Items[idx];

                            r.ItemID1 = RetierLootItemID(r.ItemID1, mappedItemTierIndex, mappedSpellTierIndex, itemSuffixes, spellSuffixes, equippableVariants, scrollVariants, scrollItemIds);
                            r.ItemID2 = RetierLootItemID(r.ItemID2, mappedItemTierIndex, mappedSpellTierIndex, itemSuffixes, spellSuffixes, equippableVariants, scrollVariants, scrollItemIds);
                            r.ItemID3 = RetierLootItemID(r.ItemID3, mappedItemTierIndex, mappedSpellTierIndex, itemSuffixes, spellSuffixes, equippableVariants, scrollVariants, scrollItemIds);

                            lootCat.Items[idx] = r;
                        }
                    }
                }

                groupsDone++;
                if (progress != null && (groupsDone % ProgressInfo.ProgressUpdateInterval == 0 || groupsDone == groupsTotal))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "MobLoot",
                        Detail = $"Processed {groupsDone}/{groupsTotal} mob families",
                        Current = groupsDone,
                        Total = groupsTotal
                    });
                }
            }

            return gd;
        }

        // ---------------------------------------------
        // Build tables
        // ---------------------------------------------
        private static NameVariantTable BuildItemVariantTableByName(
            SFGameDataNew gd,
            IReadOnlyList<string> suffixesLowToHighExcludingNormal,
            Func<ushort, bool> include,
            string phase,
            string detail,
            IProgress<ProgressInfo> progress,
            CancellationToken cancellationToken
        )
        {
            var table = new NameVariantTable();
            var suffixSet = new HashSet<string>(suffixesLowToHighExcludingNormal, StringComparer.OrdinalIgnoreCase);

            var itemCat = gd.c2003;
            int done = 0;
            int total = itemCat.Items.Count;

            for (int i = 0; i < itemCat.Items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var it = itemCat.Items[i];
                ushort itemId = it.ItemID;
                if (itemId == 0 || include != null && !include(itemId))
                    continue;

                string name = SharedHelperScripts.GetEnglishItemName(gd, it.NameID) ?? "";
                name = name.Trim();
                if (name.Length == 0)
                    continue;

                var (baseName, suffix) = SplitBaseNameAndSuffix(name, suffixSet);

                if (!table.ByBaseName.TryGetValue(baseName, out var bySuffix))
                {
                    bySuffix = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
                    table.ByBaseName[baseName] = bySuffix;
                }

                // If duplicates appear, keep the first one (stable)
                if (!bySuffix.ContainsKey(suffix))
                    bySuffix[suffix] = itemId;

                if (!table.ByAnyId.ContainsKey(itemId))
                    table.ByAnyId[itemId] = (baseName, suffix);

                done++;
                if (progress != null && (done % ProgressInfo.ProgressUpdateInterval == 0 || done == total))
                {
                    progress.Report(new ProgressInfo { Phase = phase, Detail = detail, Current = done, Total = total });
                }
            }

            return table;
        }

        private static UnitVariantTable BuildUnitVariantTableByName_FromC2024(
            SFGameDataNew gd,
            IReadOnlyList<string> mobSuffixesLowToHighExcludingNormal,
            string phase,
            string detail,
            IProgress<ProgressInfo> progress,
            CancellationToken cancellationToken
        )
        {
            var table = new UnitVariantTable();

            var suffixSet = new HashSet<string>(mobSuffixesLowToHighExcludingNormal, StringComparer.OrdinalIgnoreCase);
            var suffixToTierIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < mobSuffixesLowToHighExcludingNormal.Count; i++)
                suffixToTierIndex[mobSuffixesLowToHighExcludingNormal[i]] = i + 1; // 1..N

            var unitCat = gd.c2024;

            // Pass 1: only units WITH a recognized suffix (prevents base-name collisions)
            int total = unitCat.Items.Count;
            int done = 0;

            for (int i = 0; i < unitCat.Items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var u = unitCat.Items[i];

                string name = SharedHelperScripts.GetEnglishItemName(gd, u.NameID) ?? "";
                name = name.Trim();
                if (name.Length == 0)
                    continue;

                var (baseName, suffix) = SplitBaseNameAndSuffix(name, suffixSet);
                if (suffix.Length == 0)
                    continue; // only suffix entries in pass 1

                if (!suffixToTierIndex.TryGetValue(suffix, out int tierIndex))
                    continue;

                if (!table.ByBaseName.TryGetValue(baseName, out var tierToId))
                {
                    tierToId = new Dictionary<int, ushort>();
                    table.ByBaseName[baseName] = tierToId;
                }

                if (!tierToId.ContainsKey(tierIndex))
                    tierToId[tierIndex] = u.UnitID;

                if (!table.ByUnitId.ContainsKey(u.UnitID))
                    table.ByUnitId[u.UnitID] = (baseName, tierIndex);

                done++;
                if (progress != null && (done % ProgressInfo.ProgressUpdateInterval == 0 || done == total))
                    progress.Report(new ProgressInfo { Phase = phase, Detail = detail, Current = done, Total = total });
            }

            // Pass 2: attach "normal/original copy" units (no suffix) ONLY if baseName already known
            for (int i = 0; i < unitCat.Items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var u = unitCat.Items[i];

                string name = SharedHelperScripts.GetEnglishItemName(gd, u.NameID) ?? "";
                name = name.Trim();
                if (name.Length == 0)
                    continue;

                var (baseName, suffix) = SplitBaseNameAndSuffix(name, suffixSet);
                if (suffix.Length != 0)
                    continue;

                if (!table.ByBaseName.TryGetValue(baseName, out var tierToId))
                    continue;

                if (!tierToId.ContainsKey(0))
                    tierToId[0] = u.UnitID;

                if (!table.ByUnitId.ContainsKey(u.UnitID))
                    table.ByUnitId[u.UnitID] = (baseName, 0);
            }

            return table;
        }

        // ---------------------------------------------
        // Retiering helpers
        // ---------------------------------------------
        private static ushort RetierLootItemID(
            ushort itemId,
            int mappedItemTierIndex,
            int mappedSpellTierIndex,
            IReadOnlyList<string> itemSuffixesLowToHighExcludingNormal,
            IReadOnlyList<string> spellSuffixesLowToHighExcludingNormal,
            NameVariantTable equippableVariants,
            NameVariantTable scrollVariants,
            HashSet<ushort> inventoryScrollItemIds
        )
        {
            if (itemId == 0) return 0;

            // Scrolls use spell tiers
            if (inventoryScrollItemIds.Contains(itemId))
            {
                return TryResolveVariantId(scrollVariants, itemId, mappedSpellTierIndex, spellSuffixesLowToHighExcludingNormal);
            }

            // Equippables use item tiers
            if (equippableVariants.ByAnyId.ContainsKey(itemId))
            {
                return TryResolveVariantId(equippableVariants, itemId, mappedItemTierIndex, itemSuffixesLowToHighExcludingNormal);
            }

            // Unknown/other items: do not change
            return itemId;
        }

        private static ushort TryResolveVariantId(
            NameVariantTable table,
            ushort anyId,
            int targetTierIndexIncludingNormal,
            IReadOnlyList<string> suffixesLowToHighExcludingNormal
        )
        {
            if (!table.ByAnyId.TryGetValue(anyId, out var info))
                return anyId;

            if (!table.ByBaseName.TryGetValue(info.BaseName, out var bySuffix))
                return anyId;

            // Desired suffix for target tier
            string desiredSuffix = (targetTierIndexIncludingNormal <= 0)
                ? ""
                : suffixesLowToHighExcludingNormal[targetTierIndexIncludingNormal - 1];

            // Exact
            if (bySuffix.TryGetValue(desiredSuffix, out ushort resolved))
                return resolved;

            // Fallback downwards (best <= desired)
            for (int i = targetTierIndexIncludingNormal; i >= 0; i--)
            {
                string s = (i == 0) ? "" : suffixesLowToHighExcludingNormal[i - 1];
                if (bySuffix.TryGetValue(s, out resolved))
                    return resolved;
            }

            return anyId;
        }

        /// <summary>
        /// Mob->Target mapping:
        /// - If mob tiers <= target tiers: left-align (same index).
        /// - If mob tiers  > target tiers: right-align (drop lowest tiers first).
        /// Indices are INCLUDING normal tier at index 0.
        /// </summary>
        private static int MapTierIndex(int mobTierIndexIncludingNormal, int mobTierCountIncludingNormal, int targetTierCountIncludingNormal)
        {
            if (targetTierCountIncludingNormal <= 1)
                return 0;

            if (mobTierCountIncludingNormal <= targetTierCountIncludingNormal)
                return Math.Min(mobTierIndexIncludingNormal, targetTierCountIncludingNormal - 1);

            int offset = mobTierCountIncludingNormal - targetTierCountIncludingNormal;
            int ix = mobTierIndexIncludingNormal - offset;
            if (ix < 0) ix = 0;
            if (ix > targetTierCountIncludingNormal - 1) ix = targetTierCountIncludingNormal - 1;
            return ix;
        }

        private static ushort FindBestTemplateUnitId(
            Dictionary<int, ushort> tierToUnitId,
            Dictionary<ushort, List<int>> lootRowIndicesByUnitId,
            int preferHighestTierIndex
        )
        {
            // Prefer highest available tier that has loot
            for (int t = preferHighestTierIndex; t >= 0; t--)
            {
                if (!tierToUnitId.TryGetValue(t, out ushort uid) || uid == 0)
                    continue;

                if (lootRowIndicesByUnitId.ContainsKey(uid))
                    return uid;
            }

            // Otherwise: any tier that has loot
            foreach (var kv in tierToUnitId)
            {
                ushort uid = kv.Value;
                if (uid != 0 && lootRowIndicesByUnitId.ContainsKey(uid))
                    return uid;
            }

            return 0;
        }

        private static HashSet<ushort> CollectInventoryScrollItemIDs_FromC2013(SFGameDataNew gd)
        {
            var set = new HashSet<ushort>();
            foreach (var link in gd.c2013.Items)
            {
                if (link.ItemID != 0)
                    set.Add(link.ItemID);
            }
            return set;
        }

        // Splits "Name [Suffix]" if suffix is in suffixSet; otherwise returns (fullName, "").
        private static (string BaseName, string Suffix) SplitBaseNameAndSuffix(string fullName, HashSet<string> suffixSet)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return ("", "");

            fullName = fullName.Trim();

            // Expect the exact format created by your varianting: " ... [Suffix]"
            int open = fullName.LastIndexOf(" [", StringComparison.Ordinal);
            if (open < 0 || !fullName.EndsWith("]", StringComparison.Ordinal))
                return (fullName, "");

            string suffix = fullName.Substring(open + 2, fullName.Length - (open + 2) - 1).Trim(); // between '[' and ']'
            if (suffix.Length == 0 || !suffixSet.Contains(suffix))
                return (fullName, "");

            string baseName = fullName.Substring(0, open).Trim();
            return (baseName, suffix);
        }
    }
}
