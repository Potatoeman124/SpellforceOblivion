using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SpellforceDataEditor.OblivionScripts.SpellVarianting;

namespace SpellforceDataEditor.OblivionScripts
{
    internal class SpellPromotion
    {
        public sealed class SpellPromotionResult
        {
            public ushort BaseSpellID;
            public ushort PromotedSpellID; // == BaseSpellID when promoted

            public ushort BaseScrollItemID;
            public ushort BaseSpellbookItemID;

            // Automatically generated log of all tiers/copies produced by this operation.
            // Includes:
            // - any created lower-tier variants (new IDs)
            // - the promoted base (highest tier) (base IDs)
            // - the original copy ("Original") (new IDs)
            public List<SpellGrantVariantEntry> Variants = new List<SpellGrantVariantEntry>();
        }

        public sealed class SpellGrantVariantEntry
        {
            public string VariantName = "";
            public ushort SpellID;
            public ushort ScrollItemID;
            public ushort SpellbookItemID;

            // Optional but useful for debugging / downstream replacement logic
            public bool IsPromotedBase;     // true for the in-place promoted base
            public bool IsOriginalCopy;     // true for the no-suffix clone
        }

        // ------------------------------------------------------------
        // Entry point: promote ONE spell (must have scroll chain)
        // tier order: [Empowered, Superior, Perfected, Highest]
        // ------------------------------------------------------------
        public static SFGameDataNew PromoteSpellWithScrollToHighestAndCreateBackCopies(
            SFGameDataNew gd,
            ushort baseSpellID,
            IReadOnlyList<SpellModifierStructure> spellTierTable,
            out SpellPromotionResult result
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (spellTierTable == null) throw new ArgumentNullException(nameof(spellTierTable));

            result = new SpellPromotionResult
            {
                BaseSpellID = baseSpellID,
                PromotedSpellID = baseSpellID
            };

            // Must exist for "scrollable spells" pipeline
            ResolveSpellScrollChain(gd, baseSpellID, out ushort baseSpellbookItemID, out ushort baseScrollItemID);
            result.BaseScrollItemID = baseScrollItemID;
            result.BaseSpellbookItemID = baseSpellbookItemID;

            // RULE: if Count == 0 -> nothing to do
            if (spellTierTable.Count == 0)
                return gd;

            // Highest is always last
            var highest = spellTierTable[spellTierTable.Count - 1];

            // ------------------------------------------------------------
            // 1) Create lower-tier variants (new spell + new items)
            //    tiers: 0..Count-2 (may be empty if Count==1)
            // ------------------------------------------------------------
            for (int i = 0; i < spellTierTable.Count - 1; i++)
            {
                var tier = spellTierTable[i];

                gd = SpellVarianting.CreateSpellVariantAndGrantItems(
                    gd, baseSpellID, tier,
                    out ushort newSpell, out ushort newScroll, out ushort newBook
                );

                result.Variants.Add(new SpellGrantVariantEntry
                {
                    VariantName = tier.Suffix ?? "",
                    SpellID = newSpell,
                    ScrollItemID = newScroll,
                    SpellbookItemID = newBook,
                    IsPromotedBase = false,
                    IsOriginalCopy = false
                });
            }

            // ------------------------------------------------------------
            // 2) Create "Original copy" (new spell + new items) WITHOUT suffix
            // ------------------------------------------------------------
            gd = CreateOriginalCopySpellAndGrantItems(
                gd,
                baseSpellID,
                baseSpellbookItemID,
                baseScrollItemID,
                out ushort origSpell,
                out ushort origScroll,
                out ushort origBook
            );

            result.Variants.Add(new SpellGrantVariantEntry
            {
                VariantName = "Original",
                SpellID = origSpell,
                ScrollItemID = origScroll,
                SpellbookItemID = origBook,
                IsPromotedBase = false,
                IsOriginalCopy = true
            });

            // ------------------------------------------------------------
            // 3) Promote base spell in-place to highest tier
            // ------------------------------------------------------------
            gd = PromoteSpellInPlace_FromGeneratedVariant(gd, baseSpellID, highest);

            // ------------------------------------------------------------
            // 4) Promote base scroll + spellbook items in-place (suffix + prices)
            // ------------------------------------------------------------
            gd = PromoteSpellGrantItemsInPlace(gd, baseScrollItemID, baseSpellbookItemID, highest);

            // Log the promoted base as the highest tier entry
            result.Variants.Add(new SpellGrantVariantEntry
            {
                VariantName = highest.Suffix ?? "",
                SpellID = baseSpellID,
                ScrollItemID = baseScrollItemID,
                SpellbookItemID = baseSpellbookItemID,
                IsPromotedBase = true,
                IsOriginalCopy = false
            });

            return gd;
        }


        // ------------------------------------------------------------
        // Batch: promote all spells that have scrolls (snapshot first)
        // ------------------------------------------------------------
        public static SFGameDataNew PromoteAllSpellsWithScrolls(
            SFGameDataNew gd,
            IReadOnlyList<SpellModifierStructure> spellTierTable,
            out Dictionary<ushort, SpellPromotionResult> map
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (spellTierTable == null) throw new ArgumentNullException(nameof(spellTierTable));

            map = new Dictionary<ushort, SpellPromotionResult>();

            var spellIDs = GetAllSpellsWithScrolls(gd);

            foreach (var baseSpellID in spellIDs)
            {
                try
                {
                    gd = PromoteSpellWithScrollToHighestAndCreateBackCopies(gd, baseSpellID, spellTierTable, out var res);
                    map[baseSpellID] = res;
                }
                catch
                {
                    // skip failures in batch; add logging if desired
                }
            }

            return gd;
        }

        // ============================================================
        // Core helpers
        // ============================================================

        public static List<ushort> GetAllSpellsWithScrolls(SFGameDataNew gd)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            // Spell is "scrollable" iff:
            // c2018 has EffectID==spell, and c2013 has InstalledScrollItemID==c2018.SpellItemID
            var bookSpellItems = new Dictionary<ushort, ushort>(); // spellID -> spellbookItemID
            foreach (var sb in gd.c2018.Items)
            {
                if (sb.EffectID == 0 || sb.SpellItemID == 0) continue;
                if (!bookSpellItems.ContainsKey(sb.EffectID))
                    bookSpellItems[sb.EffectID] = sb.SpellItemID;
            }

            var installedToScroll = new HashSet<ushort>(); // spellbookItemID that has an inventory scroll
            foreach (var link in gd.c2013.Items)
            {
                if (link.InstalledScrollItemID != 0)
                    installedToScroll.Add(link.InstalledScrollItemID);
            }

            var res = new List<ushort>();
            foreach (var kv in bookSpellItems)
            {
                if (installedToScroll.Contains(kv.Value))
                    res.Add(kv.Key);
            }

            return res;
        }

        public static void ResolveSpellScrollChain(
            SFGameDataNew gd,
            ushort spellID,
            out ushort baseSpellbookItemID,
            out ushort baseScrollItemID
        )
        {
            // 1) spellbook item via c2018
            baseSpellbookItemID = 0;
            foreach (var sb in gd.c2018.Items)
            {
                if (sb.EffectID == spellID && sb.SpellItemID != 0)
                {
                    baseSpellbookItemID = sb.SpellItemID;
                    break;
                }
            }
            if (baseSpellbookItemID == 0)
                throw new Exception($"No c2018 spellbook entry found for spell {spellID}.");

            // 2) inventory scroll via c2013 (InstalledScrollItemID -> ItemID)
            baseScrollItemID = 0;
            foreach (var link in gd.c2013.Items)
            {
                if (link.InstalledScrollItemID == baseSpellbookItemID && link.ItemID != 0)
                {
                    baseScrollItemID = link.ItemID;
                    break;
                }
            }
            if (baseScrollItemID == 0)
                throw new Exception($"No c2013 inventory scroll link found for spellbook item {baseSpellbookItemID} (spell {spellID}).");
        }

        // ------------------------------------------------------------
        // Promote base spell in-place by generating a variant and copying
        // ------------------------------------------------------------
        public static SFGameDataNew PromoteSpellInPlace_FromGeneratedVariant(
            SFGameDataNew gd,
            ushort baseSpellID,
            SpellModifierStructure highest
        )
        {
            // 1) Generate a highest-tier variant (creates a NEW spell row in c2002)
            gd = SpellVarianting.CreateSpellVariant(gd, baseSpellID, highest, out ushort generatedSpellID);

            // 2) Locate both rows
            int baseIdx = FindSpellIndex(gd.c2002, baseSpellID);
            int genIdx = FindSpellIndex(gd.c2002, generatedSpellID);

            if (baseIdx < 0) throw new Exception($"Base spell {baseSpellID} not found in c2002.");
            if (genIdx < 0) throw new Exception($"Generated spell {generatedSpellID} not found in c2002.");

            var baseSpell = gd.c2002.Items[baseIdx];
            var genSpell = gd.c2002.Items[genIdx];

            // 3) Overwrite base with generated stats
            // IMPORTANT:
            // - Keep SpellID as baseSpellID (so references stay intact)
            // - DO NOT restore SpellTypeID/TypeID (your UI's "Spell type ID"), so base shows [Arch]
            // - If your struct has SpellLineID separate from TypeID, keep whatever your tool calls "Spell type ID".
            //
            // In your editor UI, "Spell type ID" is what needs to become the Arch one.
            // So: do NOT preserve baseSpell.SpellTypeID (or equivalent).
            baseSpell = genSpell;
            baseSpell.SpellID = baseSpellID;

            gd.c2002.Items[baseIdx] = baseSpell;

            // 4) Remove the temporary generated spell row from c2002 so you don't get an extra "[Arch]" entry.
            // Remove by ID (do NOT rely on genIdx after modifications).
            for (int i = gd.c2002.Items.Count - 1; i >= 0; i--)
            {
                if (gd.c2002.Items[i].SpellID == generatedSpellID)
                {
                    gd.c2002.Items.RemoveAt(i);
                    break;
                }
            }

            return gd;
        }

        // ------------------------------------------------------------
        // Promote base scroll/spellbook items in-place: suffix + prices
        // ------------------------------------------------------------
        public static SFGameDataNew PromoteSpellGrantItemsInPlace(
            SFGameDataNew gd,
            ushort baseScrollItemID,
            ushort baseSpellbookItemID,
            SpellModifierStructure highest
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (highest == null) throw new ArgumentNullException(nameof(highest));

            PromoteOneItemInPlace_NameAndPrice(gd, baseScrollItemID, highest.Suffix, highest.BuyPriceMult, highest.SellPriceMult);
            PromoteOneItemInPlace_NameAndPrice(gd, baseSpellbookItemID, highest.Suffix, highest.BuyPriceMult, highest.SellPriceMult);

            return gd;
        }

        private static void PromoteOneItemInPlace_NameAndPrice(
            SFGameDataNew gd,
            ushort itemID,
            string suffix,
            float buyMult,
            float sellMult
        )
        {
            var itemCat = gd.c2003;
            var locCat = gd.c2016;

            int idx = -1;
            for (int i = 0; i < itemCat.Items.Count; i++)
            {
                if (itemCat.Items[i].ItemID == itemID) { idx = i; break; }
            }
            if (idx < 0) throw new Exception($"Item {itemID} not found in c2003.");

            var it = itemCat.Items[idx];

            // prices
            it.BuyValue = SharedHelperScripts.ScaleUInt(it.BuyValue, buyMult);
            it.SellValue = SharedHelperScripts.ScaleUInt(it.SellValue, sellMult);

            // name suffix (clone localisation)
            if (!string.IsNullOrWhiteSpace(suffix))
            {
                ushort newNameID = SharedHelperScripts.CloneLocalisationTextID_512(locCat, it.NameID, suffix, appendSuffix: true);
                it.NameID = newNameID;
            }

            itemCat.Items[idx] = it;
        }

        // ------------------------------------------------------------
        // Create "original copy" spell + new scroll/spellbook items w/o suffix
        // ------------------------------------------------------------
        public static SFGameDataNew CreateOriginalCopySpellAndGrantItems(
            SFGameDataNew gd,
            ushort baseSpellID,
            ushort baseSpellbookItemID,
            ushort baseScrollItemID,
            out ushort newSpellID,
            out ushort newScrollItemID,
            out ushort newSpellbookItemID
        )
        {
            // 1) clone c2002 spell row exactly
            newSpellID = AllocateNewSpellID(gd);

            int baseIdx = FindSpellIndex(gd.c2002, baseSpellID);
            if (baseIdx < 0) throw new Exception($"Base spell {baseSpellID} not found in c2002.");

            var baseSpell = gd.c2002.Items[baseIdx];
            var newSpell = baseSpell;
            newSpell.SpellID = newSpellID;

            gd.c2002.Items.Add(newSpell);

            // 2) clone scroll + spellbook items with NO suffix, and repoint links to newSpellID
            CloneSpellScrollAndSpellbook_NoSuffix(
                gd,
                baseSpellID,
                baseScrollItemID,
                baseSpellbookItemID,
                newSpellID,
                out newScrollItemID,
                out newSpellbookItemID
            );

            return gd;
        }

        private static void CloneSpellScrollAndSpellbook_NoSuffix(
            SFGameDataNew gd,
            ushort baseSpellID,
            ushort baseScrollItemID,
            ushort baseSpellbookItemID,
            ushort newSpellID,
            out ushort newScrollItemID,
            out ushort newSpellbookItemID
        )
        {
            var itemCat = gd.c2003;
            var uiCat = gd.c2012; // multiple
            var linkCat = gd.c2013; // multiple
            var effCat = gd.c2014; // multiple
            var reqCat = gd.c2017; // multiple
            var sbCat = gd.c2018; // multiple

            // Allocate IDs safely (global max across item-related categories)
            ushort maxItemID = SharedHelperScripts.GetMaxItemID_Global(gd);
            newScrollItemID = (ushort)(maxItemID + 1);
            newSpellbookItemID = (ushort)(maxItemID + 2);

            // --- clone c2003 rows (NO suffix, NO price scaling) ---
            var baseScroll = itemCat.Items.First(x => x.ItemID == baseScrollItemID);
            var baseBook = itemCat.Items.First(x => x.ItemID == baseSpellbookItemID);

            var newScroll = baseScroll; newScroll.ItemID = newScrollItemID;
            var newBook = baseBook; newBook.ItemID = newSpellbookItemID;

            // IMPORTANT: keep NameID unchanged (no suffix)
            itemCat.Items.Add(newScroll);
            itemCat.Items.Add(newBook);

            // --- clone c2012 blocks safely ---
            CloneItemBlock_Multiple(uiCat, baseScrollItemID, newScrollItemID, x => x.ItemID, (x, id) => x.ItemID = id);
            CloneItemBlock_Multiple(uiCat, baseSpellbookItemID, newSpellbookItemID, x => x.ItemID, (x, id) => x.ItemID = id);

            // --- clone c2014 blocks safely and repoint spell effect for scroll item ---
            ItemVarianting.CloneOrCreateC2014BlockForItem(effCat, baseScrollItemID, newScrollItemID, baseSpellID, newSpellID);
            ItemVarianting.CloneOrCreateC2014BlockForItem(effCat, baseSpellbookItemID, newSpellbookItemID, baseSpellID, newSpellID);

            // --- clone requirements for spellbook item ---
            CloneItemBlock_Multiple(reqCat, baseSpellbookItemID, newSpellbookItemID, x => x.ItemID, (x, id) => x.ItemID = id);

            // --- create c2013 link (new scroll -> new spellbook item) safely ---
            AddC2013Link(linkCat, newScrollItemID, newSpellbookItemID);

            // --- create/clone c2018 link for spellbook item -> newSpellID safely ---
            CloneOrCreateC2018Link(sbCat, baseSpellID, baseSpellbookItemID, newSpellID, newSpellbookItemID);
        }

        // Generic safe cloning for CategoryBaseMultiple-style blocks keyed by ItemID-like field
        private static void CloneItemBlock_Multiple<T>(
            CategoryBaseMultiple<T> cat,
            ushort baseID,
            ushort newID,
            Func<T, ushort> getID,
            Action<T, ushort> setID
        )
            where T : struct, ICategorySubItem
        {
            var clones = new List<T>();

            foreach (var it in cat.Items)
            {
                if (getID(it) == baseID)
                {
                    var c = it;
                    setID(c, newID);
                    clones.Add(c);
                }
            }

            if (clones.Count == 0)
                return;

            // Do NOT touch Indices manually; rebuild after append.
            cat.Items.AddRange(clones);
            SharedHelperScripts.NormalizeCategoryMultiple(cat);
        }

        private static void AddC2013Link(Category2013 linkCat, ushort scrollItemID, ushort spellbookItemID)
        {
            linkCat.Items.Add(new Category2013Item
            {
                ItemID = scrollItemID,
                InstalledScrollItemID = spellbookItemID
            });

            SharedHelperScripts.NormalizeCategorySingle(linkCat);
        }

        private static void CloneOrCreateC2018Link(
            Category2018 sbCat,
            ushort baseSpellID,
            ushort baseSpellbookItemID,
            ushort newSpellID,
            ushort newSpellbookItemID
        )
        {
            // Find a base row to clone if possible (EffectID==baseSpellID and SpellItemID==baseSpellbookItemID)
            Category2018Item? baseRow = null;
            foreach (var r in sbCat.Items)
            {
                if (r.EffectID == baseSpellID && r.SpellItemID == baseSpellbookItemID)
                {
                    baseRow = r;
                    break;
                }
            }

            // Category2018 is CategoryBaseSingle -> NO Indices. Just append item.
            if (baseRow.HasValue)
            {
                var nr = baseRow.Value;
                nr.EffectID = newSpellID;
                nr.SpellItemID = newSpellbookItemID;
                sbCat.Items.Add(nr);
            }
            else
            {
                sbCat.Items.Add(new Category2018Item
                {
                    EffectID = newSpellID,
                    SpellItemID = newSpellbookItemID
                });
            }

            // Keep CategoryBaseSingle invariant: Items sorted by GetID()
            // (this is what the base category expects for binary search).
            SharedHelperScripts.NormalizeCategorySingle(sbCat);
        }

        private static int FindSpellIndex(Category2002 spellCat, ushort spellID)
        {
            for (int i = 0; i < spellCat.Items.Count; i++)
                if (spellCat.Items[i].SpellID == spellID) return i;
            return -1;
        }

        private static ushort AllocateNewSpellID(SFGameDataNew gd)
        {
            ushort max = 0;
            foreach (var s in gd.c2002.Items)
                if (s.SpellID > max) max = s.SpellID;
            return (ushort)(max + 1);
        }



    }
}
