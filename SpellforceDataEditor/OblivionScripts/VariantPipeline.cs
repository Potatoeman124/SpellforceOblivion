using SFEngine.SFCFF;
using System;
using System.Collections.Generic;
using System.Linq;

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
            VariantRegistry registry
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (mobTierTable == null) throw new ArgumentNullException(nameof(mobTierTable));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            unitBlacklist ??= new HashSet<ushort>();

            // Snapshot base unit IDs BEFORE modifications
            var baseUnitIDs = gd.c2024.Items.Select(u => u.UnitID).ToList();

            foreach (var baseUnitID in baseUnitIDs)
            {
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
                    // skip failures in batch mode; add logging if you want
                }
            }

            return gd;
        }

        // ------------------------------------------------------------
        // Items (equippable only)
        // ------------------------------------------------------------
        public static SFGameDataNew BuildItemVariantsAndRegister(
            SFGameDataNew gd,
            HashSet<ushort> itemBlacklist,
            ItemVarianting.ItemModifierStructure rare,
            ItemVarianting.ItemModifierStructure masterwork,
            ItemVarianting.ItemModifierStructure perfect,
            ItemVarianting.ItemModifierStructure legendary,
            VariantRegistry registry
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            itemBlacklist ??= new HashSet<ushort>();

            var baseItemIDs = gd.c2003.Items.Select(i => i.ItemID).ToList();

            foreach (var baseItemID in baseItemIDs)
            {
                if (itemBlacklist.Contains(baseItemID))
                    continue;

                if (!SharedHelperScripts.IsEquippableItem(gd, baseItemID))
                    continue;

                try
                {
                    gd = ItemVarianting.PromoteItemToHighestTierAndCreateBackCopies(
                        gd,
                        baseItemID,
                        rare,
                        masterwork,
                        perfect,
                        legendary,
                        out var res
                    );

                    registry.Items[baseItemID] = new VariantRegistry.ItemEntry
                    {
                        BaseItemID = baseItemID,
                        PromotedItemID = res.PromotedItemID,
                        RareItemID = res.RareItemID,
                        MasterworkItemID = res.MasterworkItemID,
                        PerfectItemID = res.PerfectItemID,
                        OriginalCopyItemID = res.OriginalCopyItemID
                    };
                }
                catch
                {
                    // skip failures
                }
            }

            return gd;
        }

        // ------------------------------------------------------------
        // Spells (scrollable only)
        // ------------------------------------------------------------
        public static SFGameDataNew BuildSpellVariantsAndRegister(
            SFGameDataNew gd,
            IReadOnlyList<SpellVarianting.SpellModifierStructure> spellTierTable, // [Empowered, Superior, Perfected, Arch]
            VariantRegistry registry
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (spellTierTable == null) throw new ArgumentNullException(nameof(spellTierTable));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var baseSpellIDs = SpellPromotion.GetAllSpellsWithScrolls(gd);

            foreach (var baseSpellID in baseSpellIDs)
            {
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

            return gd;
        }
    }
}
