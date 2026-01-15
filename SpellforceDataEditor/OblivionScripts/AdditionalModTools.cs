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
    }
}
