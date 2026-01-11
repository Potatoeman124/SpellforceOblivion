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
        public struct ItemModifierStructure
        {
            public float ArmorMod;

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

            public float HealthMod;
            public float ManaMod;

            public float WeaponSpeedMod;
            public float MinDamageMod;
            public float MaxDamageMod;
            public float MaxRangeMod;

            public float SellMod;
            public float BuyMod;

            public string Suffix;
        }

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

                    string text = SharedHelperScripts.ReadContent256(ref newLoc);
                    SharedHelperScripts.WriteContent256(ref newLoc, text + " [" + modifier.Suffix + "]");

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

        // ============================================================================================
        // Spell scroll + spellbook entry generator (for a newly created spell variant)
        // ============================================================================================
        public static SFGameDataNew CreateSpellScrollAndSpellbookForSpellVariant(
            SFGameDataNew gd,
            ushort baseSpellID,
            ushort newSpellID,
            SpellVarianting.SpellModifierStructure multipliers,
            out ushort newScrollItemID,
            out ushort newSpellbookItemID
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (multipliers == null) throw new ArgumentNullException(nameof(multipliers));

            var itemCat = gd.c2003; // items
            var locCat = gd.c2016; // localisation
            var uiCat = gd.c2012; // item UI
            var effCat = gd.c2014; // inventory item -> effect (scroll uses this)
            var linkCat = gd.c2013; // inventory scroll -> installed spell item
            var reqCat = gd.c2017; // requirements for installed spell item
            var sbCat = gd.c2018; // spellbook item -> effect (spell)

            // -------------------------------------------------
            // 1) Resolve base inventory scroll item via c2014 (EffectID = baseSpellID)
            // -------------------------------------------------
            ushort baseSpellbookItemID = 0;
            {
                var sb = sbCat.Items.FirstOrDefault(x => x.EffectID == baseSpellID);
                if (sb.SpellItemID == 0)
                    throw new Exception($"No spellbook item found for baseSpellID={baseSpellID} in c2018.");

                baseSpellbookItemID = sb.SpellItemID;
            }

            // -------------------------------------------------
            // 2) Resolve base inventory scroll item via c2013 (InstalledScrollItemID = baseSpellbookItemID)
            // -------------------------------------------------
            ushort baseScrollItemID = 0;
            {
                var link = linkCat.Items.FirstOrDefault(x => x.InstalledScrollItemID == baseSpellbookItemID);
                if (link.ItemID == 0)
                    throw new Exception($"No inventory scroll found for base spellbook item {baseSpellbookItemID} (spell {baseSpellID}) in c2013.");

                baseScrollItemID = link.ItemID;
            }

            // Sanity: base spellbook entry should exist in c2018
            {
                var sb = sbCat.Items.FirstOrDefault(x => x.SpellItemID == baseSpellbookItemID);
                if (sb.SpellItemID == 0)
                    throw new Exception($"No c2018 spellbook link found for base spellbook item {baseSpellbookItemID}.");
            }

            // -------------------------------------------------
            // 3) Find base item records in c2003
            // -------------------------------------------------
            Category2003Item baseScrollItem = default;
            Category2003Item baseSpellbookItem = default;

            bool foundScroll = false, foundSpellbook = false;
            foreach (var it in itemCat.Items)
            {
                if (!foundScroll && it.ItemID == baseScrollItemID) { baseScrollItem = it; foundScroll = true; }
                if (!foundSpellbook && it.ItemID == baseSpellbookItemID) { baseSpellbookItem = it; foundSpellbook = true; }
                if (foundScroll && foundSpellbook) break;
            }

            if (!foundScroll) throw new Exception($"Base scroll item {baseScrollItemID} not found in c2003.");
            if (!foundSpellbook) throw new Exception($"Base spellbook item {baseSpellbookItemID} not found in c2003.");

            // -------------------------------------------------
            // 4) Allocate 2 new ItemIDs
            // -------------------------------------------------
            ushort maxItemID = SharedHelperScripts.GetMaxItemID_Global(gd);

            newScrollItemID = (ushort)(maxItemID + 1);
            newSpellbookItemID = (ushort)(maxItemID + 2);

            // -------------------------------------------------
            // 5) Clone localisation (NameID) for both items, appending suffix
            // -------------------------------------------------
            ushort newScrollNameID = SharedHelperScripts.CloneLocalisationTextID_512(
                locCat,
                baseScrollItem.NameID,
                suffix: multipliers.Suffix,
                appendSuffix: true
            );

            ushort newSpellbookNameID = SharedHelperScripts.CloneLocalisationTextID_512(
                locCat,
                baseSpellbookItem.NameID,
                suffix: multipliers.Suffix,
                appendSuffix: true
            );

            // -------------------------------------------------
            // 6) Clone c2003 items
            // -------------------------------------------------
            var newScrollItem = baseScrollItem;
            newScrollItem.ItemID = newScrollItemID;
            newScrollItem.NameID = newScrollNameID;

            var newSpellbookItem = baseSpellbookItem;
            newSpellbookItem.ItemID = newSpellbookItemID;
            newSpellbookItem.NameID = newSpellbookNameID;

            newScrollItem.BuyValue = (uint)Math.Clamp((double)newScrollItem.BuyValue * multipliers.BuyPriceMult, 0, uint.MaxValue);
            newScrollItem.SellValue = (uint)Math.Clamp((double)newScrollItem.SellValue * multipliers.SellPriceMult, 0, uint.MaxValue);

            newSpellbookItem.BuyValue = (uint)Math.Clamp((double)newSpellbookItem.BuyValue * multipliers.BuyPriceMult, 0, uint.MaxValue);
            newSpellbookItem.SellValue = (uint)Math.Clamp((double)newSpellbookItem.SellValue * multipliers.SellPriceMult, 0, uint.MaxValue);

            itemCat.Items.Add(newScrollItem);
            itemCat.Items.Add(newSpellbookItem);

            // -------------------------------------------------
            // 7) Clone UI data (c2012) for both items
            // -------------------------------------------------
            CloneUIBlock(uiCat, baseScrollItemID, newScrollItemID);
            CloneUIBlock(uiCat, baseSpellbookItemID, newSpellbookItemID);

            // -------------------------------------------------
            // 8) Clone inventory scroll effects (c2014): ItemID -> EffectID (spell)
            //    Update EffectID from baseSpellID to newSpellID (leave other effects intact if any).
            // -------------------------------------------------

            // c2014 for inventory scroll item (points to spell)
            CloneOrCreateC2014BlockForItem(effCat, baseScrollItemID, newScrollItemID, baseSpellID, newSpellID);

            // c2014 for spellbook item as well (your UI expects it)
            CloneOrCreateC2014BlockForItem(effCat, baseSpellbookItemID, newSpellbookItemID, baseSpellID, newSpellID);
            // -------------------------------------------------
            // 9) Create new c2013 link: (new inventory scroll) -> (new spellbook item)
            // -------------------------------------------------
            {
                var newLink = new Category2013Item
                {
                    ItemID = newScrollItemID,
                    InstalledScrollItemID = newSpellbookItemID
                };
                linkCat.Items.Add(newLink);
            }

            // -------------------------------------------------
            // 10) Clone spellbook link (c2018): SpellItemID -> EffectID (spell)
            //     Set EffectID = newSpellID
            // -------------------------------------------------
            {
                var baseSb = sbCat.Items.FirstOrDefault(x => x.SpellItemID == baseSpellbookItemID);
                var newSb = baseSb;
                newSb.SpellItemID = newSpellbookItemID;
                newSb.EffectID = newSpellID;
                sbCat.Items.Add(newSb);
            }

            // -------------------------------------------------
            // 11) Clone requirements (c2017) from base spellbook item to new spellbook item
            // -------------------------------------------------
            CloneRequirements(reqCat, baseSpellbookItemID, newSpellbookItemID);

            return gd;
        }

        // ============================================================================================
        // Helpers (keep private; only public entrypoint above)
        // ============================================================================================

        private static void CloneUIBlock(Category2012 uiCat, ushort baseItemID, ushort newItemID)
        {
            int blockStart = uiCat.Items.Count;
            uiCat.Indices.Add(blockStart);

            bool any = false;
            foreach (var ui in uiCat.Items.ToArray())
            {
                if (ui.ItemID != baseItemID) continue;

                var nu = ui;
                nu.ItemID = newItemID;
                uiCat.Items.Add(nu);
                any = true;
            }

            if (!any)
                throw new Exception($"UI data (c2012) not found for base item {baseItemID}.");
        }

        private static void CloneInventoryEffectsForScroll(
            Category2014 effCat,
            ushort baseScrollItemID,
            ushort newScrollItemID,
            ushort baseSpellID,
            ushort newSpellID
        )
        {
            int blockStart = effCat.Items.Count;
            effCat.Indices.Add(blockStart);

            bool any = false;
            foreach (var e in effCat.Items.ToArray())
            {
                if (e.ItemID != baseScrollItemID) continue;

                var ne = e;
                ne.ItemID = newScrollItemID;

                // Only rewrite the spell pointer; preserve other effects if they exist.
                if (ne.EffectID == baseSpellID)
                    ne.EffectID = newSpellID;

                effCat.Items.Add(ne);
                any = true;
            }

            if (!any)
                throw new Exception($"Effect link (c2014) not found for base scroll item {baseScrollItemID}.");
        }

        private static void CloneRequirements(Category2017 reqCat, ushort baseItemID, ushort newItemID)
        {
            int blockStart = reqCat.Items.Count;
            reqCat.Indices.Add(blockStart);

            bool any = false;
            foreach (var r in reqCat.Items.ToArray())
            {
                if (r.ItemID != baseItemID) continue;

                var nr = r;
                nr.ItemID = newItemID;
                reqCat.Items.Add(nr);
                any = true;
            }

            if (!any)
                throw new Exception($"Requirements (c2017) not found for base spellbook item {baseItemID}.");
        }

        public static void CloneOrCreateC2014BlockForItem(
        Category2014 effCat,
        ushort baseItemID,
        ushort newItemID,
        ushort baseSpellID,
        ushort newSpellID
        )
        {
            // Collect base rows
            var baseRows = effCat.Items.Where(x => x.ItemID == baseItemID).ToList();

            // Start a new block in CategoryBaseMultiple
            int blockStart = effCat.Items.Count;
            effCat.Indices.Add(blockStart);

            if (baseRows.Count == 0)
            {
                // Base item has no c2014 rows -> create a minimal block
                effCat.Items.Add(new Category2014Item
                {
                    ItemID = newItemID,
                    EffectID = newSpellID,
                    EffectIndex = 1
                });
                return;
            }

            bool rewroteAny = false;
            bool hasIndex1 = false;

            foreach (var r in baseRows)
            {
                var nr = r;
                nr.ItemID = newItemID;

                if (nr.EffectID == baseSpellID)
                {
                    nr.EffectID = newSpellID;
                    rewroteAny = true;
                }

                if (nr.EffectIndex == 1)
                    hasIndex1 = true;

                effCat.Items.Add(nr);
            }

            // Safety: if base rows didn’t contain baseSpellID (rare, but happens),
            // force EffectIndex==1 to point to the new spell.
            if (!rewroteAny)
            {
                // Try to find index==1 within the block we just appended
                for (int i = blockStart; i < effCat.Items.Count; i++)
                {
                    if (effCat.Items[i].ItemID == newItemID && effCat.Items[i].EffectIndex == 1)
                    {
                        var fix = effCat.Items[i];
                        fix.EffectID = newSpellID;
                        effCat.Items[i] = fix;
                        return;
                    }
                }

                // If there was no EffectIndex==1 row at all, append one.
                if (!hasIndex1)
                {
                    effCat.Items.Add(new Category2014Item
                    {
                        ItemID = newItemID,
                        EffectID = newSpellID,
                        EffectIndex = 1
                    });
                }
            }
        }
    }
}
