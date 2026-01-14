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

        // ------------------------------------------------------------
        // Result model (structured index you can persist in memory)
        // ------------------------------------------------------------
        public sealed class ItemVariantRecord
        {
            public string VariantName = "";  // tierTable[i].Suffix or "Original"
            public ushort ItemID;
            public bool IsPromotedBase;
            public bool IsOriginalCopy;
        }

        public sealed class ItemPromotionResult
        {
            public ushort BaseItemID;
            public ushort PromotedItemID;       // == BaseItemID after promotion
            public ushort OriginalCopyItemID;   // pristine copy, no suffix/scaling

            // tierTable order + "Original" last
            public List<ItemVariantRecord> Variants = new List<ItemVariantRecord>();
        }

        // ---------------------------------------------------------------------
        // Promote ONE equippable item:
        // Base becomes highest tier; create Perfect/Masterwork/Rare + Original copy
        // ---------------------------------------------------------------------
        public static SFGameDataNew PromoteItemToHighestTierAndCreateBackCopies(
            SFGameDataNew gd,
            ushort baseItemID,
            IReadOnlyList<ItemModifierStructure> itemTierTable, // LOW -> HIGH
            out ItemPromotionResult result
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (itemTierTable == null) throw new ArgumentNullException(nameof(itemTierTable));

            result = new ItemPromotionResult
            {
                BaseItemID = baseItemID,
                PromotedItemID = baseItemID,
                OriginalCopyItemID = 0
            };

            // 0 tiers => no-op
            if (itemTierTable.Count == 0)
                return gd;

            // Only equippable items (armor/weapon)
            if (!SharedHelperScripts.IsEquippableItem(gd, baseItemID))
                return gd; // batch-friendly: skip silently

            int highestIndex = itemTierTable.Count - 1;

            // Store ItemIDs for each tier in tierTable order:
            // - i < highestIndex: new IDs created by CreateItemVariant
            // - i == highestIndex: baseItemID after promotion
            var tierItemIDs = new ushort[itemTierTable.Count];
            tierItemIDs[highestIndex] = baseItemID;

            // 1) Create lower-tier variants off ORIGINAL base (unmodified)
            // Create in tier order so IDs map predictably to the supplied tier table.
            for (int i = 0; i < highestIndex; i++)
            {
                ushort beforeMax = GetMaxItemID(gd);
                CreateItemVariant(gd, baseItemID, itemTierTable[i]);
                tierItemIDs[i] = (ushort)(beforeMax + 1);
            }

            // 2) Clone ORIGINAL (no suffix, no scaling) into a new ItemID
            {
                ushort beforeMax = GetMaxItemID(gd);
                CloneItemAsOriginalCopy(gd, baseItemID);
                result.OriginalCopyItemID = (ushort)(beforeMax + 1);
            }

            // 3) Promote BASE IN PLACE to highest tier
            ApplyItemModifierInPlace(gd, baseItemID, itemTierTable[highestIndex]);

            // 4) Build results list in tierTable order + Original
            result.Variants.Clear();
            for (int i = 0; i < itemTierTable.Count; i++)
            {
                result.Variants.Add(new ItemVariantRecord
                {
                    VariantName = itemTierTable[i].Suffix ?? "",
                    ItemID = tierItemIDs[i],
                    IsPromotedBase = (i == highestIndex),
                    IsOriginalCopy = false
                });
            }

            result.Variants.Add(new ItemVariantRecord
            {
                VariantName = "Original",
                ItemID = result.OriginalCopyItemID,
                IsPromotedBase = false,
                IsOriginalCopy = true
            });

            return gd;
        }


        // ---------------------------------------------------------------------
        // Promote ALL equippable items (batch), skipping blacklisted and non-equippable
        // Captures base IDs upfront so the newly created variants are not re-processed.
        // ---------------------------------------------------------------------
        public static SFGameDataNew PromoteAllEquippableItems(
            SFGameDataNew gd,
            IReadOnlyList<ItemModifierStructure> itemTierTable, // LOW -> HIGH
            HashSet<ushort> itemBlackList,
            out Dictionary<ushort, ItemPromotionResult> promotionMap
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (itemTierTable == null) throw new ArgumentNullException(nameof(itemTierTable));
            itemBlackList ??= new HashSet<ushort>();

            promotionMap = new Dictionary<ushort, ItemPromotionResult>();

            // Snapshot base IDs (before adding variants)
            var baseItemIDs = gd.c2003.Items.Select(i => i.ItemID).ToList();

            foreach (var baseItemID in baseItemIDs)
            {
                if (itemBlackList.Contains(baseItemID))
                    continue;

                if (!SharedHelperScripts.IsEquippableItem(gd, baseItemID))
                    continue;

                // Optional guard: avoid re-running on already promoted item names (if you want)
                // If you already have such a guard, keep yours.

                try
                {
                    gd = PromoteItemToHighestTierAndCreateBackCopies(
                        gd,
                        baseItemID,
                        itemTierTable,
                        out var res
                    );

                    promotionMap[baseItemID] = res;
                }
                catch
                {
                    // skip failures in batch mode
                }
            }

            return gd;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------
        private static bool IsNameAlreadySuffixed(SFGameDataNew gd, ushort itemID, string suffix)
        {
            // Uses your existing helper (256 buffer). Good enough as a guard.
            var item = gd.c2003.Items.FirstOrDefault(x => x.ItemID == itemID);
            if (item.ItemID == 0) return false;

            string name = SharedHelperScripts.GetEnglishItemName(gd, item.NameID) ?? "";
            return name.IndexOf("[" + suffix + "]", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ushort PredictNextItemID(SFGameDataNew gd)
        {
            ushort max = 0;
            foreach (var it in gd.c2003.Items)
                if (it.ItemID > max) max = it.ItemID;
            return (ushort)(max + 1);
        }

        private static SFGameDataNew CreateItemVariant_ReturningNewID(
            SFGameDataNew gd,
            ushort baseItemID,
            ItemModifierStructure modifier,
            out ushort newItemID
        )
        {
            // CreateItemVariant allocates newItemID as (max c2003 ItemID + 1)
            newItemID = PredictNextItemID(gd);
            gd = CreateItemVariant(gd, baseItemID, modifier);

            // sanity: ensure it exists
            ushort createdID = newItemID;
            bool exists = gd.c2003.Items.Any(i => i.ItemID == createdID);
            if (!exists)
                throw new Exception($"CreateItemVariant did not create expected ItemID {newItemID}.");

            return gd;
        }

        private static ushort CloneItemAsOriginalCopy(SFGameDataNew gd, ushort baseItemID)
        {
            var itemCat = gd.c2003;
            var uiCat = gd.c2012; // CategoryBaseMultiple (Item UI)
            var effCat = gd.c2014; // CategoryBaseMultiple (Item effects / scroll link)
            var reqCat = gd.c2017; // CategoryBaseMultiple (Skill requirements)
            var armorCat = gd.c2004; // flat list (armor stats)
            var weapCat = gd.c2015; // flat list (weapon stats)

            // Find base item
            Category2003Item baseItem = default;
            bool found = false;
            for (int i = 0; i < itemCat.Items.Count; i++)
            {
                if (itemCat.Items[i].ItemID == baseItemID)
                {
                    baseItem = itemCat.Items[i];
                    found = true;
                    break;
                }
            }
            if (!found) throw new Exception($"Base item {baseItemID} not found.");

            // Allocate new ID (must be > max c2003)
            ushort newItemID = PredictNextItemID(gd);

            // Clone c2003 row (NO scaling, NO suffix)
            var newItem = baseItem;
            newItem.ItemID = newItemID;

            // ------------------------------
            // Clone UI block (c2012) safely
            // ------------------------------
            {
                int start = uiCat.Items.Count;
                bool any = false;

                foreach (var ui in uiCat.Items.ToArray())
                {
                    if (ui.ItemID != baseItemID)
                        continue;

                    if (!any)
                    {
                        uiCat.Indices.Add(start);
                        any = true;
                    }

                    var nu = ui;
                    nu.ItemID = newItemID;
                    uiCat.Items.Add(nu);
                }
            }

            // ------------------------------
            // Clone effects block (c2014) safely
            // ------------------------------
            {
                int start = effCat.Items.Count;
                bool any = false;

                foreach (var e in effCat.Items.ToArray())
                {
                    if (e.ItemID != baseItemID)
                        continue;

                    if (!any)
                    {
                        effCat.Indices.Add(start);
                        any = true;
                    }

                    var ne = e;
                    ne.ItemID = newItemID;
                    effCat.Items.Add(ne);
                }
            }

            // ------------------------------
            // Clone requirements block (c2017) safely
            // ------------------------------
            {
                int start = reqCat.Items.Count;
                bool any = false;

                foreach (var r in reqCat.Items.ToArray())
                {
                    if (r.ItemID != baseItemID)
                        continue;

                    if (!any)
                    {
                        reqCat.Indices.Add(start);
                        any = true;
                    }

                    var nr = r;
                    nr.ItemID = newItemID;
                    reqCat.Items.Add(nr);
                }
            }

            // ------------------------------
            // Clone armor stats (c2004) if present
            // (not CategoryBaseMultiple -> no indices)
            // ------------------------------
            foreach (var a in armorCat.Items.ToArray())
            {
                if (a.ItemID != baseItemID)
                    continue;

                var na = a;
                na.ItemID = newItemID;
                armorCat.Items.Add(na);
            }

            // ------------------------------
            // Clone weapon stats (c2015) if present
            // (not CategoryBaseMultiple -> no indices)
            // ------------------------------
            foreach (var w in weapCat.Items.ToArray())
            {
                if (w.ItemID != baseItemID)
                    continue;

                var nw = w;
                nw.ItemID = newItemID;
                weapCat.Items.Add(nw);
            }

            // Insert new item last (safe)
            itemCat.Items.Add(newItem);

            return newItemID;
        }

        private static SFGameDataNew PromoteBaseItemInPlace(SFGameDataNew gd, ushort baseItemID, ItemModifierStructure modifier)
        {
            var itemCat = gd.c2003;
            var locCat = gd.c2016;
            var armorCat = gd.c2004;
            var weapCat = gd.c2015;

            // Find base item index
            int idx = -1;
            for (int i = 0; i < itemCat.Items.Count; i++)
            {
                if (itemCat.Items[i].ItemID == baseItemID)
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) throw new Exception($"Base item {baseItemID} not found.");

            var baseItem = itemCat.Items[idx];

            // --- prices (in-place) ---
            baseItem.BuyValue = SharedHelperScripts.ScaleUInt(baseItem.BuyValue, modifier.BuyMod);
            baseItem.SellValue = SharedHelperScripts.ScaleUInt(baseItem.SellValue, modifier.SellMod);

            // --- name suffix (in-place, via new TextID clone) ---
            // Important: we clone to a NEW TextID so the original name remains for the original-copy item.
            ushort newNameTextID = SharedHelperScripts.CloneLocalisationTextID_512(
                locCat,
                baseItem.NameID,
                suffix: modifier.Suffix,
                appendSuffix: true
            );
            baseItem.NameID = newNameTextID;

            itemCat.Items[idx] = baseItem;

            // --- armor stats scaling (in-place) ---
            for (int a = 0; a < armorCat.Items.Count; a++)
            {
                if (armorCat.Items[a].ItemID != baseItemID)
                    continue;

                var ar = armorCat.Items[a];

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

                armorCat.Items[a] = ar;
            }

            // --- weapon stats scaling (in-place) ---
            for (int w = 0; w < weapCat.Items.Count; w++)
            {
                if (weapCat.Items[w].ItemID != baseItemID)
                    continue;

                var we = weapCat.Items[w];

                we.WeaponSpeed = SharedHelperScripts.ScaleUShort(we.WeaponSpeed, modifier.WeaponSpeedMod);
                we.MinDamage = SharedHelperScripts.ScaleUShort(we.MinDamage, modifier.MinDamageMod);
                we.MaxDamage = SharedHelperScripts.ScaleUShort(we.MaxDamage, modifier.MaxDamageMod);
                we.MaxRange = SharedHelperScripts.ScaleUShort(we.MaxRange, modifier.MaxRangeMod);

                weapCat.Items[w] = we;
            }

            return gd;
        }

        public static void CloneOrCreateC2014BlockForItem(SFGameDataNew gd, ushort baseItemID, ushort newItemID)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            var effCat = gd.c2014;

            // Clone ALL c2014 subitems belonging to baseItemID (if any)
            var clones = new List<Category2014Item>();

            foreach (var it in effCat.Items)
            {
                if (it.ItemID == baseItemID)
                {
                    var c = it;
                    c.ItemID = newItemID;
                    clones.Add(c);
                }
            }

            // If base had none, do nothing (important: don't create empty blocks)
            if (clones.Count == 0)
                return;

            effCat.Items.AddRange(clones);

            // Re-sort + rebuild Indices to preserve CategoryBaseMultiple invariants
            SharedHelperScripts.NormalizeCategoryMultiple(effCat);
        }

        // ------------------------------------------------------------
        // Public API: promote ALL equippable items with a tier table
        // ------------------------------------------------------------
        // tierTable is expected in HIGH -> LOW order (e.g. Legendary, Perfect, Masterwork, Rare)
        public static Dictionary<ushort, ItemPromotionResult> PromoteEquippableItemsWithTiers(
            SFGameDataNew gd,
            IReadOnlyList<ItemModifierStructure> tierTable,
            HashSet<ushort>? itemBlackList = null
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (tierTable == null) throw new ArgumentNullException(nameof(tierTable));
            if (tierTable.Count == 0)
                return new Dictionary<ushort, ItemPromotionResult>();

            itemBlackList ??= new HashSet<ushort>(); // placeholder, per your request

            // Equippable == has armor (c2004) or weapon (c2015) entry
            // This is exactly how we were already checking equippables elsewhere. 
            var equippableIDs = new HashSet<ushort>();
            foreach (var a in gd.c2004.Items) equippableIDs.Add(a.ItemID);
            foreach (var w in gd.c2015.Items) equippableIDs.Add(w.ItemID);

            var results = new Dictionary<ushort, ItemPromotionResult>();

            // Iterate on a stable snapshot; we will be appending items
            var baseIDs = equippableIDs.ToArray();
            foreach (var baseItemID in baseIDs)
            {
                if (itemBlackList.Contains(baseItemID))
                    continue;

                // Defensive: if base item is missing from c2003, skip
                if (!gd.c2003.Items.Any(i => i.ItemID == baseItemID))
                    continue;

                var res = PromoteSingleItemWithTiers(gd, baseItemID, tierTable);
                results[baseItemID] = res;
            }

            return results;
        }

        // ------------------------------------------------------------
        // Public API: promote ONE equippable item with a tier table
        // ------------------------------------------------------------
        // ------------------------------------------------------------
        // Public API: promote ONE equippable item with a tier table
        // tierTable is expected LOW -> HIGH (e.g. Rare, Masterwork, Perfect, Legendary)
        // ------------------------------------------------------------
        private static ItemPromotionResult PromoteSingleItemWithTiers(
            SFGameDataNew gd,
            ushort baseItemID,
            IReadOnlyList<ItemModifierStructure> itemTierTable
        )
        {
            var result = new ItemPromotionResult
            {
                BaseItemID = baseItemID,
                PromotedItemID = baseItemID,
                OriginalCopyItemID = 0
            };
            result.Variants.Clear();

            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (itemTierTable == null || itemTierTable.Count == 0)
                return result;

            if (!SharedHelperScripts.IsEquippableItem(gd, baseItemID))
                return result;

            int highestIndex = itemTierTable.Count - 1;
            var highestTier = itemTierTable[highestIndex];

            // 1) Create lower tiers (0..highest-1) as new items
            for (int i = 0; i < highestIndex; i++)
            {
                var tier = itemTierTable[i];

                ushort beforeMax = GetMaxItemID(gd);
                CreateItemVariant(gd, baseItemID, tier);
                ushort newID = (ushort)(beforeMax + 1);

                result.Variants.Add(new ItemVariantRecord
                {
                    VariantName = tier.Suffix ?? "",
                    ItemID = newID,
                    IsPromotedBase = false,
                    IsOriginalCopy = false
                });
            }

            // 2) Clone ORIGINAL (no suffix, no scaling)
            {
                ushort beforeMax = GetMaxItemID(gd);
                CloneItemAsOriginalCopy(gd, baseItemID);
                result.OriginalCopyItemID = (ushort)(beforeMax + 1);

                result.Variants.Add(new ItemVariantRecord
                {
                    VariantName = "Original",
                    ItemID = result.OriginalCopyItemID,
                    IsPromotedBase = false,
                    IsOriginalCopy = true
                });
            }

            // 3) Promote base IN PLACE to highest tier
            ApplyItemModifierInPlace(gd, baseItemID, highestTier);

            result.Variants.Add(new ItemVariantRecord
            {
                VariantName = highestTier.Suffix ?? "",
                ItemID = baseItemID,
                IsPromotedBase = true,
                IsOriginalCopy = false
            });

            return result;
        }



        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------
        private static ushort GetMaxItemID(SFGameDataNew gd)
        {
            ushort max = 0;
            foreach (var it in gd.c2003.Items)
                if (it.ItemID > max)
                    max = it.ItemID;
            return max;
        }

        private static void ApplyItemModifierInPlace(SFGameDataNew gd, ushort itemID, ItemModifierStructure modifier)
        {
            // c2003: prices + NameID
            for (int i = 0; i < gd.c2003.Items.Count; i++)
            {
                if (gd.c2003.Items[i].ItemID != itemID)
                    continue;

                var it = gd.c2003.Items[i];

                it.BuyValue = (uint)(it.BuyValue * modifier.BuyMod);
                it.SellValue = (uint)(it.SellValue * modifier.SellMod);

                // New localisation block with suffix (same pattern as CreateItemVariant). 
                it.NameID = SharedHelperScripts.CloneLocalisationTextID_512(
                    gd.c2016,
                    it.NameID,
                    modifier.Suffix,
                    appendSuffix: true
                );

                gd.c2003.Items[i] = it;
                break;
            }

            // c2004: armor stats (short fields) – scale IN PLACE
            for (int i = 0; i < gd.c2004.Items.Count; i++)
            {
                if (gd.c2004.Items[i].ItemID != itemID)
                    continue;

                var ar = gd.c2004.Items[i];

                ar.Armor = ScaleI16(ar.Armor, modifier.ArmorMod);

                ar.Strength = ScaleI16(ar.Strength, modifier.StrengthMod);
                ar.Stamina = ScaleI16(ar.Stamina, modifier.StaminaMod);
                ar.Agility = ScaleI16(ar.Agility, modifier.AgilityMod);
                ar.Dexterity = ScaleI16(ar.Dexterity, modifier.DexterityMod);
                ar.Charisma = ScaleI16(ar.Charisma, modifier.CharismaMod);
                ar.Intelligence = ScaleI16(ar.Intelligence, modifier.IntelligenceMod);
                ar.Wisdom = ScaleI16(ar.Wisdom, modifier.WisdomMod);

                ar.ResistFire = ScaleI16(ar.ResistFire, modifier.ResistancesMod);
                ar.ResistIce = ScaleI16(ar.ResistIce, modifier.ResistancesMod);
                ar.ResistMind = ScaleI16(ar.ResistMind, modifier.ResistancesMod);
                ar.ResistBlack = ScaleI16(ar.ResistBlack, modifier.ResistancesMod);

                ar.SpeedWalk = ScaleI16(ar.SpeedWalk, modifier.WalkMod);
                ar.SpeedFight = ScaleI16(ar.SpeedFight, modifier.FightMod);
                ar.SpeedCast = ScaleI16(ar.SpeedCast, modifier.CastMod);

                ar.Health = ScaleI16(ar.Health, modifier.HealthMod);
                ar.Mana = ScaleI16(ar.Mana, modifier.ManaMod);

                gd.c2004.Items[i] = ar;
                break;
            }

            // c2015: weapon stats – scale IN PLACE (these fields are ushort in your earlier implementation) 
            for (int i = 0; i < gd.c2015.Items.Count; i++)
            {
                if (gd.c2015.Items[i].ItemID != itemID)
                    continue;

                var wp = gd.c2015.Items[i];

                wp.MinDamage = ScaleU16(wp.MinDamage, modifier.MinDamageMod);
                wp.MaxDamage = ScaleU16(wp.MaxDamage, modifier.MaxDamageMod);
                wp.MaxRange = ScaleU16(wp.MaxRange, modifier.MaxRangeMod);
                wp.WeaponSpeed = ScaleU16(wp.WeaponSpeed, modifier.WeaponSpeedMod);

                gd.c2015.Items[i] = wp;
                break;
            }
        }

        private static short ScaleI16(short value, float mult)
        {
            // Keep sign; clamp to Int16 range
            int scaled = (int)MathF.Round(value * mult);
            if (scaled > short.MaxValue) scaled = short.MaxValue;
            if (scaled < short.MinValue) scaled = short.MinValue;
            return (short)scaled;
        }

        private static ushort ScaleU16(ushort value, float mult)
        {
            int scaled = (int)MathF.Round(value * mult);
            if (scaled > ushort.MaxValue) scaled = ushort.MaxValue;
            if (scaled < ushort.MinValue) scaled = ushort.MinValue;
            return (ushort)scaled;
        }
    }
}
