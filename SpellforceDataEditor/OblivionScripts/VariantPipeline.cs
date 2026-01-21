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

        /// <summary>
        /// After spells are promoted in-place (base == highest tier), mobs will also reference the promoted SpellIDs via c2026.
        /// This pass rewrites c2026.SpellID to the "original copy" spell for all affected spells, optionally leaving player units unchanged.
        /// </summary>
        public static SFGameDataNew DepromoteUnitSpellsToOriginalCopies(
            SFGameDataNew gd,
            VariantRegistry registry,
            bool depromotePlayerUnits,
            byte playerRaceMinInclusive = 0,
            byte playerRaceMaxInclusive = 6,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            // ------------------------------------------------------------
            // 1) Build lookup: any spell id in a chain -> original copy spell id
            // ------------------------------------------------------------
            var anyToOriginal = new Dictionary<ushort, ushort>();

            foreach (var kv in registry.Spells)
            {
                var entry = kv.Value;
                if (entry == null) continue;

                // Find original copy record
                ushort originalCopySpellID = 0;
                if (entry.Variants != null)
                {
                    for (int i = 0; i < entry.Variants.Count; i++)
                    {
                        var r = entry.Variants[i];
                        if (r != null && r.IsOriginalCopy && r.SpellID != 0)
                        {
                            originalCopySpellID = r.SpellID;
                            break;
                        }
                    }
                }

                if (originalCopySpellID == 0)
                    continue; // nothing we can do for this spell chain

                // Map base/promoted too (defensive)
                if (entry.BaseSpellID != 0) anyToOriginal[entry.BaseSpellID] = originalCopySpellID;
                if (entry.PromotedSpellID != 0) anyToOriginal[entry.PromotedSpellID] = originalCopySpellID;

                // Map all known tier variants
                if (entry.Variants != null)
                {
                    for (int i = 0; i < entry.Variants.Count; i++)
                    {
                        var r = entry.Variants[i];
                        if (r == null) continue;
                        if (r.SpellID == 0) continue;

                        anyToOriginal[r.SpellID] = originalCopySpellID;
                    }
                }
            }

            if (anyToOriginal.Count == 0)
            {
                progress?.Report(new ProgressInfo
                {
                    Phase = "Depromote unit spells",
                    Detail = "No spell chains with original copies found in registry; nothing to do.",
                    Current = 1,
                    Total = 1
                });
                return gd;
            }

            // ------------------------------------------------------------
            // 2) Build player unit set (if needed)
            //     UnitID -> StatsID via c2024, StatsID -> UnitRace via c2005
            // ------------------------------------------------------------
            HashSet<ushort>? playerUnits = null;

            if (!depromotePlayerUnits)
            {
                var statsRace = new Dictionary<ushort, byte>();
                foreach (var s in gd.c2005.Items)
                    statsRace[s.StatsID] = s.UnitRace;

                playerUnits = new HashSet<ushort>();
                foreach (var u in gd.c2024.Items)
                {
                    if (u.StatsID == 0) continue; // treat unknown/dummy as non-player for this purpose
                    if (!statsRace.TryGetValue(u.StatsID, out byte race)) continue;

                    if (race >= playerRaceMinInclusive && race <= playerRaceMaxInclusive)
                        playerUnits.Add(u.UnitID);
                }
            }

            // ------------------------------------------------------------
            // 3) Rewrite c2026 spell ids
            // ------------------------------------------------------------
            int total = gd.c2026.Items.Count;
            int changed = 0;

            for (int i = 0; i < gd.c2026.Items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (progress != null && (i % ProgressInfo.ProgressUpdateInterval == 0))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Depromote unit spells",
                        Detail = $"Processing c2026 {i}/{total} (changed: {changed})",
                        Current = i,
                        Total = total
                    });
                }

                var row = gd.c2026.Items[i];

                // Skip player units unless explicitly requested
                if (playerUnits != null && playerUnits.Contains(row.UnitID))
                    continue;

                if (row.SpellID == 0)
                    continue;

                if (anyToOriginal.TryGetValue(row.SpellID, out ushort orig) && orig != 0 && orig != row.SpellID)
                {
                    row.SpellID = orig;
                    gd.c2026.Items[i] = row;
                    changed++;
                }
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Depromote unit spells",
                Detail = $"Done. Changed rows: {changed} / {total}",
                Current = total,
                Total = total
            });

            return gd;
        }


        // -----------------------------
        // PATCHED DepromoteUnitSpellsToOriginalCopies
        // -----------------------------
        public static SFGameDataNew DepromoteUnitSpellsToOriginalCopies(
            SFGameDataNew gd,
            VariantRegistry registry,
            bool depromotePlayerUnits,
            bool depromoteSummonedUnits,
            HashSet<ushort> blacklistSummonables,
            byte playerRaceMinInclusive = 0,
            byte playerRaceMaxInclusive = 6,
            IProgress<ProgressInfo> progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            progress?.Report(new ProgressInfo { Phase = "Depromote unit spells", Current = 0, Total = 1 });

            // 1) Build maps from registry:
            // - anySpellID -> originalCopySpellID (existing behavior)
            // - anySpellID -> SpellChain (new, for suffix-based resolution)
            var anyToOriginalCopy = new Dictionary<ushort, ushort>();
            var anyToChain = new Dictionary<ushort, SpellChain>();
            var knownSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in registry.Spells)
            {
                var entry = kv.Value;
                if (entry == null || entry.Variants == null || entry.Variants.Count == 0)
                    continue;

                var originalCopy = entry.Variants.FirstOrDefault(v => v.IsOriginalCopy);
                if (originalCopy.SpellID == 0)
                    continue;

                var chain = new SpellChain(originalCopy.SpellID);

                // Register all known IDs into maps + capture suffix variants
                foreach (var v in entry.Variants)
                {
                    RegisterSpellId(v.SpellID, chain, anyToOriginalCopy, anyToChain);

                    if (!v.IsOriginalCopy && !string.IsNullOrWhiteSpace(v.VariantName))
                    {
                        chain.SpellBySuffix[v.VariantName] = v.SpellID;
                        knownSuffixes.Add(v.VariantName);
                    }
                }
            }

            // Build suffix list sorted by length desc (avoid partial matches)
            var suffixesSortedDesc = knownSuffixes
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .OrderByDescending(s => s.Length)
                .ToList();

            // 2) If we're NOT depromoting summoned units, we still want tier-matching for summons.
            // Build UnitID -> suffix map for all summon tiers.
            Dictionary<ushort, string> summonedSuffixByUnitId = null;
            if (!depromoteSummonedUnits && blacklistSummonables != null && blacklistSummonables.Count > 0 && suffixesSortedDesc.Count > 0)
            {
                summonedSuffixByUnitId = BuildSummonedUnitSuffixByUnitId(gd, blacklistSummonables, suffixesSortedDesc);
            }

            // 3) Player unit blacklist (existing behavior)
            HashSet<ushort> playerUnits = null;
            if (!depromotePlayerUnits)
            {
                playerUnits = VariantBlacklists.BuildUnitIDBlacklist_ByRaceRange(gd, playerRaceMinInclusive, playerRaceMaxInclusive);
            }

            // 4) Rewrite c2026
            int changed = 0;
            for (int i = 0; i < gd.c2026.Items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var unit = gd.c2026.Items[i];

                if (unit.UnitID == 0 || unit.SpellID == 0)
                    continue;

                // keep existing player-unit exclusion behavior
                if (playerUnits != null && playerUnits.Contains(unit.UnitID))
                    continue;

                // NEW: summonables tier-match when depromoteSummonedUnits == false
                if (summonedSuffixByUnitId != null && summonedSuffixByUnitId.TryGetValue(unit.UnitID, out string unitSuffix))
                {
                    if (anyToChain.TryGetValue(unit.SpellID, out SpellChain chain))
                    {
                        ushort desired = chain.ResolveForSuffix(unitSuffix);
                        if (desired != 0 && desired != unit.SpellID)
                        {
                            unit.SpellID = desired;
                            gd.c2026.Items[i] = unit;
                            changed++;
                        }
                        continue; // done for summonables
                    }

                    // If the spell isn't in registry at all, leave it unchanged.
                    continue;
                }

                // Existing behavior: depromote to original copy
                if (anyToOriginalCopy.TryGetValue(unit.SpellID, out ushort originalCopyId) && originalCopyId != 0 && originalCopyId != unit.SpellID)
                {
                    unit.SpellID = originalCopyId;
                    gd.c2026.Items[i] = unit;
                    changed++;
                }
            }

            progress?.Report(new ProgressInfo { Phase = "Depromote unit spells", Current = 1, Total = 1});
            return gd;
        }

        // -----------------------------
        // NEW helper: per-chain resolution
        // -----------------------------
        private sealed class SpellChain
        {
            public ushort OriginalCopySpellID { get; }
            public Dictionary<string, ushort> SpellBySuffix { get; } =
                new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

            public SpellChain(ushort originalCopySpellId)
            {
                OriginalCopySpellID = originalCopySpellId;
            }

            public ushort ResolveForSuffix(string suffix)
            {
                if (string.IsNullOrWhiteSpace(suffix))
                    return OriginalCopySpellID;

                return SpellBySuffix.TryGetValue(suffix, out ushort v) ? v : OriginalCopySpellID;
            }
        }

        private static void RegisterSpellId(
            ushort spellID,
            SpellChain chain,
            Dictionary<ushort, ushort> anyToOriginalCopy,
            Dictionary<ushort, SpellChain> anyToChain
        )
        {
            if (spellID == 0) return;
            anyToOriginalCopy[spellID] = chain.OriginalCopySpellID;
            anyToChain[spellID] = chain;
        }

        // NEW helper: build English TextID->string map once (fast)
        private static Dictionary<ushort, string> BuildLocalisationMap_English(SFGameDataNew gd, ushort englishLanguageId = 1)
        {
            var map = new Dictionary<ushort, string>();
            foreach (var loc in gd.c2016.Items)
            {
                if (loc.LanguageID != englishLanguageId)
                    continue;

                var copy = loc;
                // Same pattern as used elsewhere in your codebase for c2016 reading.
                string text = SharedHelperScripts.ReadContent256(ref copy);
                map[loc.TextID] = text ?? "";
            }
            return map;
        }

        // NEW helper: strip suffix tokens in " [Suffix]" format
        private static bool TryStripKnownSuffix(string name, IReadOnlyList<string> suffixesSortedDesc, out string baseName, out string suffix)
        {
            baseName = (name ?? "").Trim();
            suffix = "";

            if (string.IsNullOrWhiteSpace(baseName))
                return false;

            foreach (var s in suffixesSortedDesc)
            {
                if (string.IsNullOrWhiteSpace(s))
                    continue;

                string tag = " [" + s + "]";
                if (baseName.EndsWith(tag, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName.Substring(0, baseName.Length - tag.Length).Trim();
                    suffix = s;
                    return true;
                }
            }

            return false;
        }

        // NEW helper:
        // 1) take the seed UnitIDs from blacklistSummonables (usually base summons)
        // 2) derive their base names (strip suffix if present)
        // 3) scan c2024 and mark ANY unit whose baseName matches -> that unit is a summon tier, suffix extracted
        private static Dictionary<ushort, string> BuildSummonedUnitSuffixByUnitId(
            SFGameDataNew gd,
            HashSet<ushort> blacklistSummonables,
            IReadOnlyList<string> knownSuffixesSortedDesc
        )
        {
            var text = BuildLocalisationMap_English(gd, englishLanguageId: 1);

            // Map UnitID -> NameID for fast seed lookup
            var nameIdByUnitId = new Dictionary<ushort, ushort>();
            foreach (var u in gd.c2024.Items)
                nameIdByUnitId[u.UnitID] = u.NameID;

            // Base summon names derived from the seed list
            var summonBaseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var unitId in blacklistSummonables)
            {
                if (!nameIdByUnitId.TryGetValue(unitId, out ushort nameId))
                    continue;

                if (!text.TryGetValue(nameId, out string unitName))
                    continue;

                if (TryStripKnownSuffix(unitName, knownSuffixesSortedDesc, out string baseName, out _))
                    summonBaseNames.Add(baseName);
                else
                    summonBaseNames.Add((unitName ?? "").Trim());
            }

            // Now mark all tiers (base + [Superior] + [Arch]...) by matching base name
            var result = new Dictionary<ushort, string>();
            foreach (var u in gd.c2024.Items)
            {
                if (!text.TryGetValue(u.NameID, out string unitName))
                    continue;

                string baseName, suffix;
                if (!TryStripKnownSuffix(unitName, knownSuffixesSortedDesc, out baseName, out suffix))
                {
                    baseName = (unitName ?? "").Trim();
                    suffix = "";
                }

                if (summonBaseNames.Contains(baseName))
                    result[u.UnitID] = suffix; // "" means Tier1/original
            }

            return result;
        }

    }
}
