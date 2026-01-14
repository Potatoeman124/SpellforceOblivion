using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpellforceDataEditor.OblivionScripts
{
    public static class UnitPromotion
    {
        public sealed class UnitVariantRecord
        {
            public string VariantName = "";
            public ushort UnitID;
            public bool IsPromotedBase;
            public bool IsOriginalCopy;
        }

        public sealed class UnitPromotionResult
        {
            public ushort BaseUnitID;
            public ushort PromotedUnitID;
            public ushort OriginalCopyUnitID;
            public List<UnitVariantRecord> Variants = new List<UnitVariantRecord>();
        }
        /// <summary>
        /// Promotes baseUnitID to Highest tier (last element in tierTable),
        /// and creates (in this exact order) Champion, Elite, Veteran, and Original copies as new UnitIDs.
        ///
        /// tierTable must be ordered: [Veteran, Elite, Champion, Oblivion] (low -> high).
        ///
        /// Returns:
        /// - promotedUnitID == baseUnitID (now highest tier)
        /// - veteranUnitID / eliteUnitID / championUnitID: new IDs
        /// - originalCopyUnitID: new ID containing the original unit data (pre-promotion name/stats/equipment/spells)
        /// </summary>
        public static SFGameDataNew PromoteUnitToHighestAndCreateBackCopies(
            SFGameDataNew gd,
            ushort baseUnitID,
            IReadOnlyList<UnitVarianting.MobModifierStructure> tierTable,
            HashSet<ushort> unitBlacklist,
            out UnitPromotionResult result
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            tierTable ??= Array.Empty<UnitVarianting.MobModifierStructure>();
            unitBlacklist ??= new HashSet<ushort>();

            result = new UnitPromotionResult
            {
                BaseUnitID = baseUnitID,
                PromotedUnitID = baseUnitID,
                OriginalCopyUnitID = 0
            };

            // 0 tiers => no-op
            if (tierTable.Count == 0)
                return gd;

            // blacklist => no-op
            if (unitBlacklist.Contains(baseUnitID))
                return gd;

            var unitCat = gd.c2024;
            var statsCat = gd.c2005;

            int baseUnitIndex = FindUnitIndex(unitCat, baseUnitID);
            if (baseUnitIndex < 0)
                throw new Exception($"Base unit {baseUnitID} not found in c2024.");

            var baseUnit = unitCat.Items[baseUnitIndex];

            // Guard: StatsID==0 units should not be promoted
            if (baseUnit.StatsID == 0)
                return gd;

            int baseStatsIndex = FindStatsIndex(statsCat, baseUnit.StatsID);
            if (baseStatsIndex < 0)
                throw new Exception($"StatsID {baseUnit.StatsID} for unit {baseUnitID} not found in c2005.");

            ushort originalStatsID = baseUnit.StatsID;
            ushort originalNameID = baseUnit.NameID;

            // --------------------------------------------------------------------
            // Create tier variants for ALL tiers except highest, BEFORE promoting base.
            // We allocate IDs strongest->weakest (descending) to keep your ID pattern stable.
            // --------------------------------------------------------------------
            int highestIndex = tierTable.Count - 1;

            // Holds the UnitID for each tier in tierTable order:
            // - for i < highestIndex: new clone ids
            // - for i == highestIndex: baseUnitID after promotion
            var tierUnitIDs = new ushort[tierTable.Count];
            tierUnitIDs[highestIndex] = baseUnitID;

            for (int i = highestIndex - 1; i >= 0; i--)
            {
                gd = UnitVarianting.CreateUnitVariant(gd, baseUnitID, tierTable[i], out ushort newUnitID);
                tierUnitIDs[i] = newUnitID;
            }

            // --------------------------------------------------------------------
            // Clone ORIGINAL (no suffix, no scaling) into a new UnitID
            // --------------------------------------------------------------------
            gd = CloneUnitAsOriginalCopy(
                gd,
                baseUnitID,
                originalNameID,
                originalStatsID,
                out ushort originalCopyUnitID
            );
            result.OriginalCopyUnitID = originalCopyUnitID;

            // --------------------------------------------------------------------
            // Promote BASE IN PLACE to highest tier (last element)
            // --------------------------------------------------------------------
            var highest = tierTable[highestIndex];
            gd = PromoteUnitInPlaceToTier(gd, baseUnitID, highest, originalNameID);
            result.PromotedUnitID = baseUnitID;

            // --------------------------------------------------------------------
            // Build result.Variants in tierTable order, then Original
            // --------------------------------------------------------------------
            result.Variants.Clear();
            for (int i = 0; i < tierTable.Count; i++)
            {
                result.Variants.Add(new UnitVariantRecord
                {
                    VariantName = tierTable[i].Suffix ?? "",
                    UnitID = tierUnitIDs[i],
                    IsPromotedBase = (i == highestIndex),
                    IsOriginalCopy = false
                });
            }

            result.Variants.Add(new UnitVariantRecord
            {
                VariantName = "Original",
                UnitID = originalCopyUnitID,
                IsPromotedBase = false,
                IsOriginalCopy = true
            });

            return gd;
        }

        /// <summary>
        /// Clones the base unit to a new UnitID as an "original copy":
        /// - Keeps NameID as provided (no suffix)
        /// - Keeps StatsID as provided (the original StatsID)
        /// - Clones c2025 (equipment) and c2026 (unit spells) blocks
        /// Does NOT clone stats row (c2005), by design.
        /// </summary>
        public static SFGameDataNew CloneUnitAsOriginalCopy(
            SFGameDataNew gd,
            ushort baseUnitID,
            ushort keepNameID,
            ushort keepStatsID,
            out ushort newUnitID
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            var unitCat = gd.c2024;

            int baseUnitIndex = FindUnitIndex(unitCat, baseUnitID);
            if (baseUnitIndex < 0) throw new Exception($"Base unit {baseUnitID} not found in c2024.");

            // Allocate a new UnitID using a global max across unit-related categories
            newUnitID = GetMaxUnitID_Global(gd);
            newUnitID++;

            // Clone unit row
            var baseUnit = unitCat.Items[baseUnitIndex];
            var newUnit = baseUnit;
            newUnit.UnitID = newUnitID;
            newUnit.NameID = keepNameID;
            newUnit.StatsID = keepStatsID;

            unitCat.Items.Add(newUnit);

            // Clone equipment (c2025) and spells (c2026)
            CloneUnitEquipmentBlock(gd.c2025, baseUnitID, newUnitID);
            CloneUnitSpellBlock(gd.c2026, baseUnitID, newUnitID);

            return gd;
        }

        /// <summary>
        /// Promotes the base unit (same UnitID) by:
        /// - cloning its current stats row to a new StatsID
        /// - applying mob modifiers to that cloned stats row
        /// - repointing c2024.StatsID to the new stats
        /// - cloning localisation NameID with suffix and assigning it to the base unit
        /// </summary>
        public static SFGameDataNew PromoteUnitInPlaceToTier(
            SFGameDataNew gd,
            ushort baseUnitID,
            UnitVarianting.MobModifierStructure tierMod,
            ushort originalNameID
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            // doesn't work
            // if (tierMod == null) throw new ArgumentNullException(nameof(tierMod));

            var unitCat = gd.c2024;
            var statsCat = gd.c2005;
            var locCat = gd.c2016;

            int unitIndex = FindUnitIndex(unitCat, baseUnitID);
            if (unitIndex < 0) throw new Exception($"Base unit {baseUnitID} not found in c2024.");

            var unit = unitCat.Items[unitIndex];

            if (unit.StatsID == 0)
                return gd; // blacklisted scenario, do nothing

            int statsIndex = FindStatsIndex(statsCat, unit.StatsID);
            if (statsIndex < 0) throw new Exception($"StatsID {unit.StatsID} for unit {baseUnitID} not found in c2005.");

            var baseStats = statsCat.Items[statsIndex];

            // Allocate new StatsID
            ushort newStatsID = GetMaxStatsID(statsCat);
            newStatsID++;

            // Clone + apply modifiers
            var newStats = baseStats;
            newStats.StatsID = newStatsID;
            ApplyMobModifiers(ref newStats, tierMod);

            statsCat.Items.Add(newStats);

            // Update unit to use new stats
            unit.StatsID = newStatsID;

            // Update promoted unit name with suffix (requested)
            ushort newNameID = SharedHelperScripts.CloneLocalisationTextID_512(
                locCat,
                originalNameID,
                suffix: tierMod.Suffix,
                appendSuffix: true
            );
            unit.NameID = newNameID;

            unitCat.Items[unitIndex] = unit;
            return gd;
        }

        // =====================================================================
        // Internals / small helpers
        // =====================================================================

        public static ushort GetMaxUnitID_Global(SFGameDataNew gd)
        {
            ushort max = 0;

            foreach (var u in gd.c2024.Items)
                if (u.UnitID > max) max = u.UnitID;

            // Ensure CategoryBaseMultiple blocks won’t violate ordering assumptions
            foreach (var e in gd.c2025.Items)
                if (e.UnitID > max) max = e.UnitID;

            foreach (var s in gd.c2026.Items)
                if (s.UnitID > max) max = s.UnitID;

            return max;
        }

        public static ushort GetMaxStatsID(Category2005 statsCat)
        {
            ushort max = 0;
            foreach (var s in statsCat.Items)
                if (s.StatsID > max) max = s.StatsID;
            return max;
        }

        public static int FindUnitIndex(Category2024 unitCat, ushort unitID)
        {
            for (int i = 0; i < unitCat.Items.Count; i++)
                if (unitCat.Items[i].UnitID == unitID)
                    return i;
            return -1;
        }

        public static int FindStatsIndex(Category2005 statsCat, ushort statsID)
        {
            for (int i = 0; i < statsCat.Items.Count; i++)
                if (statsCat.Items[i].StatsID == statsID)
                    return i;
            return -1;
        }

        public static void CloneUnitEquipmentBlock(Category2025 equipCat, ushort baseUnitID, ushort newUnitID)
        {
            int start = equipCat.Items.Count;
            bool any = false;

            foreach (var e in equipCat.Items.ToArray())
            {
                if (e.UnitID != baseUnitID) continue;

                if (!any)
                {
                    equipCat.Indices.Add(start);
                    any = true;
                }

                var ne = e;
                ne.UnitID = newUnitID;
                equipCat.Items.Add(ne);
            }
        }

        public static void CloneUnitSpellBlock(Category2026 unitSpellCat, ushort baseUnitID, ushort newUnitID)
        {
            int start = unitSpellCat.Items.Count;
            bool any = false;

            foreach (var sp in unitSpellCat.Items.ToArray())
            {
                if (sp.UnitID != baseUnitID) continue;

                if (!any)
                {
                    unitSpellCat.Indices.Add(start);
                    any = true;
                }

                var nsp = sp;
                nsp.UnitID = newUnitID;
                unitSpellCat.Items.Add(nsp);
            }
        }

        public static void ApplyMobModifiers(ref Category2005Item stats, UnitVarianting.MobModifierStructure mod)
        {
            // Use your SharedHelperScripts scaling helpers if available; otherwise clamp manually.
            stats.Strength = SharedHelperScripts.ScaleUShort(stats.Strength, mod.StrengthMod);
            stats.Stamina = SharedHelperScripts.ScaleUShort(stats.Stamina, mod.StaminaMod);
            stats.Agility = SharedHelperScripts.ScaleUShort(stats.Agility, mod.AgilityMod);
            stats.Dexterity = SharedHelperScripts.ScaleUShort(stats.Dexterity, mod.DexterityMod);
            stats.Charisma = SharedHelperScripts.ScaleUShort(stats.Charisma, mod.CharismaMod);
            stats.Intelligence = SharedHelperScripts.ScaleUShort(stats.Intelligence, mod.IntelligenceMod);
            stats.Wisdom = SharedHelperScripts.ScaleUShort(stats.Wisdom, mod.WisdomMod);

            stats.ResistanceFire = SharedHelperScripts.ScaleUShort(stats.ResistanceFire, mod.ResistancesMod);
            stats.ResistanceIce = SharedHelperScripts.ScaleUShort(stats.ResistanceIce, mod.ResistancesMod);
            stats.ResistanceMind = SharedHelperScripts.ScaleUShort(stats.ResistanceMind, mod.ResistancesMod);
            stats.ResistanceBlack = SharedHelperScripts.ScaleUShort(stats.ResistanceBlack, mod.ResistancesMod);

            stats.SpeedWalk = SharedHelperScripts.ScaleUShort(stats.SpeedWalk, mod.WalkMod);
            stats.SpeedFight = SharedHelperScripts.ScaleUShort(stats.SpeedFight, mod.FightMod);
            stats.SpeedCast = SharedHelperScripts.ScaleUShort(stats.SpeedCast, mod.CastMod);
        }
    }
}
