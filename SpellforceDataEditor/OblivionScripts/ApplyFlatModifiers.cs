using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static SpellforceDataEditor.OblivionScripts.ItemVarianting;
using static SpellforceDataEditor.OblivionScripts.SpellVarianting;

namespace SpellforceDataEditor.OblivionScripts
{
    internal class ApplyFlatModifiers
    {
        public static SFGameDataNew ApplyFlatMobBonusesToUnits(
            SFGameDataNew gd,
            UnitVarianting.MobModifierStructure modifier,
            HashSet<ushort>? unitBlacklist = null,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            // Force "no suffix / no renaming" behavior.
            var mod = modifier;
            mod.Suffix = "";

            unitBlacklist ??= new HashSet<ushort>();

            // Collect unique StatsIDs to touch (avoid double-scaling if multiple units share StatsID)
            var statsToTouch = new HashSet<ushort>();
            foreach (var u in gd.c2024.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (u.StatsID == 0) continue;
                if (unitBlacklist.Contains(u.UnitID)) continue;

                statsToTouch.Add(u.StatsID);
            }

            int total = statsToTouch.Count;
            int done = 0;
            int interval = Math.Max(1, ProgressInfo.ProgressUpdateInterval);

            // Apply directly to c2005 stats entries
            for (int i = 0; i < gd.c2005.Items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var st = gd.c2005.Items[i];
                if (!statsToTouch.Contains(st.StatsID))
                    continue;

                // Reuse the same scaling logic used by promotion
                UnitPromotion.ApplyMobModifiers(ref st, mod);
                gd.c2005.Items[i] = st;

                done++;
                if (progress != null && (done % interval == 0 || done == total))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Flat bonuses (units)",
                        Detail = $"Scaled StatsIDs: {done}/{total}",
                        Current = done,
                        Total = total
                    });
                }
            }

            return gd;
        }

        public static SFGameDataNew ApplyFlatItemBonusesToEquippables(
            SFGameDataNew gd,
            ItemModifierStructure modifier,
            HashSet<ushort>? itemBlacklist = null,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            var mod = modifier;
            mod.Suffix = ""; // no suffix, no name changes

            itemBlacklist ??= new HashSet<ushort>();

            // Build list of equippable ItemIDs once
            var equippable = new List<ushort>();
            foreach (var it in gd.c2003.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (itemBlacklist.Contains(it.ItemID))
                    continue;

                if (!SharedHelperScripts.IsEquippableItem(gd, it.ItemID))
                    continue;

                equippable.Add(it.ItemID);
            }

            int total = equippable.Count;
            int done = 0;
            int interval = Math.Max(1, ProgressInfo.ProgressUpdateInterval);

            foreach (var itemID in equippable)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ApplyItemModifierInPlace_NoSuffixNoName(gd, itemID, mod);

                done++;
                if (progress != null && (done % interval == 0 || done == total))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Flat bonuses (items)",
                        Detail = $"Scaled equippables: {done}/{total}",
                        Current = done,
                        Total = total
                    });
                }
            }

            return gd;
        }

        private static void ApplyItemModifierInPlace_NoSuffixNoName(
            SFGameDataNew gd,
            ushort itemID,
            ItemModifierStructure modifier
        )
        {
            // IMPORTANT: do NOT touch NameID / localisation here.

            // c2003 (base item)
            for (int i = 0; i < gd.c2003.Items.Count; i++)
            {
                if (gd.c2003.Items[i].ItemID != itemID) continue;

                var it = gd.c2003.Items[i];

                it.BuyValue = SharedHelperScripts.ScaleUInt(it.BuyValue, modifier.BuyMod);
                it.SellValue = SharedHelperScripts.ScaleUInt(it.SellValue, modifier.SellMod);

                gd.c2003.Items[i] = it;
                break;
            }

            // c2004 (armor stats) – if present
            for (int i = 0; i < gd.c2004.Items.Count; i++)
            {
                if (gd.c2004.Items[i].ItemID != itemID) continue;

                var ar = gd.c2004.Items[i];

                ar.Armor = SharedHelperScripts.ScaleShort(ar.Armor, modifier.ArmorMod);

                ar.Strength = SharedHelperScripts.ScaleShort(ar.Strength, modifier.StrengthMod);
                ar.Stamina = SharedHelperScripts.ScaleShort(ar.Stamina, modifier.StaminaMod);
                ar.Agility = SharedHelperScripts.ScaleShort(ar.Agility, modifier.AgilityMod);
                ar.Dexterity = SharedHelperScripts.ScaleShort(ar.Dexterity, modifier.DexterityMod);
                ar.Charisma = SharedHelperScripts.ScaleShort(ar.Charisma, modifier.CharismaMod);
                ar.Intelligence = SharedHelperScripts.ScaleShort(ar.Intelligence, modifier.IntelligenceMod);
                ar.Wisdom = SharedHelperScripts.ScaleShort(ar.Wisdom, modifier.WisdomMod);

                ar.ResistFire = SharedHelperScripts.ScaleShort(ar.ResistFire, modifier.ResistancesMod);
                ar.ResistIce = SharedHelperScripts.ScaleShort(ar.ResistIce, modifier.ResistancesMod);
                ar.ResistMind = SharedHelperScripts.ScaleShort(ar.ResistMind, modifier.ResistancesMod);
                ar.ResistBlack = SharedHelperScripts.ScaleShort(ar.ResistBlack, modifier.ResistancesMod);

                ar.SpeedWalk = SharedHelperScripts.ScaleShort(ar.SpeedWalk, modifier.WalkMod);
                ar.SpeedFight = SharedHelperScripts.ScaleShort(ar.SpeedFight, modifier.FightMod);
                ar.SpeedCast = SharedHelperScripts.ScaleShort(ar.SpeedCast, modifier.CastMod);

                ar.Health = SharedHelperScripts.ScaleShort(ar.Health, modifier.HealthMod);
                ar.Mana = SharedHelperScripts.ScaleShort(ar.Mana, modifier.ManaMod);

                gd.c2004.Items[i] = ar;
                break;
            }

            // c2015 (weapon stats) – if present
            for (int i = 0; i < gd.c2015.Items.Count; i++)
            {
                if (gd.c2015.Items[i].ItemID != itemID) continue;

                var wp = gd.c2015.Items[i];

                wp.WeaponSpeed = SharedHelperScripts.ScaleUShort(wp.WeaponSpeed, modifier.WeaponSpeedMod);
                wp.MinDamage = SharedHelperScripts.ScaleUShort(wp.MinDamage, modifier.MinDamageMod);
                wp.MaxDamage = SharedHelperScripts.ScaleUShort(wp.MaxDamage, modifier.MaxDamageMod);

                wp.MaxRange = SharedHelperScripts.ScaleUShort(wp.MaxRange, modifier.MaxRangeMod);

                gd.c2015.Items[i] = wp;
                break;
            }
        }
    }
}
