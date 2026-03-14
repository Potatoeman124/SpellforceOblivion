using System;
using System.IO;
using System.Text.Json;

namespace SpellforceDataEditor.OblivionScripts
{
    public static class VariantConfigIO
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public static VariantConfig LoadOrCreate(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            if (!File.Exists(path))
            {
                var cfg = VariantTables.ExportToConfig();
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, JsonSerializer.Serialize(cfg, JsonOptions));
                return cfg;
            }

            string json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<VariantConfig>(json, JsonOptions);
            if (loaded == null)
                throw new Exception("variant_config.json deserialized to null.");

            return loaded;
        }

        public static void Save(string path, VariantConfig cfg)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));
            if (cfg == null)
                throw new ArgumentNullException(nameof(cfg));

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(cfg, JsonOptions));
        }
    }
}