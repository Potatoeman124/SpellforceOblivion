using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using SpellforceDataEditor.OblivionScripts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpellforceDataEditor.OblivionScripts
{
    internal class VariantBlacklists
    {
        /// <summary>
        /// Units whose Stats.RaceID is in [minRaceInclusive..maxRaceInclusive].
        /// This is intended to exclude player units (race 0..6).
        /// </summary>
        public static HashSet<ushort> BuildUnitIDBlacklist_ByRaceRange(
            SFGameDataNew gd,
            byte minRaceInclusive = 0,
            byte maxRaceInclusive = 6
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            // Map StatsID -> RaceID
            var statsRace = new Dictionary<ushort, byte>();
            foreach (var s in gd.c2005.Items)
                statsRace[s.StatsID] = s.UnitRace;

            var result = new HashSet<ushort>();

            foreach (var u in gd.c2024.Items)
            {
                // NEW: blacklist units with StatsID==0
                if (u.StatsID == 0)
                {
                    result.Add(u.UnitID);
                    continue;
                }

                if (!statsRace.TryGetValue(u.StatsID, out byte race))
                    continue;

                if (race >= minRaceInclusive && race <= maxRaceInclusive)
                    result.Add(u.UnitID);
            }

            return result;
        }

        /// <summary>
        /// Summonable units blacklist using the requested multi-layer approach:
        /// 1) Find spell line IDs in c2054 whose linked text contains 'summon'
        /// 2) Find spells in c2002 that use those spell line IDs
        /// 3) Extract UnitID params from those spells via descriptors
        /// Optionally also follows sub-effect chains to find nested summons.
        /// </summary>
        public static HashSet<ushort> BuildUnitIDBlacklist_SummonableBySpellText(
            SFGameDataNew gd,
            string needle = "summon",
            bool followSubEffects = true
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (needle == null) needle = "";

            // 1) spell line IDs (c2054.SpellLineID) where text contains needle
            var summonLineIds = FindSpellLineIDsByTextNeedle(gd, needle);

            // 2) spells using those line IDs
            var result = new HashSet<ushort>();
            var visitedSpells = new HashSet<ushort>();

            foreach (var sp in gd.c2002.Items)
            {
                if (!summonLineIds.Contains(sp.SpellLineID))
                    continue;

                CollectSummonedUnitIDsFromSpell(gd, sp.SpellID, result, visitedSpells, followSubEffects);
            }

            return result;
        }

        /// <summary>
        /// Finds SpellLineIDs in c2054 where TextID or DescriptionID contains needle (case-insensitive).
        /// </summary>
        public static HashSet<ushort> FindSpellLineIDsByTextNeedle(SFGameDataNew gd, string needle)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (needle == null) needle = "";

            // Build mapping: c2058.DescriptionID -> c2058.TextID
            var descToText = new Dictionary<ushort, ushort>();
            foreach (var d in gd.c2058.Items)
            {
                // last write wins; IDs should be unique anyway
                descToText[d.DescriptionID] = d.TextID;
            }

            var result = new HashSet<ushort>();

            foreach (var t in gd.c2054.Items)
            {
                bool hit = false;

                // Spell name text
                if (t.TextID != 0 && SharedHelperScripts.TextContains(gd, t.TextID, needle))
                    hit = true;

                // Spell description text: c2054.DescriptionID -> c2058.TextID -> c2016
                if (!hit && t.DescriptionID != 0)
                {
                    if (descToText.TryGetValue(t.DescriptionID, out ushort descTextID) && descTextID != 0)
                    {
                        if (SharedHelperScripts.TextContains(gd, descTextID, needle))
                            hit = true;
                    }
                    else
                    {
                        // Fallback for any edge-case databases where DescriptionID is already a TextID
                        if (SharedHelperScripts.TextContains(gd, t.DescriptionID, needle))
                            hit = true;
                    }
                }

                if (hit)
                    result.Add(t.SpellLineID);
            }

            return result;
        }

        /// <summary>
        /// Traverses a spell (and optionally its sub-effect spells) and collects unit IDs referenced by summon-like params.
        /// </summary>
        public static void CollectSummonedUnitIDsFromSpell(
            SFGameDataNew gd,
            ushort spellID,
            HashSet<ushort> outUnitIDs,
            HashSet<ushort> visitedSpells,
            bool followSubEffects
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (outUnitIDs == null) throw new ArgumentNullException(nameof(outUnitIDs));
            if (visitedSpells == null) throw new ArgumentNullException(nameof(visitedSpells));

            if (!visitedSpells.Add(spellID))
                return;

            // Locate spell row
            Category2002Item spell = default;
            bool found = false;
            foreach (var s in gd.c2002.Items)
            {
                if (s.SpellID == spellID) { spell = s; found = true; break; }
            }
            if (!found) return;

            string[] labels = SFSpellDescriptor.get(spell.SpellLineID) ?? Array.Empty<string>();
            uint[] values = SharedHelperScripts.ReadParamsU32(spell);

            for (int i = 0; i < 10; i++)
            {
                uint v = values[i];
                if (v == 0) continue;

                string lab = (i < labels.Length ? labels[i] : "") ?? "";
                string l = lab.Trim().ToLowerInvariant();

                // UnitID param detection (summonable unit)
                if (IsSummonUnitIdLabel(l))
                {
                    // Unit IDs fit in ushort, but some code stores them in u32.
                    ushort unitId = (ushort)v;
                    if (unitId != 0)
                        outUnitIDs.Add(unitId);

                    continue;
                }

                // Follow sub-effect spells
                if (followSubEffects && IsSubEffectLabel(l))
                {
                    ushort subSpellID = (ushort)v;
                    if (subSpellID != 0)
                        CollectSummonedUnitIDsFromSpell(gd, subSpellID, outUnitIDs, visitedSpells, followSubEffects);
                }
            }
        }

        public static bool IsSummonUnitIdLabel(string labelLower)
        {
            // Keep permissive initially; tighten after seeing real descriptor vocabulary.
            return labelLower.Contains("unit id") ||
                   labelLower.Contains("creature id") ||
                   (labelLower.Contains("summon") && labelLower.Contains("id"));
        }

        public static bool IsSubEffectLabel(string labelLower)
        {
            return labelLower.Contains("sub-effect") || labelLower.Contains("sub effect");
        }

        /// <summary>
        /// Blacklists units whose *name text* contains any of the provided needles (case-insensitive).
        /// - Uses c2024.Unit.NameID -> c2016 text.
        /// - Needles may be full words or substrings.
        /// - Empty / whitespace needles are ignored.
        /// </summary>
        public static HashSet<ushort> BuildUnitIDBlacklist_ByNameContainsAny(
            SFGameDataNew gd,
            IEnumerable<string> needles
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (needles == null) needles = Array.Empty<string>();

            // Normalize needles (ignore empty)
            var needleList = new List<string>();
            foreach (var n in needles)
            {
                if (string.IsNullOrWhiteSpace(n)) continue;
                needleList.Add(n.Trim());
            }

            var result = new HashSet<ushort>();
            if (needleList.Count == 0)
                return result;

            foreach (var u in gd.c2024.Items)
            {
                // If unit has no name text, skip.
                // (If you want to treat missing names as blacklistable, change this.)
                if (u.NameID == 0)
                    continue;

                // Check if ANY needle matches this unit's name text
                bool hit = false;
                for (int i = 0; i < needleList.Count; i++)
                {
                    if (SharedHelperScripts.TextContains(gd, u.NameID, needleList[i]))
                    {
                        hit = true;
                        break;
                    }
                }

                if (hit)
                    result.Add(u.UnitID);
            }

            return result;
        }

        /// <summary>
        /// Same as BuildUnitIDBlacklist_ByNameContainsAny, but reports progress and supports cancellation.
        /// </summary>
        public static HashSet<ushort> BuildUnitIDBlacklist_ByNameContainsAny(
            SFGameDataNew gd,
            IEnumerable<string> needles,
            IProgress<ProgressInfo>? progress,
            CancellationToken cancellationToken
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (needles == null) needles = Array.Empty<string>();

            // Normalize needles (ignore empty)
            var needleList = new List<string>();
            foreach (var n in needles)
            {
                if (string.IsNullOrWhiteSpace(n)) continue;
                needleList.Add(n.Trim());
            }

            var result = new HashSet<ushort>();
            if (needleList.Count == 0)
                return result;

            var units = gd.c2024.Items;
            int total = units.Count;
            int interval = Math.Max(1, ProgressInfo.ProgressUpdateInterval);

            progress?.Report(new ProgressInfo
            {
                Phase = "Blacklist: units by name",
                Current = 0,
                Total = total,
                Detail = $"Scanning {total} units for {needleList.Count} name patterns..."
            });

            for (int idx = 0; idx < total; idx++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (progress != null && (idx % interval == 0 || idx == total - 1))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Blacklist: units by name",
                        Current = idx,
                        Total = total,
                        Detail = $"Unit {idx + 1}/{total}"
                    });
                }

                var u = units[idx];

                if (u.NameID == 0)
                    continue;

                bool hit = false;
                for (int i = 0; i < needleList.Count; i++)
                {
                    if (SharedHelperScripts.TextContains(gd, u.NameID, needleList[i]))
                    {
                        hit = true;
                        break;
                    }
                }

                if (hit)
                    result.Add(u.UnitID);
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Blacklist: units by name",
                Current = total,
                Total = total,
                Detail = $"Done. Blacklisted {result.Count} units."
            });

            return result;
        }
    }
}
