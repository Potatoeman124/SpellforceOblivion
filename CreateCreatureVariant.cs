using System;
using SpellforceDataEditor.CFF;
using SpellforceDataEditor.Models;

namespace SpellforceVariantTool
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputPath = @"GameData.cff";
            string outputPath = @"GameData_777_variant.cff";
            int baseCreatureId = 777;

            Console.WriteLine("Loading GameData...");
            GameData gameData = new GameData();
            gameData.Load(inputPath);

            // -------------------------------------------------
            // FIND BASE CREATURE
            // -------------------------------------------------
            Creature baseCreature = gameData.Creatures.GetById(baseCreatureId);
            if (baseCreature == null)
            {
                Console.WriteLine("Creature not found.");
                return;
            }

            Console.WriteLine($"Base creature: {baseCreature.CreatureId}");

            // -------------------------------------------------
            // CLONE CREATURE (EDITOR-SAFE)
            // -------------------------------------------------
            Creature variant = baseCreature.Clone();

            // Assign new IDs using editor logic
            variant.CreatureId = gameData.Creatures.GetNextFreeId();
            variant.Stats.StatsId = gameData.CreatureStats.GetNextFreeId();

            // Clone localisation IDs consistently
            int newTextId = gameData.Localisation.GetNextFreeTextId();
            foreach (var loc in variant.LocalisationEntries)
            {
                loc.TextId = newTextId;
                loc.Text += " [VARIANT]";
            }

            // -------------------------------------------------
            // MODIFY STATS
            // -------------------------------------------------
            variant.Stats.HP = (int)(variant.Stats.HP * 2.0);
            variant.Stats.MinDamage = (int)(variant.Stats.MinDamage * 1.5);
            variant.Stats.MaxDamage = (int)(variant.Stats.MaxDamage * 1.5);

            // -------------------------------------------------
            // INSERT INTO TABLES
            // -------------------------------------------------
            gameData.CreatureStats.Add(variant.Stats);

            foreach (var loc in variant.LocalisationEntries)
            {
                gameData.Localisation.Add(loc);
            }

            gameData.Creatures.Add(variant);

            // -------------------------------------------------
            // SAVE
            // -------------------------------------------------
            gameData.Save(outputPath);

            Console.WriteLine("Variant created successfully.");
            Console.WriteLine($"New Creature ID: {variant.CreatureId}");
        }
    }
}
