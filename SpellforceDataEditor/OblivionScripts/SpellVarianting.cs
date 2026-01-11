using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using SpellforceDataEditor.OblivionScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SpellforceDataEditor.special_forms.SpelllforceCFFEditor;

namespace SpellforceDataEditor.OblivionScripts
{
    public class SpellVarianting
    {
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
                Name = OblivionScripts.SharedHelperScripts.GetSpellNameByLineID(gd, lineId, languageId) ?? ""
            };

            c.IsBlacklisted = blacklist != null && blacklist.Contains(lineId);
            if (c.IsBlacklisted)
            {
                c.MainCategory = SpellMainCategory.Blacklisted;
                return c;
            }

            bool hasName = !string.IsNullOrWhiteSpace(c.Name) && !c.Name.Contains("<No TextID>", StringComparison.OrdinalIgnoreCase);

            // Detect subeffect param by label + non-zero value
            string[] labels = SFSpellDescriptor.get(lineId) ?? Array.Empty<string>();
            uint[] values = OblivionScripts.SharedHelperScripts.ReadParamsU32(spell);

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
                        c.SubEffectSpellID = (ushort)val; // subeffect points to SpellID in your specimens
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

            // Main category selection
            if (!hasName)
                c.MainCategory = SpellMainCategory.DummyOrNoName;
            else if (c.FeatureSignature.Contains("SummonUnit"))
                c.MainCategory = SpellMainCategory.Summoning;
            else if (unusedNonZero)
                c.MainCategory = SpellMainCategory.SpecialCase;
            else
                c.MainCategory = SpellMainCategory.DirectLike; // includes HasSubEffect

            // Direct archetype selection (only when DirectLike)
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

        // ============================================================================================ Spello Variantio Creatio ====================================================================
        public static SFGameDataNew CreateSpellVariant(
            SFGameDataNew gd,
            ushort baseSpellID,
            SpellMultipliers multipliers,
            out ushort newSpellID
        )
        {
            if (gd == null) throw new ArgumentNullException(nameof(gd));
            if (multipliers == null) throw new ArgumentNullException(nameof(multipliers));

            // recursion guards + “same subeffect reused within one variant build” map
            var visited = new HashSet<ushort>();
            var cloneMap = new Dictionary<ushort, ushort>(); // baseSpellID -> newSpellID

            return CreateSpellVariant_Internal(gd, baseSpellID, multipliers, visited, cloneMap, out newSpellID);
        }

        public static SFGameDataNew CreateSpellVariant_Internal(
            SFGameDataNew gd,
            ushort baseSpellID,
            SpellMultipliers multipliers,
            HashSet<ushort> visited,
            Dictionary<ushort, ushort> cloneMap,
            out ushort newSpellID
        )
        {
            // Already cloned during this build?
            if (cloneMap.TryGetValue(baseSpellID, out var existing))
            {
                newSpellID = existing;
                return gd;
            }

            // Cycle protection (defensive)
            if (!visited.Add(baseSpellID))
                throw new Exception($"Cycle detected while cloning subeffects at SpellID {baseSpellID}.");

            var spellCat = gd.c2002;
            var typeCat = gd.c2054;
            var locCat = gd.c2016;

            // 1) Find base spell (c2002)
            Category2002Item baseSpell = default;
            bool found = false;
            foreach (var s in spellCat.Items)
            {
                if (s.SpellID == baseSpellID)
                {
                    baseSpell = s;
                    found = true;
                    break;
                }
            }
            if (!found) throw new Exception($"Base spell {baseSpellID} not found.");

            // 2) Classify
            var cls = ClassifySpellUnified(gd, baseSpell, SFEngine.Settings.LanguageID, multipliers.SpellLineBlacklist);

            if (cls.MainCategory == SpellMainCategory.Blacklisted)
                throw new Exception($"SpellLineID {cls.SpellLineID} is blacklisted; refusing to generate variant.");

            if (cls.MainCategory != SpellMainCategory.DirectLike)
                throw new Exception($"CreateSpellVariant currently supports DirectLike spells only. Got: {cls.MainCategory}");

            var archetype = cls.DirectArchetype ?? DirectSpellArchetype.Utility;
            var profile = multipliers.ResolveDirectProfile(cls.SpellLineID, archetype);

            // 3) Find base type record (c2054) by SpellLineID
            Category2054Item baseType = default;
            found = false;
            foreach (var t in typeCat.Items)
            {
                if (t.SpellLineID == baseSpell.SpellLineID)
                {
                    baseType = t;
                    found = true;
                    break;
                }
            }
            if (!found) throw new Exception($"Spell type {baseSpell.SpellLineID} not found in c2054.");

            // 4) Allocate new IDs (type + spell)
            ushort newTypeID = 0;
            foreach (var t in typeCat.Items)
                if (t.SpellLineID > newTypeID) newTypeID = t.SpellLineID;
            newTypeID++;

            newSpellID = 0;
            foreach (var s in spellCat.Items)
                if (s.SpellID > newSpellID) newSpellID = s.SpellID;
            newSpellID++;

            // 5) Clone localisation for spell name (and optionally description)
            ushort newTextID = OblivionScripts.SharedHelperScripts.CloneLocalisationTextID_512(
                locCat,
                baseType.TextID,
                suffix: multipliers.Suffix,
                appendSuffix: true
            );

            ushort newDescID = baseType.DescriptionID;
            if (multipliers.CloneDescriptionText && baseType.DescriptionID != 0)
            {
                newDescID = OblivionScripts.SharedHelperScripts.CloneLocalisationTextID_512(
                    locCat,
                    baseType.DescriptionID,
                    suffix: multipliers.Suffix,
                    appendSuffix: multipliers.SuffixDescriptionText
                );
            }

            // 6) Clone type (c2054)
            var newType = baseType;
            newType.SpellLineID = newTypeID;
            newType.TextID = newTextID;
            newType.DescriptionID = newDescID;

            typeCat.Items.Add(newType);

            // 7) Clone spell (c2002), retarget SpellLineID to new type
            var newSpell = baseSpell;
            newSpell.SpellID = newSpellID;
            newSpell.SpellLineID = newTypeID;

            // Apply base-field multipliers (keep integer semantics)
            newSpell.ManaCost = OblivionScripts.SharedHelperScripts.ScaleUShort(newSpell.ManaCost, profile.ManaCostMult);
            newSpell.CastTime = OblivionScripts.SharedHelperScripts.ScaleUInt(newSpell.CastTime, profile.CastTimeMult);
            newSpell.RecastTime = OblivionScripts.SharedHelperScripts.ScaleUInt(newSpell.RecastTime, profile.RecastTimeMult);

            newSpell.MinRange = OblivionScripts.SharedHelperScripts.ScaleUShort(newSpell.MinRange, profile.MinRangeMult);
            newSpell.MaxRange = OblivionScripts.SharedHelperScripts.ScaleUShort(newSpell.MaxRange, profile.MaxRangeMult);

            // Apply Params by label-family (using base spell labels is fine; same layout per SpellLine)
            ApplyParamMultipliersByLabel(ref newSpell, baseSpell.SpellLineID, baseSpell.SpellLineID, profile, multipliers);

            // 8) If it has a subeffect, clone that spell too and rewrite param to the NEW sub spell id
            if (cls.HasSubEffect && cls.SubEffectParamIndex >= 0 && cls.SubEffectSpellID != 0)
            {
                gd = CreateSpellVariant_Internal(gd, cls.SubEffectSpellID, multipliers, visited, cloneMap, out ushort newSubSpellID);
                OblivionScripts.SharedHelperScripts.SetParamU32(ref newSpell, cls.SubEffectParamIndex, newSubSpellID);
            }

            // 9) Insert spell
            spellCat.Items.Add(newSpell);

            // 10) Cache and unwind recursion
            cloneMap[baseSpellID] = newSpellID;
            visited.Remove(baseSpellID);

            return gd;
        }

        public static void ApplyParamMultipliersByLabel(
            ref Category2002Item spell,
            ushort baseSpellLineID,
            ushort currentSpellLineID, // the spell line being processed (base line id is fine here too)
            SpellMultipliers.DirectProfile profile,
            SpellMultipliers multipliers
        )
        {
            string[] labels = SFSpellDescriptor.get(baseSpellLineID) ?? Array.Empty<string>();

            int levelCapAdd = profile.LevelCapAdd;
            if (multipliers.LevelCapAddOverrideBySpellLineID.TryGetValue(currentSpellLineID, out int ov))
                levelCapAdd = ov;

            for (int i = 0; i < 10; i++)
            {
                uint v = SharedHelperScripts.GetParamU32(ref spell, i);
                if (v == 0) continue;

                string lab = (i < labels.Length ? labels[i] : "") ?? "";
                string l = lab.Trim().ToLowerInvariant();

                if (l.Contains("unused") || l.Contains("<unknown>"))
                    continue;

                // -----------------------------
                // Level-cap additive handling
                // -----------------------------
                if (levelCapAdd != 0 &&
                    (l.Contains("max level") || l.Contains("maximum level") || l.Contains("level affected")))
                {
                    ulong nv = (ulong)v + (ulong)Math.Max(0, levelCapAdd);
                    if (nv > uint.MaxValue) nv = uint.MaxValue;
                    SharedHelperScripts.SetParamU32(ref spell, i, (uint)nv);
                    continue;
                }

                // -----------------------------
                // Multiplicative families
                // -----------------------------
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
    }
}
