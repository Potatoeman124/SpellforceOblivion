using SFEngine.SFCFF;
using System;
using System.Collections.Generic;
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
    }
}
