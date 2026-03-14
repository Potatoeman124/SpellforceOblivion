using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SpellforceDataEditor.OblivionScripts.SpellVarianting;

namespace SpellforceDataEditor.OblivionScripts
{
    public class VariantConfig
    {
        // Flags for additional mods
        public bool AttributeFreedomFlag = true;
        public bool BringBackTrousers = true;
        public bool StoningIsOutdated = true;
        public bool IAmOmnidexterous = true;
        public bool DepromotePlayerUnitSpells = false;
        public bool DepromoteSummonedUnitSpells = false;
        public bool VariantInitMobs = true;
        public bool DontVariantFood = true;
        public bool ArmyDiscountFlag = true;
        public float ArmyDiscountValue = 0.5f;

        public bool ApplyFlatUnitsMod = true;
        public bool ApplyFlatItemsMod = true;
        public bool ApplyFlatSpellMod = true;

        // RTS
        public float RTSSpawnSize = 3.0f;
        public float RTSSpawnFrequency = 2.0f;
        public int[] RTSSpawnWeights = { 3, 2, 1 };

        // EXP
        public bool HungerForBurgerFlag = true;
        public ushort HungerForBurgerHunger = 6;

        // Rune heroes
        public bool KeepThemRelevantDammit = true;
        public UnitVarianting.MobModifierStructure HeroModifierLimitedEQ;
        public UnitVarianting.MobModifierStructure HeroModifierNoEQ;

        // Variant tables actually used by the callback
        public List<UnitVarianting.MobModifierStructure> mobTierTable = new();
        public List<ItemVarianting.ItemModifierStructure> itemTierTable = new();
        public List<SpellModifierStructure> spellTierTable = new();

        // Flat bonuses
        public UnitVarianting.MobModifierStructure FlatUnitMod;
        public ItemVarianting.ItemModifierStructure FlatItemMod;
        public UnitVarianting.MobModifierStructure FlatSummonModifier;
        public SpellModifierStructure FlatSpellModifier;
    }
}