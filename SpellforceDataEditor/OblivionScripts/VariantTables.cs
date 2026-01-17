using SpellforceDataEditor.OblivionScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SpellforceDataEditor.OblivionScripts.SpellVarianting;

namespace SpellforceDataEditor.OblivionScripts
{
    public class VariantTables
    {
        // ============================================================== FLAGS FOR ADDITIONAL MODS ======================================================
        public static bool AttributeFreedomFlag = true;         // on each level attribute limit is 255 and there are no attribute requirements for skills
        public static bool BringBackTrousers = true;            // robes now do not block trousers slot
        public static bool StoningIsOutdated = true;            // Fuck you cursed guardian, freedom for Mulandir!
        public static bool IAmOmnidexterous = true;             // 2H weapons are now 1H
        public static bool DepromotePlayerUnitSpells = false;   // when false, all player units will have highest variant of spells
        public static bool VariantInitMobs = true;              // When true, then init mobs in spawn tables will be varianted too, otherwise strongest variants only
        public static bool DontVariantFood = true;              // When true, animals will not be varianted, so hunters will stop dying XD
        // ============================================================== RTS SPAWN MODIFIERS ============================================================
        public static float RTSSpawnSize = 3.0f;            // clan size multiplier
        public static float RTSSpawnFrequency = 3.0f;       // time between spawns
        // ============================================================== EXP DIMINISHING MODIFIER =======================================================
        public static bool HungerForBurgerFlag = true;
        public static ushort HungerForBurgerHunger = 4; 
        // This parameter have relation with Max XP Gained from mob similar to square root
        // If originally max xp = 100, then
        // Hunger = 2, max xp = 142
        // Hunger = 4, max xp = 200
        // etc
        
        // ============================================================== START OF MOB VARIANTS ===========================================================
        public static UnitVarianting.MobModifierStructure MobModifierElite = new UnitVarianting.MobModifierStructure
        {
            StrengthMod = 1.40f,
            StaminaMod = 3.00f,
            AgilityMod = 1.25f,
            DexterityMod = 1.25f,
            CharismaMod = 1.5f,
            IntelligenceMod = 2.00f,
            WisdomMod = 2.00f,
            ResistancesMod = 1.10f,
            WalkMod = 1.10f,
            FightMod = 1.15f,
            CastMod = 1.40f,
            Suffix = "Elite"
        };

        public static UnitVarianting.MobModifierStructure MobModifierOblivion = new UnitVarianting.MobModifierStructure
        {
            StrengthMod = 2.00f,
            StaminaMod = 5.00f,
            AgilityMod = 1.50f,
            DexterityMod = 1.60f,
            CharismaMod = 2.00f,
            IntelligenceMod = 3.00f,
            WisdomMod = 3.00f,
            ResistancesMod = 1.15f,
            WalkMod = 1.25f,
            FightMod = 1.30f,
            CastMod = 2.00f,
            Suffix = "Oblivion"
        };

        public static List<UnitVarianting.MobModifierStructure> mobTierTable = new List<UnitVarianting.MobModifierStructure>
        {
            VariantTables.MobModifierElite,
            VariantTables.MobModifierOblivion
        };

        public static int[] RTSSpawnWeights = { 3, 2, 1 };

        // ============================================================== START OF ITEM VARIANTS ===========================================================
        public static ItemVarianting.ItemModifierStructure ItemModifierMasterwork = new ItemVarianting.ItemModifierStructure
        {
            ArmorMod = 1.40f,

            StrengthMod = 1.30f,
            StaminaMod = 1.40f,
            AgilityMod = 1.25f,
            DexterityMod = 1.30f,
            CharismaMod = 1.20f,
            IntelligenceMod = 1.35f,
            WisdomMod = 1.30f,

            ResistancesMod = 1.15f,

            WalkMod = 1.15f,
            FightMod = 1.20f,
            CastMod = 1.35f,

            HealthMod = 1.40f,
            ManaMod = 1.40f,

            WeaponSpeedMod = 1.05f,
            MinDamageMod = 1.15f,
            MaxDamageMod = 1.3f,
            MaxRangeMod = 1.10f,

            SellMod = 2.00f,
            BuyMod = 7.0f,

            Suffix = "Masterwork"
        };

        public static ItemVarianting.ItemModifierStructure ItemModifierLegendary = new ItemVarianting.ItemModifierStructure
        {
            ArmorMod = 2.00f,

            StrengthMod = 1.75f,
            StaminaMod = 2.00f,
            AgilityMod = 1.40f,
            DexterityMod = 1.70f,
            CharismaMod = 1.40f,
            IntelligenceMod = 1.75f,
            WisdomMod = 1.75f,

            ResistancesMod = 1.30f,

            WalkMod = 1.40f,
            FightMod = 1.50f,
            CastMod = 1.75f,

            HealthMod = 2.00f,
            ManaMod = 2.00f,

            WeaponSpeedMod = 1.10f,
            MinDamageMod = 1.30f,
            MaxDamageMod = 1.65f,
            MaxRangeMod = 1.20f,

            SellMod = 4.00f,
            BuyMod = 20.0f,

            Suffix = "Legendary"
        };

        public static List<ItemVarianting.ItemModifierStructure> itemTierTable = new List<ItemVarianting.ItemModifierStructure>
        {
            VariantTables.ItemModifierMasterwork,
            VariantTables.ItemModifierLegendary
        };

        // ============================================================== START OF Spell VARIANTS ===========================================================
        public static SpellModifierStructure SpellModifierSuperior = new SpellModifierStructure
        {
            Suffix = "Superior",
            BuyPriceMult = 6.00f,
            SellPriceMult = 2.00f,

            Direct =
                {
                    DirectDamage =
                    {
                        DamageMult = 2.20f,
                        ManaCostMult = 1.35f,
                        RecastTimeMult = 0.90f,
                        CastTimeMult = 0.90f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 2.00f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.80f,
                        ManaCostMult = 1.35f,
                        RecastTimeMult = 0.90f,
                    },
                    Healing =
                    {
                        HealMult = 1.50f,
                        ManaCostMult = 1.25f,
                        CastTimeMult = 0.80f,
                        RecastTimeMult = 0.80f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.20f,
                        DurationMult = 1.45f,
                        ManaCostMult = 1.20f,
                        RecastTimeMult = 1.00f,
                    },
                    CrowdControl =
                    {
                        DurationMult = 1.20f,
                        ManaCostMult = 1.40f,
                        RecastTimeMult = 1.00f,
                    },
                    Utility =
                    {
                        ManaCostMult = 0.80f,
                        RecastTimeMult = 0.90f,
                    }
                },

            // NEW: Summoning tier behavior
            Summoning =
                {
                    // Pacing
                    ManaCostMult = 1.60f,
                    CastTimeMult = 1.40f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 1.40f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = VariantTables.SummonModifierSuperior,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
        };
        public static UnitVarianting.MobModifierStructure SummonModifierSuperior = new UnitVarianting.MobModifierStructure
        {
            StrengthMod = 2.00f,
            StaminaMod = 2.25f,
            AgilityMod = 1.15f,
            DexterityMod = 1.35f,
            CharismaMod = 2.0f,
            IntelligenceMod = 2.00f,
            WisdomMod = 2.00f,
            ResistancesMod = 1.20f,
            WalkMod = 1.15f,
            FightMod = 1.20f,
            CastMod = 1.50f,
            Suffix = "Superior"
        };

        public static SpellModifierStructure SpellModifierArch = new SpellModifierStructure
        {
            Suffix = "Arch",
            BuyPriceMult = 20.00f,
            SellPriceMult = 4.00f,

            Direct =
                {
                    DirectDamage =
                    {
                        DamageMult = 3.50f,
                        ManaCostMult = 2.00f,
                        RecastTimeMult = 0.80f,
                        CastTimeMult = 0.70f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 3.20f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.65f,
                        ManaCostMult = 2.00f,
                        RecastTimeMult = 0.75f,
                    },
                    Healing =
                    {
                        HealMult = 2.25f,
                        ManaCostMult = 1.65f,
                        CastTimeMult = 0.65f,
                        RecastTimeMult = 0.60f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.50f,
                        DurationMult = 2.00f,
                        ManaCostMult = 1.45f,
                        RecastTimeMult = 1.00f,
                    },
                    CrowdControl =
                    {
                        DurationMult = 1.35f,
                        ManaCostMult = 1.70f,
                        RecastTimeMult = 1.00f,
                    },
                    Utility =
                    {
                        ManaCostMult = 0.60f,
                        RecastTimeMult = 0.75f,
                    }
                },

            // NEW: Summoning tier behavior
            Summoning =
                {
                    // Pacing
                    ManaCostMult = 2.20f,
                    CastTimeMult = 2.00f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 2.00f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = VariantTables.SummonModifierArch,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
        };
        public static UnitVarianting.MobModifierStructure SummonModifierArch = new UnitVarianting.MobModifierStructure
        {
            StrengthMod = 3.00f,
            StaminaMod = 3.75f,
            AgilityMod = 1.25f,
            DexterityMod = 1.75f,
            CharismaMod = 3.00f,
            IntelligenceMod = 3.00f,
            WisdomMod = 3.00f,
            ResistancesMod = 1.40f,
            WalkMod = 1.25f,
            FightMod = 1.35f,
            CastMod = 2.00f,
            Suffix = "Arch"
        };

        public static List<SpellModifierStructure> spellTierTable = new List<SpellModifierStructure>
        {
            //SpellModifierSuperior,
            SpellModifierArch
        };
    }
}
