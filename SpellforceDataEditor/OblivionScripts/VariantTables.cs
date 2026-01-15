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
        public static bool AttributeFreedomFlag = true;
        public static bool BringBackTrousers = true;
        public static bool StoningIsOutdated = true;
        // ============================================================== START OF MOB VARIANTS ===========================================================

        public static UnitVarianting.MobModifierStructure MobModifierVeteran = new UnitVarianting.MobModifierStructure
        {
            StrengthMod = 1.5f,
            StaminaMod = 2.0f,
            AgilityMod = 1.0f,
            DexterityMod = 1.0f,
            CharismaMod = 1.0f,
            IntelligenceMod = 1.0f,
            WisdomMod = 1.0f,
            ResistancesMod = 1.0f,
            WalkMod = 1.0f,
            FightMod = 1.0f,
            CastMod = 1.0f,
            Suffix = "Veteran"
        };

        public static UnitVarianting.MobModifierStructure MobModifierElite = new UnitVarianting.MobModifierStructure
        {
            StrengthMod = 2.0f,
            StaminaMod = 3.0f,
            AgilityMod = 1.25f,
            DexterityMod = 1.25f,
            CharismaMod = 1.5f,
            IntelligenceMod = 1.5f,
            WisdomMod = 1.5f,
            ResistancesMod = 1.15f,
            WalkMod = 1.1f,
            FightMod = 1.2f,
            CastMod = 1.4f,
            Suffix = "Elite"
        };

        public static UnitVarianting.MobModifierStructure MobModifierChampion = new UnitVarianting.MobModifierStructure
        {
            StrengthMod = 2.5f,
            StaminaMod = 4.0f,
            AgilityMod = 1.35f,
            DexterityMod = 1.45f,
            CharismaMod = 1.75f,
            IntelligenceMod = 1.75f,
            WisdomMod = 1.75f,
            ResistancesMod = 1.20f,
            WalkMod = 1.15f,
            FightMod = 1.3f,
            CastMod = 1.7f,
            Suffix = "Champion"
        };

        public static UnitVarianting.MobModifierStructure MobModifierOblivion = new UnitVarianting.MobModifierStructure
        {
            StrengthMod = 3.25f,
            StaminaMod = 5.00f,
            AgilityMod = 1.50f,
            DexterityMod = 1.60f,
            CharismaMod = 2.00f,
            IntelligenceMod = 2.00f,
            WisdomMod = 2.00f,
            ResistancesMod = 1.25f,
            WalkMod = 1.25f,
            FightMod = 1.50f,
            CastMod = 2.50f,
            Suffix = "Oblivion"
        };

        public static List<UnitVarianting.MobModifierStructure> mobTierTable = new List<UnitVarianting.MobModifierStructure>
        {
            //VariantTables.MobModifierVeteran,
            VariantTables.MobModifierElite,
            //VariantTables.MobModifierChampion,
            VariantTables.MobModifierOblivion
        };

        // ============================================================== START OF ITEM VARIANTS ===========================================================
        public static ItemVarianting.ItemModifierStructure ItemModifierRare = new ItemVarianting.ItemModifierStructure
        {
            ArmorMod = 1.2f,

            StrengthMod = 1.15f,
            StaminaMod = 1.15f,
            AgilityMod = 1.15f,
            DexterityMod = 1.15f,
            CharismaMod = 1.15f,
            IntelligenceMod = 1.15f,
            WisdomMod = 1.15f,

            ResistancesMod = 1.05f,

            WalkMod = 1.1f,
            FightMod = 1.1f,
            CastMod = 1.1f,

            HealthMod = 1.15f,
            ManaMod = 1.15f,

            WeaponSpeedMod = 1.00f,
            MinDamageMod = 1.1f,
            MaxDamageMod = 1.1f,
            MaxRangeMod = 1.0f,

            SellMod = 1.25f,
            BuyMod = 3.0f,

            Suffix = "Rare"
        };

        public static ItemVarianting.ItemModifierStructure ItemModifierMasterwork = new ItemVarianting.ItemModifierStructure
        {
            ArmorMod = 1.40f,

            StrengthMod = 1.30f,
            StaminaMod = 1.40f,
            AgilityMod = 1.25f,
            DexterityMod = 1.30f,
            CharismaMod = 1.20f,
            IntelligenceMod = 1.25f,
            WisdomMod = 1.25f,

            ResistancesMod = 1.10f,

            WalkMod = 1.15f,
            FightMod = 1.15f,
            CastMod = 1.15f,

            HealthMod = 1.25f,
            ManaMod = 1.25f,

            WeaponSpeedMod = 1.05f,
            MinDamageMod = 1.2f,
            MaxDamageMod = 1.3f,
            MaxRangeMod = 1.0f,

            SellMod = 2.00f,
            BuyMod = 7.0f,

            Suffix = "Masterwork"
        };

        public static ItemVarianting.ItemModifierStructure ItemModifierPerfect = new ItemVarianting.ItemModifierStructure
        {
            ArmorMod = 1.65f,

            StrengthMod = 1.40f,
            StaminaMod = 1.50f,
            AgilityMod = 1.30f,
            DexterityMod = 1.40f,
            CharismaMod = 1.30f,
            IntelligenceMod = 1.35f,
            WisdomMod = 1.35f,

            ResistancesMod = 1.15f,

            WalkMod = 1.20f,
            FightMod = 1.20f,
            CastMod = 1.20f,

            HealthMod = 1.40f,
            ManaMod = 1.40f,

            WeaponSpeedMod = 1.05f,
            MinDamageMod = 1.30f,
            MaxDamageMod = 1.50f,
            MaxRangeMod = 1.05f,

            SellMod = 3.00f,
            BuyMod = 10.0f,

            Suffix = "Perfect"
        };

        public static ItemVarianting.ItemModifierStructure ItemModifierLegendary = new ItemVarianting.ItemModifierStructure
        {
            ArmorMod = 1.85f,

            StrengthMod = 1.55f,
            StaminaMod = 1.65f,
            AgilityMod = 1.40f,
            DexterityMod = 1.50f,
            CharismaMod = 1.40f,
            IntelligenceMod = 1.50f,
            WisdomMod = 1.50f,

            ResistancesMod = 1.20f,

            WalkMod = 1.25f,
            FightMod = 1.30f,
            CastMod = 1.30f,

            HealthMod = 1.60f,
            ManaMod = 1.60f,

            WeaponSpeedMod = 1.10f,
            MinDamageMod = 1.40f,
            MaxDamageMod = 1.65f,
            MaxRangeMod = 1.10f,

            SellMod = 5.00f,
            BuyMod = 25.0f,

            Suffix = "Legendary"
        };

        public static List<ItemVarianting.ItemModifierStructure> itemTierTable = new List<ItemVarianting.ItemModifierStructure>
        {
            //VariantTables.ItemModifierRare,
            VariantTables.ItemModifierMasterwork,
            //VariantTables.ItemModifierPerfect,
            VariantTables.ItemModifierLegendary
        };

        // ============================================================== START OF Spell VARIANTS ===========================================================

        public static SpellModifierStructure SpellModifierEmpowered = new SpellModifierStructure
        {
            Suffix = "Empowered",
            BuyPriceMult = 1.50f,
            SellPriceMult = 1.20f,

            Direct =
                {
                    DirectDamage =
                    {
                        DamageMult = 1.20f,
                        ManaCostMult = 1.10f,
                        RecastTimeMult = 1.00f,
                        CastTimeMult = 1.00f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 1.10f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.90f,
                        ManaCostMult = 1.10f,
                        RecastTimeMult = 1.00f,
                    },
                    Healing =
                    {
                        HealMult = 1.12f,
                        ManaCostMult = 1.15f,
                        CastTimeMult = 0.90f,
                        RecastTimeMult = 0.90f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.10f,
                        DurationMult = 1.10f,
                        ManaCostMult = 1.10f,
                        RecastTimeMult = 1.00f,
                    },
                    CrowdControl =
                    {
                        DurationMult = 1.05f,
                        ManaCostMult = 1.20f,
                        RecastTimeMult = 1.00f,
                    },
                    Utility =
                    {
                        ManaCostMult = 0.90f,
                        RecastTimeMult = 1.00f,
                    }
                },

            // NEW: Summoning tier behavior
            Summoning =
                {
                    // Pacing
                    ManaCostMult = 1.10f,
                    CastTimeMult = 1.10f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 1.10f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = VariantTables.MobModifierVeteran,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
        };

        public static SpellModifierStructure SpellModifierSuperior = new SpellModifierStructure
        {
            Suffix = "Superior",
            BuyPriceMult = 6.00f,
            SellPriceMult = 2.00f,

            Direct =
                {
                    DirectDamage =
                    {
                        DamageMult = 1.50f,
                        ManaCostMult = 1.25f,
                        RecastTimeMult = 1.00f,
                        CastTimeMult = 0.90f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 1.25f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.80f,
                        ManaCostMult = 1.25f,
                        RecastTimeMult = 0.90f,
                    },
                    Healing =
                    {
                        HealMult = 1.25f,
                        ManaCostMult = 1.2f,
                        CastTimeMult = 0.80f,
                        RecastTimeMult = 0.80f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.20f,
                        DurationMult = 1.25f,
                        ManaCostMult = 1.20f,
                        RecastTimeMult = 1.00f,
                    },
                    CrowdControl =
                    {
                        DurationMult = 1.10f,
                        ManaCostMult = 1.30f,
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
                    ManaCostMult = 1.30f,
                    CastTimeMult = 1.20f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 1.20f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = VariantTables.MobModifierElite,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
        };

        public static SpellModifierStructure SpellModifierPerfected = new SpellModifierStructure
        {
            Suffix = "Perfected",
            BuyPriceMult = 10.00f,
            SellPriceMult = 3.00f,

            Direct =
                {
                    DirectDamage =
                    {
                        DamageMult = 1.80f,
                        ManaCostMult = 1.40f,
                        RecastTimeMult = 0.90f,
                        CastTimeMult = 0.80f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 1.40f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.75f,
                        ManaCostMult = 1.40f,
                        RecastTimeMult = 0.80f,
                    },
                    Healing =
                    {
                        HealMult = 1.40f,
                        ManaCostMult = 1.3f,
                        CastTimeMult = 0.75f,
                        RecastTimeMult = 0.70f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.30f,
                        DurationMult = 1.40f,
                        ManaCostMult = 1.30f,
                        RecastTimeMult = 1.00f,
                    },
                    CrowdControl =
                    {
                        DurationMult = 1.20f,
                        ManaCostMult = 1.50f,
                        RecastTimeMult = 1.00f,
                    },
                    Utility =
                    {
                        ManaCostMult = 0.70f,
                        RecastTimeMult = 0.85f,
                    }
                },

            // NEW: Summoning tier behavior
            Summoning =
                {
                    // Pacing
                    ManaCostMult = 1.80f,
                    CastTimeMult = 1.40f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 1.40f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = VariantTables.MobModifierChampion,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
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
                        DamageMult = 2.00f,
                        ManaCostMult = 1.50f,
                        RecastTimeMult = 0.80f,
                        CastTimeMult = 0.70f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 1.50f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.70f,
                        ManaCostMult = 1.50f,
                        RecastTimeMult = 0.75f,
                    },
                    Healing =
                    {
                        HealMult = 1.60f,
                        ManaCostMult = 1.4f,
                        CastTimeMult = 0.65f,
                        RecastTimeMult = 0.60f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.40f,
                        DurationMult = 1.50f,
                        ManaCostMult = 1.35f,
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
                    ManaCostMult = 2.00f,
                    CastTimeMult = 1.70f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 1.60f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = VariantTables.MobModifierOblivion,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
        };

        public static List<SpellModifierStructure> spellTierTable = new List<SpellModifierStructure>
        {
            //SpellModifierEmpowered,
            SpellModifierSuperior,
            //SpellModifierPerfected,
            SpellModifierArch
        };
    }
}
