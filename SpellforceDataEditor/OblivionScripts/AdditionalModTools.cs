using SFEngine.SFCFF;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpellforceDataEditor.OblivionScripts
{
    internal class AdditionalModTools
    {
        public static SFGameDataNew SetAllC2062AttributesTo25(SFGameDataNew gd)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (gd.c2062 == null) throw new Exception("gd.c2062 is null.");

            var cat = gd.c2062;

            for (int i = 0; i < cat.Items.Count; i++)
            {
                var it = cat.Items[i];

                it.Agility = 25;
                it.Charisma = 25;
                it.Dexterity = 25;
                it.Intelligence = 25;
                it.Stamina = 25;
                it.Strength = 25;
                it.Wisdom = 25;

                cat.Items[i] = it; // struct write-back
            }

            return gd;
        }

        public static SFGameDataNew SetAllC2048AttributePointLimitTo999(SFGameDataNew gd)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (gd.c2048 == null) throw new Exception("gd.c2048 is null.");

            var cat = gd.c2048;

            for (int i = 0; i < cat.Items.Count; i++)
            {
                var it = cat.Items[i];
                it.AttributePointLimit = 255;
                cat.Items[i] = it; // struct write-back
            }

            return gd;
        }
        // Make robes equippable with pants
        public static SFGameDataNew RemapC2003_ItemType2_10_To_2(SFGameDataNew gd)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (gd.c2003 == null) throw new Exception("gd.c2003 is null.");

            var cat = gd.c2003;

            for (int i = 0; i < cat.Items.Count; i++)
            {
                var it = cat.Items[i];

                if (it.ItemType2 == 10)
                {
                    it.ItemType2 = 2;
                    cat.Items[i] = it; // struct write-back
                }
            }

            return gd;
        }
        // Make 2H weapons 1H
        public static SFGameDataNew RemapC2003_ItemType2_8_To_7(SFGameDataNew gd)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (gd.c2003 == null) throw new Exception("gd.c2003 is null.");

            var cat = gd.c2003;

            for (int i = 0; i < cat.Items.Count; i++)
            {
                var it = cat.Items[i];

                if (it.ItemType2 == 8)
                {
                    it.ItemType2 = 7;
                    cat.Items[i] = it; // struct write-back
                }
            }

            return gd;
        }

        public static SFGameDataNew ReplaceUnitSpell_C2026(
            SFGameDataNew gd,
            ushort unitID,
            ushort oldSpellID,
            ushort newSpellID
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (gd.c2026 == null) throw new Exception("gd.c2026 is null.");

            var cat = gd.c2026;

            for (int i = 0; i < cat.Items.Count; i++)
            {
                var it = cat.Items[i];

                if (it.UnitID == unitID && it.SpellID == oldSpellID)
                {
                    it.SpellID = newSpellID;
                    cat.Items[i] = it; // struct write-back
                }
            }

            return gd;
        }

        // Increase max exp gained from mob
        public static SFGameDataNew MultiplyExperienceFalloff(SFGameDataNew gd, ushort exp_multiplier)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (gd.c2024 == null) throw new Exception("gd.c2024 is null.");

            var cat = gd.c2024;

            for (int i = 0; i < cat.Items.Count; i++)
            {
                var it = cat.Items[i];
                it.ExperienceFalloff *= exp_multiplier;
                cat.Items[i] = it; // struct write-back
            }

            return gd;
        }

        /// <summary>
        /// Scales army resource costs/requirements:
        /// - c2028.ResourceValue (byte)
        /// - c2031.ResourceRequirement (ushort)
        /// by a float multiplier (e.g. VariantTables.ArmyDiscountValue).
        /// </summary>
        public static SFGameDataNew ApplyArmyDiscountValue(
            SFGameDataNew gd,
            float armyDiscountValue
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (armyDiscountValue < 0f) throw new ArgumentOutOfRangeException(nameof(armyDiscountValue), "Must be >= 0.");

            // ---- c2028: byte ResourceValue ----
            for (int i = 0; i < gd.c2028.Items.Count; i++)
            {
                var it = gd.c2028.Items[i];

                // byte -> double scale -> clamp [0..255] -> byte
                double scaled = it.ResourceValue * (double)armyDiscountValue;
                int v = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
                if (v < 0) v = 0;
                if (v > 255) v = 255;

                it.ResourceValue = (byte)v;
                gd.c2028.Items[i] = it;
            }

            // ---- c2031: ushort ResourceRequirement ----
            for (int i = 0; i < gd.c2031.Items.Count; i++)
            {
                var it = gd.c2031.Items[i];

                // ushort -> double scale -> clamp [0..65535] -> ushort
                double scaled = it.ResourceRequirement * (double)armyDiscountValue;
                int v = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
                if (v < 0) v = 0;
                if (v > ushort.MaxValue) v = ushort.MaxValue;

                it.ResourceRequirement = (ushort)v;
                gd.c2031.Items[i] = it;
            }

            return gd;
        }

        public struct HeroBuffRecord
        {
            public ushort UnitID;
            public ushort OriginalStatsID;
            public byte EquipmentMode;
            public string ModifierTag; // "HeroModifierLimitedEQ" / "HeroModifierNoEQ"
        }

        /// <summary>
        /// Buffs UnitRace==0 units:
        /// - EquipmentMode==2 => apply heroModifierLimitedEQ
        /// - EquipmentMode==3 => apply heroModifierNoEQ
        /// Applies by promoting each matching UnitID in-place (no Unit variants created).
        /// Returns a list of affected UnitIDs for optional dumping.
        /// </summary>
        public static SFGameDataNew BuffRace0HeroesByEquipmentMode(
            SFGameDataNew gd,
            UnitVarianting.MobModifierStructure heroModifierLimitedEQ,
            UnitVarianting.MobModifierStructure heroModifierNoEQ,
            out List<HeroBuffRecord> affected
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            affected = new List<HeroBuffRecord>();

            // StatsID -> list of UnitIDs that use it
            var unitsByStats = new Dictionary<ushort, List<ushort>>();
            foreach (var u in gd.c2024.Items)
            {
                if (u.StatsID == 0) continue;
                if (!unitsByStats.TryGetValue(u.StatsID, out var list))
                {
                    list = new List<ushort>(1);
                    unitsByStats[u.StatsID] = list;
                }
                list.Add(u.UnitID);
            }

            // Walk stats (c2005) and apply as requested
            for (int i = 0; i < gd.c2005.Items.Count; i++)
            {
                var st = gd.c2005.Items[i];

                if (st.UnitRace != 0)
                    continue;

                bool match = (st.EquipmentMode == 2) || (st.EquipmentMode == 3);
                if (!match)
                    continue;

                if (!unitsByStats.TryGetValue(st.StatsID, out var unitIDs) || unitIDs.Count == 0)
                    continue;

                var mod = (st.EquipmentMode == 2) ? heroModifierLimitedEQ : heroModifierNoEQ;
                string tag = (st.EquipmentMode == 2) ? "HeroModifierLimitedEQ" : "HeroModifierNoEQ";

                // Apply to ALL units that reference this StatsID
                foreach (ushort unitID in unitIDs)
                {
                    // Resolve original NameID for this unit (needed by PromoteUnitInPlaceToTier)
                    ushort originalNameID = 0;
                    bool foundUnit = false;
                    for (int u = 0; u < gd.c2024.Items.Count; u++)
                    {
                        if (gd.c2024.Items[u].UnitID == unitID)
                        {
                            originalNameID = gd.c2024.Items[u].NameID;
                            foundUnit = true;
                            break;
                        }
                    }
                    if (!foundUnit)
                        continue;

                    // In-place promote (clones stats row, repoints unit.StatsID; name change is guarded by the fix above)
                    gd = UnitPromotion.PromoteUnitInPlaceToTier(gd, unitID, mod, originalNameID);

                    affected.Add(new HeroBuffRecord
                    {
                        UnitID = unitID,
                        OriginalStatsID = st.StatsID,
                        EquipmentMode = st.EquipmentMode,
                        ModifierTag = tag
                    });
                }
            }

            return gd;
        }

        /// <summary>
        /// Temporary helper: dumps affected UnitIDs + which modifier was used.
        /// </summary>
        public static void DumpHeroBuffedUnits(
            string outPath,
            IEnumerable<HeroBuffRecord> records
        )
        {
            if (string.IsNullOrWhiteSpace(outPath)) throw new ArgumentNullException(nameof(outPath));
            if (records == null) throw new ArgumentNullException(nameof(records));

            using var sw = new StreamWriter(outPath, false);
            sw.WriteLine("Hero buff report");
            sw.WriteLine("UnitID\tStatsID\tEquipmentMode\tModifier");

            foreach (var r in records)
            {
                sw.WriteLine(
                    r.UnitID.ToString(CultureInfo.InvariantCulture) + "\t" +
                    r.OriginalStatsID.ToString(CultureInfo.InvariantCulture) + "\t" +
                    r.EquipmentMode.ToString(CultureInfo.InvariantCulture) + "\t" +
                    r.ModifierTag
                );
            }
        }
    }
}
