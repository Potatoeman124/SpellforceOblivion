using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using SpellforceDataEditor.OblivionScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SpellforceDataEditor.special_forms.SpelllforceCFFEditor;

namespace SpellforceDataEditor.OblivionScripts
{
    public class UnitVarianting
    {
        public struct MobModifierStructure
        {
            public float StrengthMod;
            public float StaminaMod;
            public float AgilityMod;
            public float DexterityMod;
            public float CharismaMod;
            public float IntelligenceMod;
            public float WisdomMod;

            public float ResistancesMod;

            public float WalkMod;
            public float FightMod;
            public float CastMod;

            public string Suffix;
        }

        //=====================================================================================================

        public static SFGameDataNew CreateUnitVariant(
            SFGameDataNew gd,
            ushort baseUnitID,
            MobModifierStructure modifier,
            out ushort newUnitID
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            var unitCat = gd.c2024;
            var statsCat = gd.c2005;
            var locCat = gd.c2016;
            var equipCat = gd.c2025;

            // Find base unit
            Category2024Item baseUnit = default;
            bool found = false;
            foreach (var u in unitCat.Items)
            {
                if (u.UnitID == baseUnitID) { baseUnit = u; found = true; break; }
            }
            if (!found) throw new Exception($"Base unit {baseUnitID} not found.");

            // Find base stats
            Category2005Item baseStats = default;
            found = false;
            foreach (var s in statsCat.Items)
            {
                if (s.StatsID == baseUnit.StatsID) { baseStats = s; found = true; break; }
            }
            if (!found) throw new Exception($"Stats for unit {baseUnitID} not found.");

            // Allocate IDs
            //newUnitID = 0;
            //foreach (var u in unitCat.Items)
            //    if (u.UnitID > newUnitID) newUnitID = u.UnitID;
            //newUnitID++;
            newUnitID = 0;
            foreach (var u in unitCat.Items)
                if (u.UnitID > newUnitID) newUnitID = u.UnitID;

            foreach (var sp in gd.c2026.Items)
                if (sp.UnitID > newUnitID) newUnitID = sp.UnitID;

            newUnitID++;

            ushort newStatsID = 0;
            foreach (var s in statsCat.Items)
                if (s.StatsID > newStatsID) newStatsID = s.StatsID;
            newStatsID++;

            // Clone stats with modifiers
            var newStats = baseStats;
            newStats.StatsID = newStatsID;

            newStats.Strength = (ushort)(newStats.Strength * modifier.StrengthMod);
            newStats.Stamina = (ushort)(newStats.Stamina * modifier.StaminaMod);
            newStats.Agility = (ushort)(newStats.Agility * modifier.AgilityMod);
            newStats.Dexterity = (ushort)(newStats.Dexterity * modifier.DexterityMod);
            newStats.Charisma = (ushort)(newStats.Charisma * modifier.CharismaMod);
            newStats.Intelligence = (ushort)(newStats.Intelligence * modifier.IntelligenceMod);
            newStats.Wisdom = (ushort)(newStats.Wisdom * modifier.WisdomMod);

            newStats.ResistanceFire = (ushort)(newStats.ResistanceFire * modifier.ResistancesMod);
            newStats.ResistanceIce = (ushort)(newStats.ResistanceIce * modifier.ResistancesMod);
            newStats.ResistanceMind = (ushort)(newStats.ResistanceMind * modifier.ResistancesMod);
            newStats.ResistanceBlack = (ushort)(newStats.ResistanceBlack * modifier.ResistancesMod);

            newStats.SpeedWalk = (ushort)(newStats.SpeedWalk * modifier.WalkMod);
            newStats.SpeedFight = (ushort)(newStats.SpeedFight * modifier.FightMod);
            newStats.SpeedCast = (ushort)(newStats.SpeedCast * modifier.CastMod);

            // Clone unit
            var newUnit = baseUnit;
            newUnit.UnitID = newUnitID;
            newUnit.StatsID = newStatsID;

            // Clone localisation
            if (!string.IsNullOrWhiteSpace(modifier.Suffix))
            {
                ushort newNameID = SharedHelperScripts.CloneLocalisationTextID_512(
                    locCat,
                    baseUnit.NameID,
                    suffix: modifier.Suffix,
                    appendSuffix: true
                );
                newUnit.NameID = newNameID;
            }

            // Clone equipment (optional)
            int equipBlockStart = equipCat.Items.Count;
            bool anyEquip = false;
            foreach (var e in equipCat.Items.ToArray())
            {
                if (e.UnitID != baseUnitID) continue;
                if (!anyEquip) { equipCat.Indices.Add(equipBlockStart); anyEquip = true; }
                var ne = e;
                ne.UnitID = newUnitID;
                equipCat.Items.Add(ne);
            }

            // -------------------------------------------------
            // Clone unit spells (c2026) – optional but required for summoned units
            // -------------------------------------------------
            var unitSpellCat = gd.c2026;

            int newSpellBlockStart = unitSpellCat.Items.Count;
            bool anySpellCloned = false;

            foreach (var sp in unitSpellCat.Items.ToArray())
            {
                if (sp.UnitID == baseUnitID)
                {
                    if (!anySpellCloned)
                    {
                        unitSpellCat.Indices.Add(newSpellBlockStart);
                        anySpellCloned = true;
                    }

                    var newSp = sp;
                    newSp.UnitID = newUnitID;
                    unitSpellCat.Items.Add(newSp);
                }
            }

            // Insert stats + unit
            statsCat.Items.Add(newStats);
            unitCat.Items.Add(newUnit);

            return gd;
        }

        // Keep old API working
        public static SFGameDataNew CreateUnitVariant(SFGameDataNew gd, ushort baseUnitID, MobModifierStructure modifier)
        {
            return CreateUnitVariant(gd, baseUnitID, modifier, out _);
        }

        static public SFGameDataNew ApplyBossModifiers(SFGameDataNew gd, MobModifierStructure modifier)
        {
            var unitCat = gd.c2024;
            var statsCat = gd.c2005;
            var locCat = gd.c2016;

            var bossRaces = CollectBossRaces(gd);

            for (int u = 0; u < unitCat.Items.Count; u++)
            {
                var unit = unitCat.Items[u];

                // -----------------------------
                // Resolve stats entry
                // -----------------------------
                int statsIndex = -1;
                for (int s = 0; s < statsCat.Items.Count; s++)
                {
                    if (statsCat.Items[s].StatsID == unit.StatsID)
                    {
                        statsIndex = s;
                        break;
                    }
                }

                if (statsIndex < 0)
                    continue;

                var stats = statsCat.Items[statsIndex];

                // -----------------------------
                // Check if this is a Boss race
                // -----------------------------
                if (!bossRaces.Contains(stats.UnitRace))
                    continue;

                // -----------------------------
                // Modify stats IN PLACE
                // -----------------------------
                stats.Strength = (ushort)(stats.Strength * modifier.StrengthMod);
                stats.Stamina = (ushort)(stats.Stamina * modifier.StaminaMod);
                stats.Agility = (ushort)(stats.Agility * modifier.AgilityMod);
                stats.Dexterity = (ushort)(stats.Dexterity * modifier.DexterityMod);
                stats.Charisma = (ushort)(stats.Charisma * modifier.CharismaMod);
                stats.Intelligence = (ushort)(stats.Intelligence * modifier.IntelligenceMod);
                stats.Wisdom = (ushort)(stats.Wisdom * modifier.WisdomMod);

                stats.ResistanceFire = (ushort)(stats.ResistanceFire * modifier.ResistancesMod);
                stats.ResistanceIce = (ushort)(stats.ResistanceIce * modifier.ResistancesMod);
                stats.ResistanceMind = (ushort)(stats.ResistanceMind * modifier.ResistancesMod);
                stats.ResistanceBlack = (ushort)(stats.ResistanceBlack * modifier.ResistancesMod);

                stats.SpeedWalk = (ushort)(stats.SpeedWalk * modifier.WalkMod);
                stats.SpeedFight = (ushort)(stats.SpeedFight * modifier.FightMod);
                stats.SpeedCast = (ushort)(stats.SpeedCast * modifier.CastMod);

                statsCat.Items[statsIndex] = stats;

                // -----------------------------
                // Modify localisation IN PLACE
                // -----------------------------
                for (int l = 0; l < locCat.Items.Count; l++)
                {
                    if (locCat.Items[l].TextID == unit.NameID)
                    {
                        var loc = locCat.Items[l];
                        string text = SharedHelperScripts.ReadContent256(ref loc);

                        if (!text.Contains("[" + modifier.Suffix + "]"))
                        {
                            SharedHelperScripts.WriteContent256(ref loc, text + " [" + modifier.Suffix + "]");
                            locCat.Items[l] = loc;
                        }
                    }
                }
            }

            return gd;
        }

        static public HashSet<byte> CollectBossRaces(SFGameDataNew gd)
        {
            var bossRaces = new HashSet<byte>();
            var raceCat = gd.c2022;

            foreach (var race in raceCat.Items)
            {
                if (SharedHelperScripts.TextContains(gd, race.TextID, "Boss"))
                {
                    bossRaces.Add(race.RaceID);
                }
            }

            return bossRaces;
        }
    }
}
