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
    public class ItemVarianting
    {
        static public SFGameDataNew CreateItemVariant(SFGameDataNew gd, ushort baseItemID, ItemModifierStructure modifier)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            var itemCat = gd.c2003;
            var locCat = gd.c2016;
            var uiCat = gd.c2012;
            var effCat = gd.c2014;
            var reqCat = gd.c2017;
            var armorCat = gd.c2004;
            var weapCat = gd.c2015;

            // -------------------------------------------------
            // Find base item
            // -------------------------------------------------
            Category2003Item baseItem = default;
            bool found = false;

            foreach (var it in itemCat.Items)
            {
                if (it.ItemID == baseItemID)
                {
                    baseItem = it;
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new Exception($"Base item {baseItemID} not found.");

            // -------------------------------------------------
            // Allocate new ItemID
            // -------------------------------------------------
            ushort newItemID = 0;
            foreach (var it in itemCat.Items)
                if (it.ItemID > newItemID)
                    newItemID = it.ItemID;
            newItemID++;

            // -------------------------------------------------
            // Clone base item (c2003)
            // -------------------------------------------------
            var newItem = baseItem;
            newItem.ItemID = newItemID;

            newItem.BuyValue = (uint)(newItem.BuyValue * modifier.BuyMod);
            newItem.SellValue = (uint)(newItem.SellValue * modifier.SellMod);

            // -------------------------------------------------
            // Clone localisation (c2016)
            // -------------------------------------------------
            ushort baseTextID = baseItem.NameID;

            ushort newTextID = 0;
            foreach (var loc in locCat.Items)
                if (loc.TextID > newTextID)
                    newTextID = loc.TextID;
            newTextID++;

            int locBlockStart = locCat.Items.Count;
            locCat.Indices.Add(locBlockStart);

            bool anyLoc = false;
            foreach (var loc in locCat.Items.ToArray())
            {
                if (loc.TextID == baseTextID)
                {
                    var newLoc = loc;
                    newLoc.TextID = newTextID;

                    string text = SharedHelperScripts.ReadContent(ref newLoc);
                    SharedHelperScripts.WriteContent(ref newLoc, text + " [" + modifier.Suffix + "]");

                    locCat.Items.Add(newLoc);
                    anyLoc = true;
                }
            }

            if (!anyLoc)
                throw new Exception("Item localisation not found.");

            newItem.NameID = newTextID;

            // -------------------------------------------------
            // Clone UI data (c2012)
            // -------------------------------------------------
            int uiBlockStart = uiCat.Items.Count;
            uiCat.Indices.Add(uiBlockStart);

            foreach (var ui in uiCat.Items.ToArray())
            {
                if (ui.ItemID == baseItemID)
                {
                    var newUI = ui;
                    newUI.ItemID = newItemID;
                    uiCat.Items.Add(newUI);
                }
            }

            // -------------------------------------------------
            // Clone effects (c2014) – optional
            // -------------------------------------------------
            int effBlockStart = effCat.Items.Count;
            bool effFound = false;

            foreach (var ef in effCat.Items.ToArray())
            {
                if (ef.ItemID == baseItemID)
                {
                    if (!effFound)
                    {
                        effCat.Indices.Add(effBlockStart);
                        effFound = true;
                    }

                    var newEf = ef;
                    newEf.ItemID = newItemID;
                    effCat.Items.Add(newEf);
                }
            }

            // -------------------------------------------------
            // Clone requirements (c2017) – optional
            // -------------------------------------------------
            int reqBlockStart = reqCat.Items.Count;
            bool reqFound = false;

            foreach (var rq in reqCat.Items.ToArray())
            {
                if (rq.ItemID == baseItemID)
                {
                    if (!reqFound)
                    {
                        reqCat.Indices.Add(reqBlockStart);
                        reqFound = true;
                    }

                    var newRq = rq;
                    newRq.ItemID = newItemID;
                    reqCat.Items.Add(newRq);
                }
            }

            // -------------------------------------------------
            // Clone armor stats (c2004) – optional
            // -------------------------------------------------
            foreach (var ar in armorCat.Items.ToArray())
            {
                if (ar.ItemID == baseItemID)
                {
                    var newAr = ar;
                    newAr.ItemID = newItemID;

                    newAr.Armor = (short)(newAr.Armor * modifier.ArmorMod);
                    newAr.Strength = (short)(newAr.Strength * modifier.StrengthMod);
                    newAr.Stamina = (short)(newAr.Stamina * modifier.StaminaMod);
                    newAr.Agility = (short)(newAr.Agility * modifier.AgilityMod);
                    newAr.Dexterity = (short)(newAr.Dexterity * modifier.DexterityMod);
                    newAr.Charisma = (short)(newAr.Charisma * modifier.CharismaMod);
                    newAr.Intelligence = (short)(newAr.Intelligence * modifier.IntelligenceMod);
                    newAr.Wisdom = (short)(newAr.Wisdom * modifier.WisdomMod);

                    newAr.ResistFire = (short)(newAr.ResistFire * modifier.ResistancesMod);
                    newAr.ResistIce = (short)(newAr.ResistIce * modifier.ResistancesMod);
                    newAr.ResistMind = (short)(newAr.ResistMind * modifier.ResistancesMod);
                    newAr.ResistBlack = (short)(newAr.ResistBlack * modifier.ResistancesMod);

                    newAr.SpeedWalk = (short)(newAr.SpeedWalk * modifier.WalkMod);
                    newAr.SpeedFight = (short)(newAr.SpeedFight * modifier.FightMod);
                    newAr.SpeedCast = (short)(newAr.SpeedCast * modifier.CastMod);

                    newAr.Health = (short)(newAr.Health * modifier.HealthMod);
                    newAr.Mana = (short)(newAr.Mana * modifier.ManaMod);

                    armorCat.Items.Add(newAr);
                }
            }

            // -------------------------------------------------
            // Clone weapon stats (c2015) – optional
            // -------------------------------------------------
            foreach (var wp in weapCat.Items.ToArray())
            {
                if (wp.ItemID == baseItemID)
                {
                    var newWp = wp;
                    newWp.ItemID = newItemID;

                    newWp.MinDamage = (ushort)(newWp.MinDamage * modifier.MinDamageMod);
                    newWp.MaxDamage = (ushort)(newWp.MaxDamage * modifier.MaxDamageMod);
                    newWp.MaxRange = (ushort)(newWp.MaxRange * modifier.MaxRangeMod);
                    newWp.WeaponSpeed = (ushort)(newWp.WeaponSpeed * modifier.WeaponSpeedMod);

                    weapCat.Items.Add(newWp);
                }
            }

            // -------------------------------------------------
            // Insert base item last
            // -------------------------------------------------
            itemCat.Items.Add(newItem);

            return gd;
        }
    }
}
