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
            public ushort PromotedUnitID;       // == BaseUnitID after promotion
            public ushort OriginalCopyUnitID;

            // Fully general tier list (tierTable order + "Original" last)
            public List<UnitPromotion.UnitVariantRecord> Variants = new List<UnitPromotion.UnitVariantRecord>();

            // Optional legacy convenience fields (safe to keep if you still use them elsewhere)
            public ushort VeteranUnitID;
            public ushort EliteUnitID;
            public ushort ChampionUnitID;
        }

        public sealed class ItemEntry
        {
            public ushort BaseItemID;
            public ushort PromotedItemID;        // == BaseItemID after promotion
            public ushort OriginalCopyItemID;

            // Fully general tier list (tierTable order + "Original" last)
            public List<ItemVarianting.ItemVariantRecord> Variants = new List<ItemVarianting.ItemVariantRecord>();

            // Optional legacy convenience fields (keep if other code still reads them)
            public ushort RareItemID;
            public ushort MasterworkItemID;
            public ushort PerfectItemID;
        }

        public sealed class SpellEntry
        {
            public ushort BaseSpellID;

            // The base spell is promoted in-place when tiers exist.
            public ushort PromotedSpellID;

            public ushort BaseScrollItemID;
            public ushort BaseSpellbookItemID;

            // Fully general tier/copy list produced by SpellPromotion.
            public List<SpellGrantVariantRecord> Variants = new List<SpellGrantVariantRecord>();
        }

        public sealed class SpellGrantVariantRecord
        {
            public string VariantName = "";     // e.g. "Empowered", "Superior", "Arch", "Original"
            public ushort SpellID;
            public ushort ScrollItemID;
            public ushort SpellbookItemID;
            public bool IsPromotedBase;
            public bool IsOriginalCopy;
        }
    }
}