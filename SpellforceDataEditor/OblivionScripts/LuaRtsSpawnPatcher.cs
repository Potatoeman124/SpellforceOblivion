using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SFEngine.SFCFF;

namespace SpellforceDataEditor.OblivionScripts
{
    public static class LuaRtsSpawnPatcher
    {
        // Matches e.g. "MaxClanSize = 15"
        private static readonly Regex RxMaxClanSize = new Regex(
            @"\bMaxClanSize\s*=\s*(?<val>\d+)",
            RegexOptions.Compiled
        );

        // Matches e.g. "Minutes = 3.5"
        private static readonly Regex RxMinutes = new Regex(
            @"\bMinutes\s*=\s*(?<val>[-+]?\d+(?:\.\d+)?)",
            RegexOptions.Compiled
        );

        // Matches e.g. "Units = {2797, 2798, 2799}"
        private static readonly Regex RxUnits = new Regex(
            @"\bUnits\s*=\s*\{(?<ids>[^}]*)\}",
            RegexOptions.Compiled
        );

        // Matches "Init = {779, 783, 777}" (optionally followed by a comma)
        private static readonly Regex RxInitInline =
            new Regex(@"\bInit\s*=\s*\{(?<ids>[^}]*)\}(?<trail>\s*,?)",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        // Finds "SpawnData" blocks by scanning and brace-matching
        private const string SpawnDataToken = "SpawnData";

        // ------------------------------------------------------------
        // Public entry
        // ------------------------------------------------------------
        public static void PatchAllRtsSpawnNTLuaFiles(
            SFEngine.SFCFF.SFGameDataNew gd,
            string globalDataPath,
            List<UnitVarianting.MobModifierStructure> mobTierTable,
            float rtsSpawnFrequency,
            float rtsSpawnSize,
            int[] rtsSpawnWeights,
            bool variantInitMobs,
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (string.IsNullOrWhiteSpace(globalDataPath)) throw new ArgumentNullException(nameof(globalDataPath));
            if (mobTierTable == null) throw new ArgumentNullException(nameof(mobTierTable));
            if (rtsSpawnFrequency <= 0f) throw new ArgumentOutOfRangeException(nameof(rtsSpawnFrequency), "Must be > 0.");
            if (rtsSpawnSize <= 0f) throw new ArgumentOutOfRangeException(nameof(rtsSpawnSize), "Must be > 0.");
            if (rtsSpawnWeights == null) throw new ArgumentNullException(nameof(rtsSpawnWeights));

            cancellationToken.ThrowIfCancellationRequested();

            var root = GetGameRootFromGlobalDataPath(globalDataPath);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"Game root not found: {root}");

            // Build local unit variant chains from game data (no registry dependency)
            var unitChainsByAnyId = BuildUnitChainsByAnyID_FromGameData(gd, mobTierTable);

            // Enumerate all lua files
            var luaFiles = Directory.EnumerateFiles(root, "*.lua", System.IO.SearchOption.AllDirectories).ToList();
            int total = luaFiles.Count;
            int changed = 0;

            progress?.Report(new ProgressInfo
            {
                Phase = "Lua: scan",
                Detail = $"Root: {root}. Lua files: {total}",
                Current = 0,
                Total = Math.Max(total, 1)
            });

            // SpellForce Lua typically is Windows-1252
            var enc = Encoding.GetEncoding(1252);

            for (int idx = 0; idx < total; idx++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string path = luaFiles[idx];
                string text;
                try
                {
                    text = File.ReadAllText(path, enc);
                }
                catch
                {
                    continue; // unreadable -> skip
                }

                string patched = PatchLuaText_AllSpawnFormats(
                    text,
                    unitChainsByAnyId,
                    rtsSpawnFrequency,
                    rtsSpawnSize,
                    rtsSpawnWeights,
                    variantInitMobs
                );

                if (!string.Equals(text, patched, StringComparison.Ordinal))
                {
                    try
                    {
                        File.WriteAllText(path, patched, enc);
                        changed++;
                    }
                    catch
                    {
                        // write failed -> skip
                    }
                }

                if (progress != null && (idx % Math.Max(1, ProgressInfo.ProgressUpdateInterval) == 0 || idx == total - 1))
                {
                    progress.Report(new ProgressInfo
                    {
                        Phase = "Lua: patching",
                        Detail = $"{Path.GetFileName(path)} ({idx + 1}/{total}), changed so far: {changed}",
                        Current = idx + 1,
                        Total = Math.Max(total, 1)
                    });
                }
            }

            progress?.Report(new ProgressInfo
            {
                Phase = "Lua: done",
                Detail = $"Patched files: {changed} / {total}",
                Current = total,
                Total = Math.Max(total, 1)
            });
        }

        // ------------------------------------------------------------
        // Core patching
        // ------------------------------------------------------------
        private static string PatchLuaText_AllSpawnFormats(
            string text,
            Dictionary<ushort, UnitVariantChain> unitChainsByAnyId,
            float rtsSpawnFrequency,
            float rtsSpawnSize,
            int[] rtsSpawnWeights,
            bool variantInitMobs
        )
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1) MaxClanSize: patch globally (covers RtsSpawnNT blocks and spawn-group tables)
            string patched = RxMaxClanSize.Replace(text, m =>
            {
                if (!int.TryParse(m.Groups["val"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int oldVal))
                    return m.Value;

                double scaled = oldVal * (double)rtsSpawnSize;
                int newVal = Math.Max(1, (int)Math.Round(scaled, MidpointRounding.AwayFromZero));
                return m.Value.Replace(m.Groups["val"].Value, newVal.ToString(CultureInfo.InvariantCulture));
            });

            // 2) SpawnData blocks: patch Minutes and Units inside each SpawnData={...}
            patched = PatchAllSpawnDataBlocks(patched, unitChainsByAnyId, rtsSpawnFrequency, rtsSpawnWeights, variantInitMobs);

            // 3) Variant spawning on initial mobs
            if (variantInitMobs)
            {
                patched = RxInitInline.Replace(patched, m =>
                {
                    var baseIds = ParseUShortList(m.Groups["ids"].Value);

                    var expanded = new List<ushort>();
                    foreach (ushort id in baseIds)
                        expanded.AddRange(ExpandUnitIdByWeights(id, unitChainsByAnyId, rtsSpawnWeights));

                    string joined = string.Join(", ", expanded.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                    return $"Init = {{{joined}}}{m.Groups["trail"].Value}";
                });
            }

            return patched;
        }

        private static string PatchAllSpawnDataBlocks(
            string text,
            Dictionary<ushort, UnitVariantChain> unitChainsByAnyId,
            float rtsSpawnFrequency,
            int[] rtsSpawnWeights,
            bool variantInitMobs
        )
        {
            int i = 0;
            var sb = new StringBuilder(text.Length);

            while (true)
            {
                int k = IndexOfWord(text, SpawnDataToken, i);
                if (k < 0)
                {
                    sb.Append(text, i, text.Length - i);
                    break;
                }

                // Copy everything before SpawnData
                sb.Append(text, i, k - i);

                // Find '=' after SpawnData
                int eq = text.IndexOf('=', k);
                if (eq < 0)
                {
                    sb.Append(text, k, text.Length - k);
                    break;
                }

                // Find '{' after '='
                int braceStart = text.IndexOf('{', eq);
                if (braceStart < 0)
                {
                    sb.Append(text, k, text.Length - k);
                    break;
                }

                int braceEnd = FindMatchingBrace(text, braceStart);
                if (braceEnd < 0)
                {
                    sb.Append(text, k, text.Length - k);
                    break;
                }

                // Append "SpawnData = {"
                sb.Append(text, k, braceStart - k + 1);

                // Patch body
                string body = text.Substring(braceStart + 1, braceEnd - braceStart - 1);
                string patchedBody = PatchSpawnDataBody(body, unitChainsByAnyId, rtsSpawnFrequency, rtsSpawnWeights, variantInitMobs);
                sb.Append(patchedBody);

                // Append closing brace
                sb.Append('}');

                i = braceEnd + 1;
            }

            return sb.ToString();
        }

        private static string PatchSpawnDataBody(
            string body,
            Dictionary<ushort, UnitVariantChain> unitChainsByAnyId,
            float rtsSpawnFrequency,
            int[] rtsSpawnWeights,
            bool variantInitMobs
        )
        {
            // Minutes scaling
            body = RxMinutes.Replace(body, m =>
            {
                if (!double.TryParse(m.Groups["val"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double oldVal))
                    return m.Value;

                double newVal = oldVal / (double)rtsSpawnFrequency;
                string s = newVal.ToString("0.###", CultureInfo.InvariantCulture);
                return m.Value.Replace(m.Groups["val"].Value, s);
            });

            // Units expansion (only Units lists, NOT Init lists)
            body = RxUnits.Replace(body, m =>
            {
                string idsText = m.Groups["ids"].Value;
                var baseIds = ParseUShortList(idsText);

                var expanded = new List<ushort>();
                foreach (ushort id in baseIds)
                    expanded.AddRange(ExpandUnitIdByWeights(id, unitChainsByAnyId, rtsSpawnWeights));

                string joined = string.Join(", ", expanded.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                return $"Units = {{{joined}}}";
            });

            return body;
        }

        // ------------------------------------------------------------
        // Unit chain reconstruction from game data
        // ------------------------------------------------------------
        private sealed class TempUnitGroup
        {
            public ushort OriginalCopyUnitID;
            public Dictionary<string, ushort> TierUnitBySuffix = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<ushort, UnitVariantChain> BuildUnitChainsByAnyID_FromGameData(
            SFGameDataNew gd,
            List<UnitVarianting.MobModifierStructure> mobTierTable
        )
        {
            // suffix order LOW->HIGH as given by mobTierTable
            var suffixes = mobTierTable
                .Select(t => (t.Suffix ?? "").Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            // Cache localisation text
            var locText = new Dictionary<ushort, string>();
            try
            {
                var loc = gd.c2016;
                for (int i = 0; i < loc.Items.Count; i++)
                {
                    var it = loc.Items[i];
                    string s = SharedHelperScripts.ReadContent512(ref it) ?? "";
                    ushort id = it.TextID;
                    if (id != 0 && !locText.ContainsKey(id))
                        locText[id] = s;
                }
            }
            catch
            {
                // best-effort; if this fails chains will just not expand
            }

            // Group units by base name and suffix (use c2024 for UnitID/NameID)
            var byBaseName = new Dictionary<string, TempUnitGroup>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var cat = gd.c2024;
                for (int i = 0; i < cat.Items.Count; i++)
                {
                    var u = cat.Items[i];
                    ushort unitId = u.UnitID;
                    ushort nameId = u.NameID;
                    if (unitId == 0 || nameId == 0) continue;

                    if (!locText.TryGetValue(nameId, out string? name) || string.IsNullOrWhiteSpace(name))
                        continue;

                    name = name.Trim();

                    string baseName = name;
                    string foundSuffix = "";

                    // detect suffix in form "Base [Suffix]"
                    foreach (var suf in suffixes)
                    {
                        string needle = " [" + suf + "]";
                        if (name.EndsWith(needle, StringComparison.OrdinalIgnoreCase))
                        {
                            baseName = name.Substring(0, name.Length - needle.Length).TrimEnd();
                            foundSuffix = suf;
                            break;
                        }
                    }

                    if (!byBaseName.TryGetValue(baseName, out var g))
                    {
                        g = new TempUnitGroup();
                        byBaseName[baseName] = g;
                    }

                    if (string.IsNullOrWhiteSpace(foundSuffix))
                    {
                        // original copy (no suffix) — keep first
                        if (g.OriginalCopyUnitID == 0)
                            g.OriginalCopyUnitID = unitId;
                    }
                    else
                    {
                        // tier unit by suffix — last wins
                        g.TierUnitBySuffix[foundSuffix] = unitId;
                    }
                }
            }
            catch
            {
                // if c2024 is unavailable for some reason, no expansions will happen
            }

            // Build chains and map by ANY tier id -> chain
            var byAnyId = new Dictionary<ushort, UnitVariantChain>();

            foreach (var kv in byBaseName)
            {
                var baseName = kv.Key;
                var g = kv.Value;

                var tierUnits = new List<ushort>();
                foreach (var suf in suffixes)
                {
                    g.TierUnitBySuffix.TryGetValue(suf, out ushort id);
                    tierUnits.Add(id);
                }

                var chain = new UnitVariantChain(baseName, g.OriginalCopyUnitID, tierUnits);

                void Add(ushort id)
                {
                    if (id == 0) return;
                    byAnyId[id] = chain;
                }

                Add(g.OriginalCopyUnitID);
                foreach (var id in tierUnits) Add(id);
            }

            return byAnyId;
        }

        private sealed class UnitVariantChain
        {
            public string BaseName { get; }
            public ushort OriginalCopyUnitID { get; }
            public List<ushort> TierUnitsLowToHigh { get; }

            public UnitVariantChain(string baseName, ushort originalCopyUnitId, List<ushort> tierUnitsLowToHigh)
            {
                BaseName = baseName;
                OriginalCopyUnitID = originalCopyUnitId;
                TierUnitsLowToHigh = tierUnitsLowToHigh ?? new List<ushort>();
            }

            // Weak -> strong list
            public List<ushort> BuildWeakToStrong()
            {
                var res = new List<ushort>();
                if (OriginalCopyUnitID != 0) res.Add(OriginalCopyUnitID);

                foreach (var id in TierUnitsLowToHigh)
                {
                    if (id != 0 && !res.Contains(id))
                        res.Add(id);
                }

                return res;
            }
        }

        // weights[0] corresponds to weakest, weights[last] to strongest.
        // If chain has more tiers than weights, we downsample evenly (keeps weakest+strongest).
        private static List<ushort> ExpandUnitIdByWeights(
            ushort anyTierUnitId,
            Dictionary<ushort, UnitVariantChain> unitChainsByAnyId,
            int[] weights
        )
        {
            if (weights.Length == 0)
                return new List<ushort> { anyTierUnitId };

            if (!unitChainsByAnyId.TryGetValue(anyTierUnitId, out var chain))
                return new List<ushort> { anyTierUnitId };

            var weakToStrong = chain.BuildWeakToStrong();
            if (weakToStrong.Count == 0)
                return new List<ushort> { anyTierUnitId };

            int take = Math.Min(weights.Length, weakToStrong.Count);
            var selected = SelectEvenlySpaced(weakToStrong, take);

            var expanded = new List<ushort>();
            for (int i = 0; i < selected.Count; i++)
            {
                int w = (i < weights.Length) ? weights[i] : 1;
                if (w <= 0) continue;
                for (int k = 0; k < w; k++)
                    expanded.Add(selected[i]);
            }

            return expanded.Count > 0 ? expanded : new List<ushort> { anyTierUnitId };
        }

        private static List<ushort> SelectEvenlySpaced(List<ushort> src, int count)
        {
            if (count >= src.Count) return new List<ushort>(src);
            if (count <= 1) return new List<ushort> { src[0] };

            var res = new List<ushort>(count);
            int last = src.Count - 1;

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / (double)(count - 1);
                int idx = (int)Math.Round(t * last, MidpointRounding.AwayFromZero);
                idx = Math.Max(0, Math.Min(last, idx));
                res.Add(src[idx]);
            }

            // ensure uniqueness only if it accidentally duplicated due to rounding
            // (rare; still better than shrinking)
            for (int i = 1; i < res.Count; i++)
            {
                if (res[i] == res[i - 1])
                {
                    int j = res[i] == src[last] ? last : Math.Min(last, Array.IndexOf(src.ToArray(), res[i]) + 1);
                    res[i] = src[j];
                }
            }

            return res;
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------
        private static List<ushort> ParseUShortList(string s)
        {
            var res = new List<ushort>();
            foreach (Match m in Regex.Matches(s, @"\d+"))
            {
                if (ushort.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort v))
                    res.Add(v);
            }
            return res;
        }

        private static int IndexOfWord(string text, string word, int startIndex)
        {
            int i = startIndex;
            while (true)
            {
                int k = text.IndexOf(word, i, StringComparison.Ordinal);
                if (k < 0) return -1;

                bool leftOk = k == 0 || !IsIdentChar(text[k - 1]);
                int r = k + word.Length;
                bool rightOk = r >= text.Length || !IsIdentChar(text[r]);

                if (leftOk && rightOk) return k;

                i = k + word.Length;
            }
        }

        private static bool IsIdentChar(char c)
            => char.IsLetterOrDigit(c) || c == '_';

        private static int FindMatchingBrace(string text, int openBraceIndex)
        {
            int depth = 0;
            for (int i = openBraceIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static string GetGameRootFromGlobalDataPath(string globalDataPath)
        {
            // Example:
            // G:\...\Spellforce Platinum Edition\data\GameData.cff
            // Go 2 levels up -> main directory
            var dataDir = Path.GetDirectoryName(globalDataPath) ?? "";
            var p1 = Directory.GetParent(dataDir);
            var p2 = p1?.Parent;
            return p2?.FullName ?? dataDir;
        }
    }
}
