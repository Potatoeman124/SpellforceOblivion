using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using SpellforceDataEditor.OblivionScripts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static SpellforceDataEditor.special_forms.SpelllforceCFFEditor;

namespace SpellforceDataEditor.OblivionScripts
{
    public class HelperDumpers
    {
        public static void WriteDump(string fileName, string content)
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                fileName
            );

            File.WriteAllText(path, content);

            MessageBox.Show(
                $"Written:\n{path}",
                "Item analysis complete"
            );
        }

        public static void DumpItemsNotSoldByMerchants(SFGameDataNew gd)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            var merchantCat = gd.c2042;
            var itemCat = gd.c2003;
            var locCat = gd.c2016;

            // -------------------------------------------------
            // 1. Collect all ItemIDs sold by merchants
            // -------------------------------------------------
            var soldItemIDs = new HashSet<ushort>();

            foreach (var m in merchantCat.Items)
            {
                soldItemIDs.Add(m.ItemID);
            }

            // -------------------------------------------------
            // 2. Iterate over all items and find unsold ones
            // -------------------------------------------------
            var sb = new System.Text.StringBuilder();

            foreach (var item in itemCat.Items)
            {
                if (soldItemIDs.Contains(item.ItemID))
                    continue;

                // -------------------------------------------------
                // 3. Resolve English name via localisation
                // -------------------------------------------------
                string name = "<NO ENGLISH NAME>";

                for (int i = 0; i < locCat.Items.Count; i++)
                {
                    var loc = locCat.Items[i];

                    if (loc.TextID == item.NameID && loc.LanguageID == 1)
                    {
                        var locCopy = loc;
                        name = SharedHelperScripts.ReadContent256(ref locCopy);
                        break;
                    }
                }

                sb.AppendLine($"{item.ItemID}\t{name}");
            }

            // -------------------------------------------------
            // 4. Write to text file
            // -------------------------------------------------
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Items_Not_Sold_By_Merchants.txt"
            );

            System.IO.File.WriteAllText(path, sb.ToString());

            MessageBox.Show(
                $"Item list written to:\n{path}",
                "Merchant analysis complete"
            );
        }

        public static void DumpQuestRewardEquippableItems(SFGameDataNew gd)
        {
            string path = Path.Combine(
                SFEngine.Settings.GameDirectory,
                "script",
                "gdsquestrewards.lua"
            );

            if (!File.Exists(path))
            {
                MessageBox.Show("gdsquestrewards.lua not found.");
                return;
            }

            string luaText = File.ReadAllText(path);
            var questItemIDs = ExtractQuestRewardItemIDs(luaText);

            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                if (!questItemIDs.Contains(item.ItemID))
                    continue;

                if (!SharedHelperScripts.IsEquippableItem(gd, item.ItemID))
                    continue;

                string name = SharedHelperScripts.GetEnglishItemName(gd, item.NameID);
                sb.AppendLine($"{item.ItemID}\t{name}");
            }

            string outPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Quest_Reward_Equippable_Items.txt"
            );

            File.WriteAllText(outPath, sb.ToString());

            MessageBox.Show($"Written:\n{outPath}", "Quest reward analysis complete");
        }
        public static void DumpQuestExclusiveEquippableItems(SFGameDataNew gd)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            // -------------------------------------------------
            // Locate quest rewards file
            // -------------------------------------------------
            string questPath = Path.Combine(
                SFEngine.Settings.GameDirectory,
                "script",
                "gdsquestrewards.lua"
            );

            if (!File.Exists(questPath))
            {
                MessageBox.Show("gdsquestrewards.lua not found.");
                return;
            }

            // -------------------------------------------------
            // Parse quest reward item IDs
            // -------------------------------------------------
            string luaText = File.ReadAllText(questPath);
            HashSet<ushort> questItemIDs = ExtractQuestRewardItemIDs(luaText);

            // -------------------------------------------------
            // Collect merchant item IDs (c2042)
            // -------------------------------------------------
            HashSet<ushort> merchantItemIDs = new HashSet<ushort>();
            foreach (var m in gd.c2042.Items)
                merchantItemIDs.Add(m.ItemID);

            // -------------------------------------------------
            // Collect drop / chest item IDs
            // (reuse your existing logic here)
            // -------------------------------------------------
            HashSet<ushort> dropItemIDs = CollectDropEquippableItemIDs(gd);

            // -------------------------------------------------
            // Scan all items
            // -------------------------------------------------
            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                ushort itemID = item.ItemID;

                // must be quest reward
                if (!questItemIDs.Contains(itemID))
                    continue;

                // must NOT be sold by merchants
                if (merchantItemIDs.Contains(itemID))
                    continue;

                // must NOT drop from enemies / chests
                if (dropItemIDs.Contains(itemID))
                    continue;

                // must be equippable
                if (!SharedHelperScripts.IsEquippableItem(gd, itemID))
                    continue;

                // resolve English name
                string name = SharedHelperScripts.GetEnglishItemName(gd, item.NameID);

                sb.AppendLine($"{itemID}\t{name}");
            }

            // -------------------------------------------------
            // Write output
            // -------------------------------------------------
            string outPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Quest_Exclusive_Equippable_Items.txt"
            );

            File.WriteAllText(outPath, sb.ToString());

            MessageBox.Show(
                $"Quest-exclusive equippable items written to:\n{outPath}",
                "Quest item analysis complete"
            );
        }
        static public void DumpUnusedEquippableItems(SFGameDataNew gd)
        {
            var mobLoot = CollectMobLootItemIDs(gd);
            var chestLoot = CollectChestLootItemIDs(gd);
            var equip = CollectEquippableItemIDs(gd);

            var itemCat = gd.c2003;
            var locCat = gd.c2016;

            var sb = new System.Text.StringBuilder();

            foreach (var item in itemCat.Items)
            {
                ushort id = item.ItemID;

                // Condition 1 & 2: not in mob loot AND not in chest loot
                if (mobLoot.Contains(id)) continue;
                if (chestLoot.Contains(id)) continue;

                // Condition 3: must be armor or weapon
                if (!equip.Contains(id)) continue;

                // Resolve name
                string name = "<NO NAME FOUND>";
                bool foundAny = false;

                for (int i = 0; i < locCat.Items.Count; i++)
                {
                    var loc = locCat.Items[i];

                    if (loc.TextID != item.NameID)
                        continue;

                    // First matching entry → fallback
                    if (!foundAny)
                    {
                        var locCopy = loc;
                        name = SharedHelperScripts.ReadContent256(ref locCopy);
                        foundAny = true;
                    }

                    // Prefer English if present
                    if (loc.LanguageID == 1)
                    {
                        var locCopy = loc;
                        name = SharedHelperScripts.ReadContent256(ref locCopy);
                        break;
                    }
                }

                sb.AppendLine($"{id}\t{name}");
            }

            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Unused_Equippable_Items.txt"
            );

            System.IO.File.WriteAllText(path, sb.ToString());

            MessageBox.Show(
                $"Unused equippable item list written to:\n{path}",
                "Item analysis complete"
            );
        }

        public static void DumpMerchantExclusiveEquippableItems(SFGameDataNew gd)
        {
            var merchantIDs = CollectMerchantEquippableItemIDs(gd);
            var mobIDs = CollectMobLootEquippableItemIDs(gd);
            var chestIDs = CollectChestEquippableItemIDs(gd);
            var questIDs = CollectQuestEquippableItemIDs(gd);

            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                ushort id = item.ItemID;

                if (!merchantIDs.Contains(id)) continue;
                if (mobIDs.Contains(id)) continue;
                if (chestIDs.Contains(id)) continue;
                if (questIDs.Contains(id)) continue;

                sb.AppendLine($"{id}\t{SharedHelperScripts.GetEnglishItemName(gd, item.NameID)}");
            }

            WriteDump("Merchant_Exclusive_Equippable_Items.txt", sb.ToString());
        }

        public static void DumpMobLootExclusiveEquippableItems(SFGameDataNew gd)
        {
            var mobIDs = CollectMobLootEquippableItemIDs(gd);
            var merchantIDs = CollectMerchantEquippableItemIDs(gd);
            var chestIDs = CollectChestEquippableItemIDs(gd);
            var questIDs = CollectQuestEquippableItemIDs(gd);

            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                ushort id = item.ItemID;

                if (!mobIDs.Contains(id)) continue;
                if (merchantIDs.Contains(id)) continue;
                if (chestIDs.Contains(id)) continue;
                if (questIDs.Contains(id)) continue;

                sb.AppendLine($"{id}\t{SharedHelperScripts.GetEnglishItemName(gd, item.NameID)}");
            }

            WriteDump("Mob_Loot_Exclusive_Equippable_Items.txt", sb.ToString());
        }

        public static void DumpChestExclusiveEquippableItems(SFGameDataNew gd)
        {
            var chestIDs = CollectChestEquippableItemIDs(gd);
            var mobIDs = CollectMobLootEquippableItemIDs(gd);
            var merchantIDs = CollectMerchantEquippableItemIDs(gd);
            var questIDs = CollectQuestEquippableItemIDs(gd);

            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                ushort id = item.ItemID;

                if (!chestIDs.Contains(id)) continue;
                if (mobIDs.Contains(id)) continue;
                if (merchantIDs.Contains(id)) continue;
                if (questIDs.Contains(id)) continue;

                sb.AppendLine($"{id}\t{SharedHelperScripts.GetEnglishItemName(gd, item.NameID)}");
            }

            WriteDump("Chest_Exclusive_Equippable_Items.txt", sb.ToString());
        }

        public static void DumpSpellParameterSpecimens(SFGameDataNew gd)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            var spellCat = gd.c2002;
            var spellLineCat = gd.c2054;
            var locCat = gd.c2016;

            var sb = new StringBuilder();

            // 👇 NEW: track processed spell types
            var dumpedSpellTypes = new HashSet<ushort>();

            foreach (var spell in spellCat.Items)
            {
                ushort spellID = spell.SpellID;
                ushort spellTypeID = spell.SpellLineID;

                // 👇 NEW: skip duplicates
                if (!dumpedSpellTypes.Add(spellTypeID))
                    continue;

                // -------------------------------------------------
                // Resolve spell name
                // -------------------------------------------------
                string spellName = "<UNKNOWN SPELL>";

                if (spellLineCat.GetItemIndex(spellTypeID, out int spellLineIndex))
                {
                    ushort textID = spellLineCat.Items[spellLineIndex].TextID;
                    bool found = false;

                    foreach (var loc in locCat.Items)
                    {
                        if (loc.TextID != textID)
                            continue;

                        var locCopy = loc;
                        string text = SharedHelperScripts.ReadContent256(ref locCopy);

                        if (!found)
                        {
                            spellName = text;
                            found = true;
                        }

                        if (loc.LanguageID == 1) // English preferred
                        {
                            spellName = text;
                            break;
                        }
                    }
                }

                // -------------------------------------------------
                // Resolve parameter labels
                // -------------------------------------------------
                string[] labels = SFSpellDescriptor.get(spellTypeID);

                // -------------------------------------------------
                // Header
                // -------------------------------------------------
                sb.AppendLine(
                    $"{spellName} (SpellID: {spellID}, SpellTypeID: {spellTypeID})"
                );

                // -------------------------------------------------
                // Dump parameters
                // -------------------------------------------------
                const int PARAM_COUNT = 8;

                for (int i = 0; i < PARAM_COUNT; i++)
                {
                    uint value = spell.GetParam(i);
                    string label = (labels != null && i < labels.Length)
                        ? labels[i]
                        : "";

                    sb.AppendLine(
                        $"- [{i}] - {value}\t- {label}"
                    );
                }

                sb.AppendLine();
            }

            // -------------------------------------------------
            // Write output
            // -------------------------------------------------
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Spell_Parameter_Specimens.txt"
            );

            File.WriteAllText(path, sb.ToString());

            MessageBox.Show(
                $"Spell parameter specimens written to:\n{path}",
                "Spell analysis complete"
            );
        }

        public static void DumpSpellClassificationLookup(
        SFGameDataNew gd,
        int languageId,
        string outputPath,
        HashSet<ushort> blacklistedSpellLineIDs
        )
        {
            blacklistedSpellLineIDs ??= new HashSet<ushort>();

            // One representative per SpellLineID (as you did before)
            var byLine = new Dictionary<ushort, Category2002Item>();
            foreach (var s in gd.c2002.Items)
            {
                if (s.SpellLineID == 0) continue;
                if (!byLine.ContainsKey(s.SpellLineID))
                    byLine.Add(s.SpellLineID, s);
            }

            var all = byLine.Values
                .Select(s => SpellVarianting.ClassifySpellUnified(gd, s, languageId, blacklistedSpellLineIDs))
                .OrderBy(c => c.IsBlacklisted ? 0 : 1) // blacklisted first section
                .ThenBy(c => c.MainCategory.ToString())
                .ThenBy(c => c.DirectArchetype.HasValue ? c.DirectArchetype.Value.ToString() : "")
                .ThenBy(c => c.FeatureSignature)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using var w = new StreamWriter(outputPath, false, Encoding.UTF8);

            // BLACKLIST section
            var bl = all.Where(x => x.IsBlacklisted).ToList();
            if (bl.Count > 0)
            {
                w.WriteLine("===== BLACKLIST =====");
                w.WriteLine("These SpellLineIDs are excluded from grouping and variant generation.");
                w.WriteLine();
                foreach (var c in bl)
                    w.WriteLine($"- {c.Name} (SpellID: {c.SpellID}, SpellLineID: {c.SpellLineID})");
                w.WriteLine();
            }

            // Main grouped view (Direct archetypes include HasSubEffect as an annotation)
            foreach (var mainGroup in all.Where(x => !x.IsBlacklisted).GroupBy(c => c.MainCategory))
            {
                w.WriteLine($"===== {mainGroup.Key} =====");
                w.WriteLine();

                if (mainGroup.Key == SpellMainCategory.DirectLike)
                {
                    foreach (var archGroup in mainGroup.GroupBy(c => c.DirectArchetype ?? DirectSpellArchetype.Utility))
                    {
                        w.WriteLine($"--- {archGroup.Key} ---");

                        foreach (var sigGroup in archGroup.GroupBy(c => c.FeatureSignature))
                        {
                            w.WriteLine($"  [Signature] {sigGroup.Key}");

                            foreach (var c in sigGroup)
                            {
                                string sub = c.HasSubEffect ? " (HasSubEffect)" : "";
                                w.WriteLine($"    - {c.Name} (SpellID: {c.SpellID}, SpellLineID: {c.SpellLineID}){sub}");
                            }

                            w.WriteLine();
                        }

                        w.WriteLine();
                    }
                }
                else
                {
                    // Summoning / SpecialCase / Dummy…
                    foreach (var sigGroup in mainGroup.GroupBy(c => c.FeatureSignature))
                    {
                        w.WriteLine($"  [Signature] {sigGroup.Key}");
                        foreach (var c in sigGroup)
                        {
                            string sub = c.HasSubEffect ? " (HasSubEffect)" : "";
                            w.WriteLine($"    - {c.Name} (SpellID: {c.SpellID}, SpellLineID: {c.SpellLineID}){sub}");
                        }
                        w.WriteLine();
                    }
                }

                w.WriteLine();
            }
        }

        static public HashSet<ushort> CollectMobLootItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();
            var lootCat = gd.c2040;

            foreach (var l in lootCat.Items)
            {
                if (l.ItemID1 != 0) result.Add(l.ItemID1);
                if (l.ItemID2 != 0) result.Add(l.ItemID2);
                if (l.ItemID3 != 0) result.Add(l.ItemID3);
            }

            return result;
        }
        static public HashSet<ushort> CollectChestLootItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();
            var chestCat = gd.c2065;

            foreach (var l in chestCat.Items)
            {
                if (l.ItemID1 != 0) result.Add(l.ItemID1);
                if (l.ItemID2 != 0) result.Add(l.ItemID2);
                if (l.ItemID3 != 0) result.Add(l.ItemID3);
            }

            return result;
        }
        static public HashSet<ushort> CollectEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            // Armor data
            foreach (var a in gd.c2004.Items)
            {
                result.Add(a.ItemID);
            }

            // Weapon data
            foreach (var w in gd.c2015.Items)
            {
                result.Add(w.ItemID);
            }

            return result;
        }

        public static HashSet<ushort> ExtractQuestRewardItemIDs(string luaText)
        {
            var result = new HashSet<ushort>();

            // Match numbers inside Items = { ... }
            var itemBlockRegex = new System.Text.RegularExpressions.Regex(
                @"Items\s*=\s*\{([^}]*)\}",
                System.Text.RegularExpressions.RegexOptions.Multiline
            );

            var numberRegex = new System.Text.RegularExpressions.Regex(@"\d+");

            foreach (System.Text.RegularExpressions.Match block in itemBlockRegex.Matches(luaText))
            {
                foreach (System.Text.RegularExpressions.Match num in numberRegex.Matches(block.Groups[1].Value))
                {
                    if (ushort.TryParse(num.Value, out ushort id))
                        result.Add(id);
                }
            }

            return result;
        }

        public static HashSet<ushort> CollectDropEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            var gameDataType = gd.GetType();

            foreach (var field in gameDataType.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance))
            {
                object category = field.GetValue(gd);
                if (category == null)
                    continue;

                // Skip base item definitions
                if (category == gd.c2003)
                    continue;

                // Skip merchant stock
                if (category == gd.c2042)
                    continue;

                var itemsProp = category.GetType().GetProperty("Items");
                if (itemsProp == null)
                    continue;

                if (itemsProp.GetValue(category) is not System.Collections.IEnumerable items)
                    continue;

                foreach (var item in items)
                {
                    if (item == null)
                        continue;

                    var itemIDField = item.GetType().GetField("ItemID");
                    if (itemIDField == null)
                        continue;

                    object val = itemIDField.GetValue(item);
                    if (val is ushort itemID)
                    {
                        // Only care about equippables
                        if (SharedHelperScripts.IsEquippableItem(gd, itemID))
                            result.Add(itemID);
                    }
                }
            }

            return result;
        }

        public static HashSet<ushort> CollectMobLootEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            foreach (var loot in gd.c2040.Items)
            {
                if (loot.ItemID1 != 0 && SharedHelperScripts.IsEquippableItem(gd, loot.ItemID1))
                    result.Add(loot.ItemID1);

                if (loot.ItemID2 != 0 && SharedHelperScripts.IsEquippableItem(gd, loot.ItemID2))
                    result.Add(loot.ItemID2);

                if (loot.ItemID3 != 0 && SharedHelperScripts.IsEquippableItem(gd, loot.ItemID3))
                    result.Add(loot.ItemID3);
            }

            return result;
        }

        public static HashSet<ushort> CollectChestEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            foreach (var loot in gd.c2065.Items)
            {
                if (loot.ItemID1 != 0 && SharedHelperScripts.IsEquippableItem(gd, loot.ItemID1))
                    result.Add(loot.ItemID1);

                if (loot.ItemID2 != 0 && SharedHelperScripts.IsEquippableItem(gd, loot.ItemID2))
                    result.Add(loot.ItemID2);

                if (loot.ItemID3 != 0 && SharedHelperScripts.IsEquippableItem(gd, loot.ItemID3))
                    result.Add(loot.ItemID3);
            }

            return result;
        }

        public static HashSet<ushort> CollectMerchantEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            foreach (var m in gd.c2042.Items)
            {
                if (SharedHelperScripts.IsEquippableItem(gd, m.ItemID))
                    result.Add(m.ItemID);
            }

            return result;
        }

        public static HashSet<ushort> CollectQuestEquippableItemIDs(SFGameDataNew gd)
        {
            string questPath = Path.Combine(
                SFEngine.Settings.GameDirectory,
                "script",
                "gdsquestrewards.lua"
            );

            var result = new HashSet<ushort>();

            if (!File.Exists(questPath))
                return result;

            string luaText = File.ReadAllText(questPath);
            var questIDs = ExtractQuestRewardItemIDs(luaText);

            foreach (var item in gd.c2003.Items)
            {
                if (questIDs.Contains(item.ItemID) &&
                    SharedHelperScripts.IsEquippableItem(gd, item.ItemID))
                {
                    result.Add(item.ItemID);
                }
            }

            return result;
        }

    }
}
