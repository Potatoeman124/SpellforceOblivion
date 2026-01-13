using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using SpellforceDataEditor.OblivionScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SpellforceDataEditor.OblivionScripts.UnitVarianting;
using static SpellforceDataEditor.special_forms.SpelllforceCFFEditor;

namespace SpellforceDataEditor.OblivionScripts
{
    public class SpellVarianting
    {

        public sealed class SpellModifierStructure
        {
            public string Suffix = "Uncommon";
            public float BuyPriceMult = 1.0f;
            public float SellPriceMult = 1.0f;

            public HashSet<ushort> SpellLineBlacklist = new();

            public Dictionary<ushort, DirectSpellArchetype> ArchetypeOverrideBySpellLineID = new();

            public Dictionary<ushort, int> LevelCapAddOverrideBySpellLineID = new();

            public DirectArchetypeProfiles Direct = new();

            // NEW
            public SummoningProfile Summoning = new();

            public sealed class DirectArchetypeProfiles
            {
                public DirectProfile DirectDamage = new();
                public DirectProfile DamageOverTime = new();
                public DirectProfile Healing = new();
                public DirectProfile BuffDebuff = new();
                public DirectProfile CrowdControl = new();
                public DirectProfile Utility = new();
            }

            public sealed class DirectProfile
            {
                public float ManaCostMult = 1.0f;
                public float CastTimeMult = 1.0f;
                public float RecastTimeMult = 1.0f;

                public float MinRangeMult = 1.0f;
                public float MaxRangeMult = 1.0f;

                public float DamageMult = 1.0f;
                public float HealMult = 1.0f;
                public float DurationMult = 1.0f;
                public float TickCountMult = 1.0f;
                public float TickIntervalMult = 1.0f;
                public float RadiusMult = 1.0f;
                public float PercentMult = 1.0f;
                public float ChanceMult = 1.0f;

                public int LevelCapAdd = 0;

                public float GenericParamMult = 1.0f;
            }

            // NEW: Summoning modifiers
            public sealed class SummoningProfile
            {
                // Base spell pacing fields (c2002)
                public float ManaCostMult = 1.0f;
                public float CastTimeMult = 1.0f;
                public float RecastTimeMult = 1.0f;

                // Params (by label)
                public float TickIntervalMult = 1.0f; // "time between ticks" / similar
                public float ManaPerTickMult = 1.0f;  // "mana per tick" / similar

                // Summoned unit scaling (unit variant)
                public MobModifierStructure SummonedMobModifier = new UnitVarianting.MobModifierStructure();

                // If true and SummonedMobModifier.Suffix is empty, copy spell Suffix to mob suffix
                public bool InheritSuffixToSummon = true;
            }
        }

        public static HashSet<ushort> BuildSpellLineBlacklist()
        {
            return new HashSet<ushort>
            {
                223, // Aura Siege Human
                225, // Aura Siege Elf
                226, // Aura Siege Orc
                227, // Aura Siege Troll
                228, // Aura Siege Darkelf
            };
        }
        public sealed class SpellClassification
        {
            public ushort SpellID;
            public ushort SpellLineID;
            public string Name = "";

            public SpellMainCategory MainCategory;
            public DirectSpellArchetype? DirectArchetype;

            public bool HasSubEffect;
            public int SubEffectParamIndex = -1;
            public ushort SubEffectSpellID = 0;

            public string FeatureSignature = "";
            public bool IsBlacklisted;
        }

        public enum SpellMainCategory
        {
            DirectLike,    // includes direct and “has subeffect”
            Summoning,
            SpecialCase,
            DummyOrNoName,
            Blacklisted
        }

        public enum DirectSpellArchetype
        {
            DirectDamage,
            DamageOverTime,
            Healing,
            BuffDebuff,
            CrowdControl,
            Utility
        }

        // ================================================= functionalee

        public static SFGameDataNew CreateSpellVariantAndGrantItems(
        SFGameDataNew gd,
        ushort baseSpellID,
        SpellModifierStructure mods,
        out ushort newSpellID,
        out ushort newScrollItemID,
        out ushort newSpellbookItemID
        )
        {
            gd = SpellVarianting.CreateSpellVariant(gd, baseSpellID, mods, out newSpellID);

            // This assumes you already have this method implemented (as in your current working state)
            gd = ItemVarianting.CreateSpellScrollAndSpellbookForSpellVariant(
                gd,
                baseSpellID,
                newSpellID,
                mods,
                out newScrollItemID,
                out newSpellbookItemID
            );

            return gd;
        }

        public static SFGameDataNew CreateSpellVariantAndGrantItems(
            SFGameDataNew gd,
            ushort baseSpellID,
            SpellModifierStructure mods
        )
        {
            return CreateSpellVariantAndGrantItems(gd, baseSpellID, mods, out _, out _, out _);
        }

        // -----------------------------
        // Public API (unit-style): you do NOT provide IDs
        // -----------------------------
        public static SFGameDataNew CreateSpellVariant(
            SFGameDataNew gd,
            ushort baseSpellID,
            SpellModifierStructure mods
        )
        {
            return CreateSpellVariant(gd, baseSpellID, mods, out _);
        }

        public static SFGameDataNew CreateSpellVariant(
            SFGameDataNew gd,
            ushort baseSpellID,
            SpellModifierStructure mods,
            out ushort newSpellID
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (mods == null) throw new ArgumentNullException(nameof(mods));

            var visited = new HashSet<ushort>();
            var spellCloneMap = new Dictionary<ushort, ushort>(); // baseSpellID -> newSpellID
            var unitCloneMap = new Dictionary<ushort, ushort>();  // baseUnitID  -> newUnitID

            return CreateSpellVariant_Internal(gd, baseSpellID, mods, visited, spellCloneMap, unitCloneMap, out newSpellID);
        }

        // -----------------------------
        // Internal (recursive) creator
        // -----------------------------
        public static SFGameDataNew CreateSpellVariant_Internal(
            SFGameDataNew gd,
            ushort baseSpellID,
            SpellModifierStructure mods,
            HashSet<ushort> visited,
            Dictionary<ushort, ushort> spellCloneMap,
            Dictionary<ushort, ushort> unitCloneMap,
            out ushort newSpellID
        )
        {
            if (spellCloneMap.TryGetValue(baseSpellID, out var existing))
            {
                newSpellID = existing;
                return gd;
            }

            if (!visited.Add(baseSpellID))
                throw new Exception($"Cycle detected while cloning subeffects at SpellID {baseSpellID}.");

            // Find base spell
            var spellCat = gd.c2002;
            Category2002Item baseSpell = default;
            bool found = false;
            foreach (var s in spellCat.Items)
            {
                if (s.SpellID == baseSpellID) { baseSpell = s; found = true; break; }
            }
            if (!found) throw new Exception($"Base spell {baseSpellID} not found.");

            var cls = ClassifySpellUnified(gd, baseSpell, SFEngine.Settings.LanguageID, mods.SpellLineBlacklist);

            if (cls.MainCategory == SpellMainCategory.Blacklisted)
                throw new Exception($"SpellLineID {cls.SpellLineID} is blacklisted; refusing to generate variant.");

            SFGameDataNew result = cls.MainCategory switch
            {
                SpellMainCategory.DirectLike => CreateDirectLikeSpellVariant_Internal(
                    gd, baseSpellID, baseSpell, cls, mods, visited, spellCloneMap, unitCloneMap, out newSpellID),

                SpellMainCategory.Summoning => CreateSummoningSpellVariant_Internal(
                    gd, baseSpellID, baseSpell, cls, mods, visited, spellCloneMap, unitCloneMap, out newSpellID),

                _ => throw new Exception($"CreateSpellVariant: unsupported category {cls.MainCategory} for SpellID {baseSpellID}.")
            };

            visited.Remove(baseSpellID);
            return result;
        }

        public static SFGameDataNew CreateDirectLikeSpellVariant_Internal(
    SFGameDataNew gd,
    ushort baseSpellID,
    Category2002Item baseSpell,
    SpellClassification cls,
    SpellModifierStructure mods,
    HashSet<ushort> visited,
    Dictionary<ushort, ushort> spellCloneMap,
    Dictionary<ushort, ushort> unitCloneMap,
    out ushort newSpellID
)
        {
            var spellCat = gd.c2002;
            var typeCat = gd.c2054;
            var locCat = gd.c2016;

            var archetype = cls.DirectArchetype ?? DirectSpellArchetype.Utility;
            var profile = ResolveDirectProfile(mods, cls.SpellLineID, archetype);

            // Find base type
            Category2054Item baseType = default;
            bool found = false;
            foreach (var t in typeCat.Items)
            {
                if (t.SpellLineID == baseSpell.SpellLineID) { baseType = t; found = true; break; }
            }
            if (!found) throw new Exception($"Spell type {baseSpell.SpellLineID} not found in c2054.");

            // Allocate IDs
            ushort newTypeID = 0;
            foreach (var t in typeCat.Items) if (t.SpellLineID > newTypeID) newTypeID = t.SpellLineID;
            newTypeID++;

            newSpellID = 0;
            foreach (var s in spellCat.Items) if (s.SpellID > newSpellID) newSpellID = s.SpellID;
            newSpellID++;

            // Clone name localisation
            ushort newTextID = SharedHelperScripts.CloneLocalisationTextID_512(locCat, baseType.TextID, mods.Suffix, true);

            // Clone type
            var newType = baseType;
            newType.SpellLineID = newTypeID;
            newType.TextID = newTextID;
            typeCat.Items.Add(newType);

            // Clone spell
            var newSpell = baseSpell;
            newSpell.SpellID = newSpellID;
            newSpell.SpellLineID = newTypeID;

            // Base fields (do not touch EffectPower/EffectRange)
            newSpell.ManaCost = SharedHelperScripts.ScaleUShort(newSpell.ManaCost, profile.ManaCostMult);
            newSpell.CastTime = SharedHelperScripts.ScaleUInt(newSpell.CastTime, profile.CastTimeMult);
            newSpell.RecastTime = SharedHelperScripts.ScaleUInt(newSpell.RecastTime, profile.RecastTimeMult);

            newSpell.MinRange = SharedHelperScripts.ScaleUShort(newSpell.MinRange, profile.MinRangeMult);
            newSpell.MaxRange = SharedHelperScripts.ScaleUShort(newSpell.MaxRange, profile.MaxRangeMult);

            ApplyParamMultipliersByLabel(ref newSpell, baseSpell.SpellLineID, cls.SpellLineID, profile, mods);

            // Sub-effect recursion
            if (cls.HasSubEffect && cls.SubEffectParamIndex >= 0 && cls.SubEffectSpellID != 0)
            {
                gd = CreateSpellVariant_Internal(gd, cls.SubEffectSpellID, mods, visited, spellCloneMap, unitCloneMap, out ushort newSubSpellID);
                SharedHelperScripts.SetParamU32(ref newSpell, cls.SubEffectParamIndex, newSubSpellID);
            }

            spellCat.Items.Add(newSpell);

            spellCloneMap[baseSpellID] = newSpellID;
            return gd;
        }
        public static SpellModifierStructure.DirectProfile ResolveDirectProfile(
        SpellModifierStructure mods,
        ushort spellLineID,
        DirectSpellArchetype archetype
        )
        {
            if (mods.ArchetypeOverrideBySpellLineID != null &&
                mods.ArchetypeOverrideBySpellLineID.TryGetValue(spellLineID, out var forced))
                archetype = forced;

            return archetype switch
            {
                DirectSpellArchetype.DirectDamage => mods.Direct.DirectDamage,
                DirectSpellArchetype.DamageOverTime => mods.Direct.DamageOverTime,
                DirectSpellArchetype.Healing => mods.Direct.Healing,
                DirectSpellArchetype.BuffDebuff => mods.Direct.BuffDebuff,
                DirectSpellArchetype.CrowdControl => mods.Direct.CrowdControl,
                _ => mods.Direct.Utility,
            };
        }

        public static SFGameDataNew CreateSummoningSpellVariant_Internal(
            SFGameDataNew gd,
            ushort baseSpellID,
            Category2002Item baseSpell,
            SpellClassification cls,
            SpellModifierStructure mods,
            HashSet<ushort> visited,
            Dictionary<ushort, ushort> spellCloneMap,
            Dictionary<ushort, ushort> unitCloneMap,
            out ushort newSpellID
        )
        {
            var spellCat = gd.c2002;
            var typeCat = gd.c2054;
            var locCat = gd.c2016;

            // Find base type
            Category2054Item baseType = default;
            bool found = false;
            foreach (var t in typeCat.Items)
            {
                if (t.SpellLineID == baseSpell.SpellLineID) { baseType = t; found = true; break; }
            }
            if (!found) throw new Exception($"Spell type {baseSpell.SpellLineID} not found in c2054.");

            // Allocate IDs
            ushort newTypeID = 0;
            foreach (var t in typeCat.Items) if (t.SpellLineID > newTypeID) newTypeID = t.SpellLineID;
            newTypeID++;

            newSpellID = 0;
            foreach (var s in spellCat.Items) if (s.SpellID > newSpellID) newSpellID = s.SpellID;
            newSpellID++;

            // Clone name localisation
            ushort newTextID = SharedHelperScripts.CloneLocalisationTextID_512(locCat, baseType.TextID, mods.Suffix, true);

            // Clone type
            var newType = baseType;
            newType.SpellLineID = newTypeID;
            newType.TextID = newTextID;
            typeCat.Items.Add(newType);

            // Clone spell
            var newSpell = baseSpell;
            newSpell.SpellID = newSpellID;
            newSpell.SpellLineID = newTypeID;

            var summ = mods.Summoning ?? new SpellModifierStructure.SummoningProfile();

            // Base fields
            newSpell.ManaCost = SharedHelperScripts.ScaleUShort(newSpell.ManaCost, summ.ManaCostMult);
            newSpell.CastTime = SharedHelperScripts.ScaleUInt(newSpell.CastTime, summ.CastTimeMult);
            newSpell.RecastTime = SharedHelperScripts.ScaleUInt(newSpell.RecastTime, summ.RecastTimeMult);

            // Param rewrites: UnitID + tick interval + mana per tick
            ApplySummoningParamMods(ref newSpell, baseSpell.SpellLineID, cls.SpellLineID, summ, mods, unitCloneMap, gd);

            // Sub-effect recursion (keep supported)
            if (cls.HasSubEffect && cls.SubEffectParamIndex >= 0 && cls.SubEffectSpellID != 0)
            {
                gd = CreateSpellVariant_Internal(gd, cls.SubEffectSpellID, mods, visited, spellCloneMap, unitCloneMap, out ushort newSubSpellID);
                SharedHelperScripts.SetParamU32(ref newSpell, cls.SubEffectParamIndex, newSubSpellID);
            }

            spellCat.Items.Add(newSpell);

            spellCloneMap[baseSpellID] = newSpellID;
            return gd;
        }

        public static void ApplySummoningParamMods(
            ref Category2002Item spell,
            ushort baseSpellLineID,
            ushort currentSpellLineID,
            SpellModifierStructure.SummoningProfile summ,
            SpellModifierStructure mods,
            Dictionary<ushort, ushort> unitCloneMap,
            SFGameDataNew gd
        )
        {
            string[] labels = SFSpellDescriptor.get(baseSpellLineID) ?? Array.Empty<string>();

            for (int i = 0; i < 10; i++)
            {
                uint v = SharedHelperScripts.GetParamU32(ref spell, i);
                if (v == 0) continue;

                string lab = (i < labels.Length ? labels[i] : "") ?? "";
                string l = lab.Trim().ToLowerInvariant();

                if (l.Contains("unused") || l.Contains("<unknown>"))
                    continue;

                // 1) Summoned unit ID -> create unit variant and rewire
                if (IsSummonUnitIdLabel(l))
                {
                    ushort baseUnitID = (ushort)v;
                    ushort newUnitID = ResolveOrCreateSummonedUnitVariant(gd, baseUnitID, mods, summ, unitCloneMap);
                    SharedHelperScripts.SetParamU32(ref spell, i, newUnitID);
                    continue;
                }

                // 2) Time between ticks
                if (IsTickIntervalLabel(l))
                {
                    uint nv = SharedHelperScripts.ScaleUInt(v, summ.TickIntervalMult);
                    SharedHelperScripts.SetParamU32(ref spell, i, nv);
                    continue;
                }

                // 3) Mana per tick
                if (IsManaPerTickLabel(l))
                {
                    uint nv = SharedHelperScripts.ScaleUInt(v, summ.ManaPerTickMult);
                    SharedHelperScripts.SetParamU32(ref spell, i, nv);
                    continue;
                }
            }
        }

        public static bool IsSummonUnitIdLabel(string labelLower)
        {
            // Typical descriptor patterns
            return labelLower.Contains("unit id") ||
                   labelLower.Contains("creature id") ||
                   labelLower.Contains("summon") && labelLower.Contains("id");
        }

        public static bool IsTickIntervalLabel(string labelLower)
        {
            return labelLower.Contains("time between") ||
                   labelLower.Contains("between ticks") ||
                   labelLower.Contains("tick interval");
        }

        public static bool IsManaPerTickLabel(string labelLower)
        {
            // You can tighten once you see exact descriptor wording in your dump
            return (labelLower.Contains("mana") && labelLower.Contains("tick")) ||
                   labelLower.Contains("mana per");
        }

        public static ushort ResolveOrCreateSummonedUnitVariant(
            SFGameDataNew gd,
            ushort baseUnitID,
            SpellModifierStructure mods,
            SpellModifierStructure.SummoningProfile summ,
            Dictionary<ushort, ushort> unitCloneMap
        )
        {
            if (unitCloneMap.TryGetValue(baseUnitID, out ushort cached))
                return cached;

            var mobMod = summ.SummonedMobModifier;
            // disabled because it throws error - this is not nullable type, hopefully this debug will not be required.
            //if (mobMod == null)
            //    throw new Exception($"Summoning variant requested but SummonedMobModifier is null (Spell suffix {mods.Suffix}).");

            // Inherit suffix if requested
            if (summ.InheritSuffixToSummon && string.IsNullOrWhiteSpace(mobMod.Suffix))
                mobMod.Suffix = mods.Suffix;

            gd = UnitVarianting.CreateUnitVariant(gd, baseUnitID, mobMod, out ushort newUnitID);

            unitCloneMap[baseUnitID] = newUnitID;
            return newUnitID;
        }


        // -----------------------------
        // Profile selection for current SpellModifierStructure
        // -----------------------------
        private static SpellModifierStructure.DirectProfile ResolveProfile(
            SpellModifierStructure mods,
            ushort spellLineID,
            DirectSpellArchetype archetype
        )
        {
            // Optional override support
            if (mods.ArchetypeOverrideBySpellLineID != null &&
                mods.ArchetypeOverrideBySpellLineID.TryGetValue(spellLineID, out var forced))
            {
                archetype = forced;
            }

            return archetype switch
            {
                DirectSpellArchetype.DirectDamage => mods.Direct.DirectDamage,
                DirectSpellArchetype.DamageOverTime => mods.Direct.DamageOverTime,
                DirectSpellArchetype.Healing => mods.Direct.Healing,
                DirectSpellArchetype.BuffDebuff => mods.Direct.BuffDebuff,
                DirectSpellArchetype.CrowdControl => mods.Direct.CrowdControl,
                _ => mods.Direct.Utility,
            };
        }

        // -----------------------------
        // Param scaling: label -> family multiplier OR level-cap additive
        // -----------------------------
        public static void ApplyParamMultipliersByLabel(
            ref Category2002Item spell,
            ushort baseSpellLineID,
            ushort currentSpellLineID,
            SpellModifierStructure.DirectProfile profile,
            SpellModifierStructure mods
        )
        {
            string[] labels = SFSpellDescriptor.get(baseSpellLineID) ?? Array.Empty<string>();

            int levelCapAdd = profile.LevelCapAdd;
            if (mods.LevelCapAddOverrideBySpellLineID != null &&
                mods.LevelCapAddOverrideBySpellLineID.TryGetValue(currentSpellLineID, out int ov))
            {
                levelCapAdd = ov;
            }

            for (int i = 0; i < 10; i++)
            {
                uint v = SharedHelperScripts.GetParamU32(ref spell, i);
                if (v == 0) continue;

                string lab = (i < labels.Length ? labels[i] : "") ?? "";
                string l = lab.Trim().ToLowerInvariant();

                if (l.Contains("unused") || l.Contains("<unknown>"))
                    continue;

                // Additive for "max level affected" style params (Cure Poison / Cure Disease / Petrify etc.)
                if (levelCapAdd != 0 &&
                    (l.Contains("max level") || l.Contains("maximum level") || l.Contains("level affected")))
                {
                    ulong nv = (ulong)v + (ulong)Math.Max(0, levelCapAdd);
                    if (nv > uint.MaxValue) nv = uint.MaxValue;
                    SharedHelperScripts.SetParamU32(ref spell, i, (uint)nv);
                    continue;
                }

                float m =
                    (l.Contains("damage")) ? profile.DamageMult :
                    (l.Contains("heal")) ? profile.HealMult :
                    (l.Contains("duration")) ? profile.DurationMult :
                    (l.Contains("tick count") || l.Contains("ticks")) ? profile.TickCountMult :
                    (l.Contains("time between") || l.Contains("between ticks") || l.Contains("tick interval")) ? profile.TickIntervalMult :
                    (l.Contains("radius") || l.Contains("area")) ? profile.RadiusMult :
                    (l.Contains("chance")) ? profile.ChanceMult :
                    (l.Contains("%") || l.Contains("percent")) ? profile.PercentMult :
                    profile.GenericParamMult;

                if (m != 1.0f)
                    SharedHelperScripts.SetParamU32(ref spell, i, SharedHelperScripts.ScaleUInt(v, m));
            }
        }

        // -----------------------------
        // Your existing classifier (unchanged)
        // -----------------------------
        public static SpellClassification ClassifySpellUnified(
            SFGameDataNew gd,
            Category2002Item spell,
            int languageId,
            HashSet<ushort> blacklist
        )
        {
            ushort lineId = spell.SpellLineID;

            var c = new SpellClassification
            {
                SpellID = spell.SpellID,
                SpellLineID = lineId,
                Name = SharedHelperScripts.GetSpellNameByLineID(gd, lineId, languageId) ?? ""
            };

            c.IsBlacklisted = blacklist != null && blacklist.Contains(lineId);
            if (c.IsBlacklisted)
            {
                c.MainCategory = SpellMainCategory.Blacklisted;
                return c;
            }

            bool hasName = !string.IsNullOrWhiteSpace(c.Name) && !c.Name.Contains("<No TextID>", StringComparison.OrdinalIgnoreCase);

            string[] labels = SFSpellDescriptor.get(lineId) ?? Array.Empty<string>();
            uint[] values = SharedHelperScripts.ReadParamsU32(spell);

            bool unusedNonZero = false;
            var sigParts = new List<string>();

            for (int i = 0; i < 10 && i < labels.Length; i++)
            {
                string lab = (labels[i] ?? "").Trim().ToLowerInvariant();
                uint val = values[i];

                if (lab.Contains("sub-effect") || lab.Contains("sub effect"))
                {
                    if (val != 0)
                    {
                        c.HasSubEffect = true;
                        c.SubEffectParamIndex = i;
                        c.SubEffectSpellID = (ushort)val;
                    }
                    sigParts.Add("SubEffect");
                    continue;
                }

                if (lab.Contains("unit id") || lab.Contains("summon") || lab.Contains("creature id"))
                    sigParts.Add("SummonUnit");

                if (lab.Contains("damage")) sigParts.Add("Damage");
                if (lab.Contains("heal")) sigParts.Add("Heal");
                if (lab.Contains("duration")) sigParts.Add("Duration");
                if (lab.Contains("tick count") || lab.Contains("ticks")) sigParts.Add("TickCount");
                if (lab.Contains("time between") || lab.Contains("between ticks") || lab.Contains("tick interval")) sigParts.Add("TickInterval");
                if (lab.Contains("radius") || lab.Contains("area")) sigParts.Add("Radius");
                if (lab.Contains("%") || lab.Contains("percent")) sigParts.Add("Percent");
                if (lab.Contains("chance")) sigParts.Add("Chance");
                if (lab.Contains("unused") && val != 0) unusedNonZero = true;
            }

            c.FeatureSignature = string.Join("+", sigParts.Distinct().OrderBy(x => x, StringComparer.Ordinal));

            if (!hasName)
                c.MainCategory = SpellMainCategory.DummyOrNoName;
            else if (c.FeatureSignature.Contains("SummonUnit"))
                c.MainCategory = SpellMainCategory.Summoning;
            else if (unusedNonZero)
                c.MainCategory = SpellMainCategory.SpecialCase;
            else
                c.MainCategory = SpellMainCategory.DirectLike;

            if (c.MainCategory == SpellMainCategory.DirectLike)
            {
                bool dmg = c.FeatureSignature.Contains("Damage");
                bool heal = c.FeatureSignature.Contains("Heal");
                bool dot = dmg && (c.FeatureSignature.Contains("TickCount") || c.FeatureSignature.Contains("TickInterval") || c.FeatureSignature.Contains("Duration"));

                if (heal) c.DirectArchetype = DirectSpellArchetype.Healing;
                else if (dot) c.DirectArchetype = DirectSpellArchetype.DamageOverTime;
                else if (dmg) c.DirectArchetype = DirectSpellArchetype.DirectDamage;
                else c.DirectArchetype = DirectSpellArchetype.Utility;
            }

            return c;
        }


    }
}
