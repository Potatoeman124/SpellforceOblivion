using SFEngine.SFCFF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SpellforceDataEditor.OblivionScripts
{
    public static class VariantPipeline
    {
        // ------------------------------------------------------------
        // Units
        // ------------------------------------------------------------
        public static SFGameDataNew BuildUnitVariantsAndRegister(
            SFGameDataNew gd,
            IReadOnlyList<UnitVarianting.MobModifierStructure> mobTierTable,
            HashSet<ushort> unitBlacklist,
            VariantRegistry registry,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (mobTierTable == null) throw new ArgumentNullException(nameof(mobTierTable));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            unitBlacklist ??= new HashSet<ushort>();

            // 0 tiers => nothing to do (per your clarified rule)
            if (mobTierTable.Count == 0)
                return gd;

            // Snapshot base unit IDs BEFORE modifications
            var baseUnitIDs = gd.c2024.Items.Select(u => u.UnitID).ToList();
            int total = baseUnitIDs.Count;

            progress?.Report(new ProgressInfo
            {
                Phase = "Units",
                Detail = $"Preparing {total} units...",
                Current = 0,
                Total = total
            });

            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ushort baseUnitID = baseUnitIDs[i];

                // throttle UI updates
                if (i % ProgressInfo.ProgressUpdateInterval == 0)
                {
                    progress?.Report(new ProgressInfo
                    {
                        Phase = "Units",
                        Detail = $"Processing UnitID {baseUnitID} ({i}/{total})",
                        Current = i,
                        Total = total
                    });
                }

                if (unitBlacklist.Contains(baseUnitID))
                    continue;

                try
                {
                    // NEW: tier-count agnostic result
                    gd = UnitPromotion.PromoteUnitToHighestAndCreateBackCopies(
                        gd,
                        baseUnitID,
                        mobTierTable,
                        unitBlacklist,
                        out UnitPromotion.UnitPromotionResult promo
                    );

                    // skip if promotion did nothing (e.g. StatsID==0)
                    if (promo == null || promo.Variants == null || promo.Variants.Count == 0)
                        continue;

                    // Register
                    registry.Units[baseUnitID] = new VariantRegistry.UnitEntry
                    {
                        BaseUnitID = promo.BaseUnitID,
                        PromotedUnitID = promo.PromotedUnitID,
                        OriginalCopyUnitID = promo.OriginalCopyUnitID,
                        Variants = promo.Variants
                    };
                }
                catch
                {
                    // keep batch robust
                }
            }

            // final update
            progress?.Report(new ProgressInfo
            {
                Phase = "Units",
                Detail = $"Done ({total}/{total})",
                Current = total,
                Total = total
            });

            return gd;

        }


        // ------------------------------------------------------------
        // Items (equippable only)
        // ------------------------------------------------------------
        public static SFEngine.SFCFF.SFGameDataNew BuildItemVariantsAndRegister(
            SFEngine.SFCFF.SFGameDataNew gd,
            IReadOnlyList<ItemVarianting.ItemModifierStructure> itemTierTable,
            HashSet<ushort> itemBlackList,
            VariantRegistry registry,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (itemTierTable == null) throw new ArgumentNullException(nameof(itemTierTable));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            itemBlackList ??= new HashSet<ushort>();

            // 0 tiers => nothing to do
            if (itemTierTable.Count == 0)
            {
                progress?.Report(new ProgressInfo
                {
                    Phase = "Items: skipped",
                    Current = 0,
                    Total = 0,
                    Detail = "itemTierTable is empty"
                });
                return gd;
            }

            // Snapshot base ItemIDs BEFORE modifications
            var baseItemIDs = gd.c2003.Items.Select(i => i.ItemID).ToList();
            int total = baseItemIDs.Count;
            int interval = Math.Max(1, ProgressInfo.ProgressUpdateInterval);

            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ushort baseItemID = baseItemIDs[i];

                if (progress != null && (i % interval == 0 || i == total - 1))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Items: promoting & registering",
                        Current = i,
                        Total = total,
                        Detail = $"BaseItemID {baseItemID}"
                    });
                }

                if (itemBlackList.Contains(baseItemID))
                    continue;

                if (!SharedHelperScripts.IsEquippableItem(gd, baseItemID))
                    continue;

                try
                {
                    gd = ItemVarianting.PromoteItemToHighestTierAndCreateBackCopies(
                        gd, baseItemID, itemTierTable, out var res
                    );

                    if (res == null || res.Variants == null || res.Variants.Count == 0)
                        continue;

                    var entry = new VariantRegistry.ItemEntry
                    {
                        BaseItemID = baseItemID,
                        PromotedItemID = res.PromotedItemID,
                        OriginalCopyItemID = res.OriginalCopyItemID,
                        Variants = res.Variants
                    };

                    // Optional legacy convenience fields (only if other code still expects them)
                    foreach (var v in res.Variants)
                    {
                        if (string.Equals(v.VariantName, "Rare", StringComparison.OrdinalIgnoreCase)) entry.RareItemID = v.ItemID;
                        else if (string.Equals(v.VariantName, "Masterwork", StringComparison.OrdinalIgnoreCase)) entry.MasterworkItemID = v.ItemID;
                        else if (string.Equals(v.VariantName, "Perfect", StringComparison.OrdinalIgnoreCase)) entry.PerfectItemID = v.ItemID;
                    }

                    registry.Items[baseItemID] = entry;
                }
                catch
                {
                    // batch robustness: skip failures
                }
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Items: done",
                Current = total,
                Total = total,
                Detail = $"Processed {total} items"
            });

            return gd;

        }

        // ------------------------------------------------------------
        // Spells (scrollable only)
        // ------------------------------------------------------------
        public static SFGameDataNew BuildSpellVariantsAndRegister(
            SFGameDataNew gd,
            IReadOnlyList<SpellVarianting.SpellModifierStructure> spellTierTable,
            HashSet<ushort> spellLineBlackList,
            VariantRegistry registry,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (spellTierTable == null) throw new ArgumentNullException(nameof(spellTierTable));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            spellLineBlackList ??= new HashSet<ushort>();

            // If Count == 0 -> nothing to do (per your updated rule set)
            if (spellTierTable.Count == 0)
            {
                progress?.Report(new ProgressInfo
                {
                    Phase = "Spells: skipped",
                    Current = 0,
                    Total = 0,
                    Detail = "spellTierTable is empty"
                });
                return gd;
            }

            int interval = Math.Max(1, ProgressInfo.ProgressUpdateInterval);

            // Snapshot base SpellIDs that have scrolls (stable before modifications)
            var baseSpellIDs = SpellPromotion.GetAllSpellsWithScrolls(gd);

            // SpellID -> SpellLineID lookup (from the snapshot state)
            var spellLineBySpellID = new Dictionary<ushort, ushort>(gd.c2002.Items.Count);
            foreach (var s in gd.c2002.Items)
                spellLineBySpellID[s.SpellID] = s.SpellLineID;

            int total = baseSpellIDs.Count;
            int done = 0;

            foreach (var baseSpellID in baseSpellIDs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                done++;

                if (progress != null && (done % interval == 0 || done == total))
                {
                    // Keep this only if you still want the expensive consistency checks
                    SharedHelperScripts.RunCriticalChecks(gd, $"spells loop at {baseSpellID}");

                    progress.Report(new ProgressInfo
                    {
                        Phase = "Spells: promoting & registering",
                        Current = done,
                        Total = total,
                        Detail = $"BaseSpellID {baseSpellID}"
                    });
                }

                if (!spellLineBySpellID.TryGetValue(baseSpellID, out ushort lineId))
                    continue;

                // SpellLineID blacklist (as requested)
                if (spellLineBlackList.Contains(lineId))
                    continue;

                try
                {
                    gd = SpellPromotion.PromoteSpellWithScrollToHighestAndCreateBackCopies(
                        gd,
                        baseSpellID,
                        spellTierTable,
                        out var res
                    );

                    var entry = new VariantRegistry.SpellEntry
                    {
                        BaseSpellID = baseSpellID,
                        PromotedSpellID = res.PromotedSpellID,
                        BaseScrollItemID = res.BaseScrollItemID,
                        BaseSpellbookItemID = res.BaseSpellbookItemID
                    };

                    // Record all produced variants/copies (fully general)
                    if (res.Variants != null)
                    {
                        foreach (var v in res.Variants)
                        {
                            entry.Variants.Add(new VariantRegistry.SpellGrantVariantRecord
                            {
                                VariantName = v.VariantName ?? "",
                                SpellID = v.SpellID,
                                ScrollItemID = v.ScrollItemID,
                                SpellbookItemID = v.SpellbookItemID,
                                IsPromotedBase = v.IsPromotedBase,
                                IsOriginalCopy = v.IsOriginalCopy
                            });
                        }
                    }

                    registry.Spells[baseSpellID] = entry;
                }
                catch
                {
                    // skip failures
                }
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Spells: done",
                Current = total,
                Total = total,
                Detail = $"Processed {total} spells"
            });

            return gd;
        }

    }
}
