using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpellforceDataEditor.OblivionScripts
{
    /// <summary>
    /// Central registry for all variant/promotion outputs.
    /// This is intended to be built once per GD generation run and then used
    /// for loot/merchant/chest/spawn rewrites without scanning categories.
    /// </summary>
    public sealed class VariantRegistry
    {
        public Dictionary<ushort, UnitEntry> Units { get; } = new();
        public Dictionary<ushort, ItemEntry> Items { get; } = new();
        public Dictionary<ushort, SpellEntry> Spells { get; } = new();

        public sealed class UnitEntry
        {
            public ushort BaseUnitID;
            public ushort PromotedUnitID;     // == BaseUnitID (Oblivion after promotion)
            public ushort VeteranUnitID;
            public ushort EliteUnitID;
            public ushort ChampionUnitID;
            public ushort OriginalCopyUnitID;
        }

        public sealed class ItemEntry
        {
            public ushort BaseItemID;
            public ushort PromotedItemID;     // == BaseItemID (Legendary after promotion)
            public ushort RareItemID;
            public ushort MasterworkItemID;
            public ushort PerfectItemID;
            public ushort OriginalCopyItemID;
        }

        public sealed class SpellEntry
        {
            public ushort BaseSpellID;
            public ushort PromotedSpellID;    // == BaseSpellID (Arch after promotion)

            public ushort EmpoweredSpellID;
            public ushort SuperiorSpellID;
            public ushort PerfectedSpellID;
            public ushort OriginalCopySpellID;

            // Base chain
            public ushort BaseScrollItemID;
            public ushort BaseSpellbookItemID;

            // Variant chains
            public ushort EmpoweredScrollItemID;
            public ushort EmpoweredSpellbookItemID;

            public ushort SuperiorScrollItemID;
            public ushort SuperiorSpellbookItemID;

            public ushort PerfectedScrollItemID;
            public ushort PerfectedSpellbookItemID;

            public ushort OriginalCopyScrollItemID;
            public ushort OriginalCopySpellbookItemID;
        }
    }
}