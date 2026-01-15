using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using SpellforceDataEditor.OblivionScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
namespace SpellforceDataEditor.OblivionScripts
{
    public class SharedHelperScripts
    {
        public static bool TextContains(SFGameDataNew gd, ushort textID, string needle)
        {
            var locCat = gd.c2016;

            for (int i = 0; i < locCat.Items.Count; i++)
            {
                if (locCat.Items[i].TextID == textID)
                {
                    var loc = locCat.Items[i];
                    string text = ReadContent256(ref loc);

                    if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        public static string ReadContent256(ref Category2016Item item)
        {
            unsafe
            {
                fixed (byte* ptr = item.Content)
                {
                    return Encoding.GetEncoding(1252)
                        .GetString(ptr, 256)   // buffer size (adjust if needed)
                        .TrimEnd('\0');
                }
            }
        }

        public static void WriteContent256(ref Category2016Item item, string text)
        {
            byte[] bytes = Encoding.GetEncoding(1252).GetBytes(text);

            unsafe
            {
                fixed (byte* ptr = item.Content)
                {
                    for (int i = 0; i < 256; i++)
                        ptr[i] = 0;

                    for (int i = 0; i < bytes.Length && i < 255; i++)
                        ptr[i] = bytes[i];
                }
            }
        }

        public static bool IsEquippableItem(SFGameDataNew gd, ushort itemID)
        {
            foreach (var a in gd.c2004.Items) // armor
                if (a.ItemID == itemID)
                    return true;

            foreach (var w in gd.c2015.Items) // weapon
                if (w.ItemID == itemID)
                    return true;

            return false;
        }

        public static string GetEnglishItemName(SFGameDataNew gd, ushort nameID)
        {
            foreach (var loc in gd.c2016.Items)
            {
                if (loc.TextID == nameID && loc.LanguageID == 1)
                {
                    var copy = loc;
                    return ReadContent256(ref copy);
                }
            }
            return "<NO ENGLISH NAME>";
        }

        public static void FilterItemListByName(
        string inputPath,
        string outputPath,
        string[] forbiddenSubstrings)
        {
            var lines = File.ReadAllLines(inputPath);
            var sb = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                bool blocked = false;

                foreach (var s in forbiddenSubstrings)
                {
                    if (line.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                    sb.AppendLine(line);
            }

            File.WriteAllText(outputPath, sb.ToString());
        }

        public static unsafe List<int> ReadSpellParams(Category2002Item spell)
        {
            var result = new List<int>();

            const int PARAM_COUNT = 8;

            Category2002Item* s = &spell;
            {
                ushort* p = (ushort*)s->Params;

                for (int i = 0; i < PARAM_COUNT; i++)
                {
                    result.Add(p[i]);
                }
            }

            return result;
        }


        public static string GetSpellLineName(SFGameDataNew gd, ushort spellLineID)
        {
            foreach (var sl in gd.c2054.Items)
            {
                if (sl.SpellLineID == spellLineID)
                {
                    return GetEnglishItemName(gd, sl.TextID);
                }
            }

            return $"<Unknown SpellLine {spellLineID}>";
        }

        public static uint GetParamU32(ref Category2002Item spell, int index)
        {
            if ((uint)index >= 10) throw new ArgumentOutOfRangeException(nameof(index));
            unsafe
            {
                fixed (uint* p = spell.Params)
                    return p[index];
            }
        }

        public static void SetParamU32(ref Category2002Item spell, int index, uint value)
        {
            if ((uint)index >= 10) throw new ArgumentOutOfRangeException(nameof(index));
            unsafe
            {
                fixed (uint* p = spell.Params)
                    p[index] = value;
            }
        }

        public static ushort CloneLocalisationTextID_512(Category2016 locCat, ushort baseTextID, string suffix, bool appendSuffix)
        {
            if (baseTextID == 0)
                throw new Exception("CloneLocalisationTextID_512: baseTextID is 0.");

            // 1) Collect source rows first (NO mutation yet)
            var source = new List<Category2016Item>();
            foreach (var loc in locCat.Items)
                if (loc.TextID == baseTextID)
                    source.Add(loc);

            if (source.Count == 0)
                throw new Exception($"No localisation entries found for TextID {baseTextID}.");

            // 2) Allocate new TextID safely
            ushort newTextID = 0;
            foreach (var loc in locCat.Items)
                if (loc.TextID > newTextID) newTextID = loc.TextID;
            newTextID++;

            // 3) Append new block
            int blockStart = locCat.Items.Count;
            locCat.Indices.Add(blockStart);

            for (int i = 0; i < source.Count; i++)
            {
                var newLoc = source[i];
                newLoc.TextID = newTextID;

                if (appendSuffix && !string.IsNullOrWhiteSpace(suffix))
                {
                    string text = ReadContent512(ref newLoc);
                    WriteContent512(ref newLoc, text + " [" + suffix + "]");
                }

                locCat.Items.Add(newLoc);
            }

            return newTextID;
        }

        public static string ReadContent512(ref Category2016Item item)
        {
            unsafe
            {
                fixed (byte* ptr = item.Content)
                {
                    return Encoding.GetEncoding(1252)
                        .GetString(ptr, 512)
                        .TrimEnd('\0');
                }
            }
        }

        public static uint[] ReadParamsU32(Category2002Item spell)
        {
            var result = new uint[10];

            unsafe
            {
                Category2002Item* s = &spell;
                uint* p = s->Params;

                for (int i = 0; i < 10; i++)
                    result[i] = p[i];
            }

            return result;
        }


        public static void WriteContent512(ref Category2016Item item, string text)
        {
            byte[] bytes = Encoding.GetEncoding(1252).GetBytes(text);

            unsafe
            {
                fixed (byte* ptr = item.Content)
                {
                    for (int i = 0; i < 512; i++)
                        ptr[i] = 0;

                    for (int i = 0; i < bytes.Length && i < 511; i++)
                        ptr[i] = bytes[i];
                }
            }
        }

        public static ushort ScaleUShort(ushort v, float m)
        {
            if (m == 1.0f) return v;
            int nv = (int)Math.Round(v * m);
            if (nv < 0) nv = 0;
            if (nv > ushort.MaxValue) nv = ushort.MaxValue;
            return (ushort)nv;
        }

        public static uint ScaleUInt(uint v, float m)
        {
            if (m == 1.0f) return v;
            double nv = Math.Round(v * (double)m);
            if (nv < 0) nv = 0;
            if (nv > uint.MaxValue) nv = uint.MaxValue;
            return (uint)nv;
        }

        public static string GetSpellNameByLineID(SFGameDataNew gd, ushort spellLineID, int languageId)
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));

            // c2054: SpellLineID -> TextID
            if (!gd.c2054.GetItemIndex(spellLineID, out int idx))
                return "<SpellLine missing>";

            ushort textId = gd.c2054.Items[idx].TextID;
            if (textId == 0)
                return "<No TextID>";

            // Your project already uses SFCategoryManager.GetTextByLanguage(...)
            // (as seen in your spell specimen dump logic).
            string name = SFCategoryManager.GetTextByLanguage(textId, languageId);

            return string.IsNullOrWhiteSpace(name) ? "<Empty name>" : name;
        }

        public static ushort GetMaxItemID_Global(SFGameDataNew gd)
        {
            ushort max = 0;

            // c2003: items
            foreach (var it in gd.c2003.Items)
                if (it.ItemID > max) max = it.ItemID;

            // c2012: item UI
            foreach (var ui in gd.c2012.Items)
                if (ui.ItemID > max) max = ui.ItemID;

            // c2013: scroll -> installed spell item
            foreach (var l in gd.c2013.Items)
            {
                if (l.ItemID > max) max = l.ItemID;
                if (l.InstalledScrollItemID > max) max = l.InstalledScrollItemID;
            }

            // c2014: weapon effects / inventory scroll link
            foreach (var e in gd.c2014.Items)
                if (e.ItemID > max) max = e.ItemID;

            // c2017: requirements
            foreach (var r in gd.c2017.Items)
                if (r.ItemID > max) max = r.ItemID;

            // c2018: spellbook link
            foreach (var sb in gd.c2018.Items)
                if (sb.SpellItemID > max) max = sb.SpellItemID;

            return max;
        }

        public static short ScaleShort(short value, float mult)
        {
            // Use double to reduce rounding artifacts
            double scaled = value * (double)mult;
            int s = (int)Math.Round(scaled);

            if (s > short.MaxValue) s = short.MaxValue;
            if (s < short.MinValue) s = short.MinValue;

            return (short)s;
        }

        public static void NormalizeCategorySingle<T>(CategoryBaseSingle<T> cat)
        where T : struct, ICategoryItem
        {
            if (cat == null) throw new ArgumentNullException(nameof(cat));

            cat.Items.Sort((a, b) => a.GetID().CompareTo(b.GetID()));
        }

        public static void NormalizeCategoryMultiple<T>(CategoryBaseMultiple<T> cat)
            where T : struct, ICategorySubItem
        {
            if (cat == null) throw new ArgumentNullException(nameof(cat));

            // Keep Items sorted by (ID, SubID)
            cat.Items.Sort((a, b) =>
            {
                int c = a.GetID().CompareTo(b.GetID());
                if (c != 0) return c;
                return a.GetSubID().CompareTo(b.GetSubID());
            });

            // Rebuild Indices from scratch (CategoryBaseMultiple.CalculateIndices() is private).:contentReference[oaicite:2]{index=2}
            cat.Indices.Clear();
            int cur = int.MinValue;
            for (int i = 0; i < cat.Items.Count; i++)
            {
                int id = cat.Items[i].GetID();
                if (id != cur)
                {
                    cur = id;
                    cat.Indices.Add(i);
                }
            }
        }

        //====================================================================
        public static void AssertSortedUnique_Single<T>(string name, CategoryBaseSingle<T> cat)
    where T : struct, ICategoryItem
        {
            int prev = int.MinValue;
            var seen = new HashSet<int>();

            for (int i = 0; i < cat.Items.Count; i++)
            {
                int id = cat.Items[i].GetID();

                if (id < prev)
                    throw new Exception($"{name}: Items not sorted at index {i} (id {id} < prev {prev}).");

                if (!seen.Add(id))
                    throw new Exception($"{name}: Duplicate ID {id} at index {i}.");

                prev = id;
            }
        }

        public static void AssertSortedGrouped_Multiple<T>(string name, CategoryBaseMultiple<T> cat)
            where T : struct, ICategorySubItem
        {
            // 1) Items sorted by ID ONLY (non-decreasing)
            int prevID = int.MinValue;
            for (int i = 0; i < cat.Items.Count; i++)
            {
                int id = cat.Items[i].GetID();
                if (id < prevID)
                    throw new Exception($"{name}: Items not sorted by ID at index {i} (id {id} < prev {prevID}).");
                prevID = id;
            }

            // 2) Indices monotonic and valid
            for (int i = 0; i < cat.Indices.Count; i++)
            {
                int start = cat.Indices[i];
                if (start < 0 || start >= cat.Items.Count)
                    throw new Exception($"{name}: Indices[{i}] out of range: {start} (Items.Count={cat.Items.Count}).");

                if (i > 0 && cat.Indices[i] <= cat.Indices[i - 1])
                    throw new Exception($"{name}: Indices not strictly increasing at {i}.");
            }

            // 3) Each index points to the first occurrence of a new ID group
            int lastStart = -1;
            for (int i = 0; i < cat.Indices.Count; i++)
            {
                int start = cat.Indices[i];
                int id = cat.Items[start].GetID();

                if (start == lastStart)
                    throw new Exception($"{name}: Duplicate Indices value at {i}.");

                if (start > 0)
                {
                    int prevId = cat.Items[start - 1].GetID();
                    if (prevId == id)
                        throw new Exception($"{name}: Indices[{i}]={start} does not start a new ID group (ID={id}).");
                }

                lastStart = start;
            }
        }

        public static void RunCriticalChecks(SFGameDataNew gd, string checkpointLabel)
        {
            // Single categories (sorted+unique required)
            AssertSortedUnique_Single("c2002 (spells) @ " + checkpointLabel, gd.c2002);
            AssertSortedUnique_Single("c2003 (items) @ " + checkpointLabel, gd.c2003);
            AssertSortedGrouped_Multiple("c2012 (item UI) @ " + checkpointLabel, gd.c2012);
            AssertSortedUnique_Single("c2013 (scroll<->spellbook) @ " + checkpointLabel, gd.c2013);
            AssertSortedGrouped_Multiple("c2016 (localisation) @ " + checkpointLabel, gd.c2016);
            AssertSortedUnique_Single("c2018 (spellbook->effect) @ " + checkpointLabel, gd.c2018);
            AssertSortedUnique_Single("c2054 (spell types) @ " + checkpointLabel, gd.c2054);

            // Multiple categories you mutate a lot
            AssertSortedGrouped_Multiple("c2014 (item->effect) @ " + checkpointLabel, gd.c2014);
            AssertSortedGrouped_Multiple("c2026 (unit->spells) @ " + checkpointLabel, gd.c2026);
        }

        /// <summary>
        /// Rebuilds a CategoryBaseMultiple so that:
        /// - Items are sorted by (ID, SubID) non-decreasing
        /// - Indices points to the first occurrence of each new ID group
        /// This is critical for categories that rely on binary search/group offsets (e.g. c2040 loot tables).
        /// </summary>
        public static void RebuildAndSortGrouped_Multiple<T>(CategoryBaseMultiple<T> cat)
            where T : struct, ICategorySubItem
        {
            if (cat == null) throw new ArgumentNullException(nameof(cat));

            // Stable ordering: ID asc, SubID asc (LootIndex is usually SubID)
            var sorted = cat.Items
                .Select((item, idx) => (item, idx))
                .OrderBy(x => x.item.GetID())
                .ThenBy(x => x.item.GetSubID())
                .ThenBy(x => x.idx) // stability for duplicates
                .Select(x => x.item)
                .ToList();

            cat.Items.Clear();
            cat.Items.AddRange(sorted);

            cat.Indices.Clear();

            int prevId = int.MinValue;
            for (int i = 0; i < cat.Items.Count; i++)
            {
                int id = cat.Items[i].GetID();
                if (id != prevId)
                {
                    cat.Indices.Add(i);
                    prevId = id;
                }
            }
        }
    }
}
