using SFEngine.SFCFF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SpellforceDataEditor.OblivionScripts
{
    public static class VariantPipeline
    {
        // ------------------------------------------------------------
        // Units
        // ------------------------------------------------------------
        public static SFGameDataNew BuildUnitVariantsAndRegister(
            SFGameDataNew gd,
            IReadOnlyList<UnitVarianting.MobModifierStructure> mobTierTable, // [Veteran, Elite, Champion, Oblivion]
            HashSet<ushort> unitBlacklist,
            VariantRegistry registry,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (mobTierTable == null) throw new ArgumentNullException(nameof(mobTierTable));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            unitBlacklist ??= new HashSet<ushort>();

            int interval = Math.Max(1, ProgressInfo.ProgressUpdateInterval);

            // Snapshot base unit IDs BEFORE modifications
            var baseUnitIDs = gd.c2024.Items.Select(u => u.UnitID).ToList();

            int total = baseUnitIDs.Count;
            int done = 0;

            foreach (var baseUnitID in baseUnitIDs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                done++;

                if (progress != null && (done % interval == 0 || done == total))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Units: promoting & registering",
                        Current = done,
                        Total = total,
                        Detail = $"BaseUnitID {baseUnitID}"
                    });
                }

                if (unitBlacklist.Contains(baseUnitID))
                    continue;

                try
                {
                    gd = UnitPromotion.PromoteUnitToHighestAndCreateBackCopies(
                        gd,
                        baseUnitID,
                        mobTierTable,
                        unitBlacklist,
                        out ushort promoted,
                        out ushort vet,
                        out ushort elite,
                        out ushort champ,
                        out ushort originalCopy
                    );

                    registry.Units[baseUnitID] = new VariantRegistry.UnitEntry
                    {
                        BaseUnitID = baseUnitID,
                        PromotedUnitID = promoted,
                        VeteranUnitID = vet,
                        EliteUnitID = elite,
                        ChampionUnitID = champ,
                        OriginalCopyUnitID = originalCopy
                    };
                }
                catch
                {
                    // skip failures in batch mode
                }
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Units: done",
                Current = total,
                Total = total,
                Detail = $"Processed {total} units"
            });

            return gd;
        }

        // ------------------------------------------------------------
        // Items (equippable only)
        // ------------------------------------------------------------
        public static SFEngine.SFCFF.SFGameDataNew BuildItemVariantsAndRegister(
            SFEngine.SFCFF.SFGameDataNew gd,
            IReadOnlyList<ItemVarianting.ItemModifierStructure> itemTierTable,
            HashSet<ushort> itemBlackList,
            VariantRegistry registry,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (itemTierTable == null) throw new ArgumentNullException(nameof(itemTierTable));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            itemBlackList ??= new HashSet<ushort>();

            // Promote + return map from ItemVarianting (batch function)
            // We report progress from inside the loop here by calling the single-item function.
            var baseItemIDs = new List<ushort>();
            foreach (var it in gd.c2003.Items)
                baseItemIDs.Add(it.ItemID);

            int total = baseItemIDs.Count;
            int done = 0;

            for (int i = 0; i < baseItemIDs.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ushort baseItemID = baseItemIDs[i];
                done++;

                if (progress != null && (done % ProgressInfo.ProgressUpdateInterval == 0 || done == total))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Items: promoting & registering",
                        Current = done,
                        Total = total,
                        Detail = $"BaseItemID {baseItemID}"
                    });
                }

                if (itemBlackList.Contains(baseItemID))
                    continue;

                if (!SharedHelperScripts.IsEquippableItem(gd, baseItemID))
                    continue;

                try
                {
                    gd = ItemVarianting.PromoteItemToHighestTierAndCreateBackCopies(
                        gd, baseItemID, itemTierTable, out var res);

                    registry.Items[baseItemID] = new VariantRegistry.ItemEntry
                    {
                        BaseItemID = baseItemID,
                        PromotedItemID = res.PromotedItemID,
                        OriginalCopyItemID = res.OriginalCopyItemID,
                        RareItemID = res.GetTier("Rare"),
                        MasterworkItemID = res.GetTier("Masterwork"),
                        PerfectItemID = res.GetTier("Perfect"),
                    };
                }
                catch
                {
                    // skip failures; optionally log
                }
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Items: done",
                Current = total,
                Total = total,
                Detail = $"Processed {total} items"
            });

            return gd;
        }

        // ------------------------------------------------------------
        // Spells (scrollable only)
        // ------------------------------------------------------------
        public static SFGameDataNew BuildSpellVariantsAndRegister(
            SFGameDataNew gd,
            IReadOnlyList<SpellVarianting.SpellModifierStructure> spellTierTable, // [Empowered, Superior, Perfected, Arch]
            HashSet<ushort> spellLineBlackList,
            VariantRegistry registry,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (spellTierTable == null) throw new ArgumentNullException(nameof(spellTierTable));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            spellLineBlackList ??= new HashSet<ushort>();

            int interval = Math.Max(1, ProgressInfo.ProgressUpdateInterval);

            // Snapshot base SpellIDs that have scrolls (stable before modifications)
            var baseSpellIDs = SpellPromotion.GetAllSpellsWithScrolls(gd);

            // Fast lookup SpellID -> SpellLineID (avoid scanning c2002 for each spell)
            var spellLineBySpellID = new Dictionary<ushort, ushort>(gd.c2002.Items.Count);
            foreach (var s in gd.c2002.Items)
                spellLineBySpellID[s.SpellID] = s.SpellLineID;

            int total = baseSpellIDs.Count;
            int done = 0;

            foreach (var baseSpellID in baseSpellIDs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                done++;

                if (progress != null && (done % interval == 0 || done == total))
                {
                    SharedHelperScripts.RunCriticalChecks(gd, $"spells loop at {baseSpellID}");
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Spells: promoting & registering",
                        Current = done,
                        Total = total,
                        Detail = $"BaseSpellID {baseSpellID}"
                    });
                }

                if (!spellLineBySpellID.TryGetValue(baseSpellID, out ushort lineId))
                    continue;

                if (spellLineBlackList.Contains(lineId))
                    continue;

                try
                {
                    gd = SpellPromotion.PromoteSpellWithScrollToHighestAndCreateBackCopies(
                        gd,
                        baseSpellID,
                        spellTierTable,
                        out var res
                    );

                    registry.Spells[baseSpellID] = new VariantRegistry.SpellEntry
                    {
                        BaseSpellID = baseSpellID,
                        PromotedSpellID = res.PromotedSpellID,

                        EmpoweredSpellID = res.EmpoweredSpellID,
                        SuperiorSpellID = res.SuperiorSpellID,
                        PerfectedSpellID = res.PerfectedSpellID,
                        OriginalCopySpellID = res.OriginalCopySpellID,

                        BaseScrollItemID = res.BaseScrollItemID,
                        BaseSpellbookItemID = res.BaseSpellbookItemID,

                        EmpoweredScrollItemID = res.EmpoweredScrollItemID,
                        EmpoweredSpellbookItemID = res.EmpoweredSpellbookItemID,

                        SuperiorScrollItemID = res.SuperiorScrollItemID,
                        SuperiorSpellbookItemID = res.SuperiorSpellbookItemID,

                        PerfectedScrollItemID = res.PerfectedScrollItemID,
                        PerfectedSpellbookItemID = res.PerfectedSpellbookItemID,

                        OriginalCopyScrollItemID = res.OriginalCopyScrollItemID,
                        OriginalCopySpellbookItemID = res.OriginalCopySpellbookItemID
                    };
                }
                catch
                {
                    // skip failures
                }
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Spells: done",
                Current = total,
                Total = total,
                Detail = $"Processed {total} spells"
            });

            return gd;
        }
    }
}
