# Spellforce Oblivion – `variant_config.json` guide

This guide explains the configuration file in plain language.
It is written for players and mod users, not programmers.

---

## What this file does

`variant_config.json` is the main settings file used by the tool.
It controls:

- optional gameplay tweaks,
- enemy/item/spell variant generation,
- RTS spawn behaviour,
- hero compensation buffs,
- flat global stat multipliers.

If the file does not exist yet, the tool creates it automatically with default values.

---

## Before you edit anything

1. Make a backup copy of the file.
2. Change only a few values at a time.
3. Save the file, run the tool, and test in game.
4. If something becomes too strong or too strange, restore your backup.

---

## How to read the numbers

Most values in the file are **multipliers**.

- `1.0` = no change
- more than `1.0` = stronger / more / faster / more expensive, depending on the field
- less than `1.0` = weaker / less / slower / cheaper, depending on the field

Examples:

- `StrengthMod: 1.5` = 50% more Strength
- `CastTimeMult: 0.7` = 30% faster casting
- `ArmyDiscountValue: 0.5` = army costs are cut in half

---

## Quick-start: safest settings to edit first

If you are not technical, these are the easiest and safest settings to adjust:

- `ArmyDiscountFlag`
- `ArmyDiscountValue`
- `RTSSpawnSize`
- `RTSSpawnFrequency`
- `RTSSpawnWeights`
- `mobTierTable` suffixes and multipliers
- `itemTierTable` suffixes and multipliers
- `spellTierTable` suffix and broad archetype multipliers

Leave the advanced per-spell exception fields empty unless you know exactly why you need them.

---

## Main option groups

## 1. Extra gameplay toggles

### `AttributeFreedomFlag`
Turns on the “free attributes” tweak.
Attribute max value is not restricted by level and skills do not require attributes

### `BringBackTrousers`
Allows robes and trousers to be used together.

### `StoningIsOutdated`
Cursed guardians in Mulandir will turn enemies to stone for only a fraction of time

### `IAmOmnidexterous`
Makes two-handed weapons behave like one-handed weapons.

### `DepromotePlayerUnitSpells`
Controls whether player-controlled units keep original spell versions instead of promoted variant spell versions.

### `DepromoteSummonedUnitSpells`
Controls whether summoned units keep original spell versions instead of using promoted spell variants that match their summon level.

### `VariantInitMobs`
Controls whether the tool also variants the units listed in initial spawn tables.
Turn this off if you want only stronger spawned variants later, but not stronger units in initial placement data.

### `DontVariantFood`
Prevents animals / food-type units from being varianted.
Useful if varianting animals causes gameplay problems, for example for hunters.

### `ArmyDiscountFlag`
Turns army/building cost reduction on or off.

### `ArmyDiscountValue`
Sets the army/building cost multiplier.
Examples:

- `1.0` = no discount
- `0.5` = half cost
- `0.25` = quarter cost
- `2.0` = double cost

### `ApplyFlatUnitsMod`
Turns on the global flat modifier for all units.
This does **not** create new variants. It directly scales the original data.

### `ApplyFlatItemsMod`
Turns on the global flat modifier for equippable items.
This also works in place, without creating new variant copies.

### `ApplyFlatSpellMod`
Turns on the global flat modifier for spells.
This is powerful and can affect the whole game balance quickly.

---

## 2. RTS spawn settings

### `RTSSpawnSize`
Multiplier for RTS clan size / group size.
Higher values mean bigger groups.

### `RTSSpawnFrequency`
Multiplier used for spawn timing.
Change carefully and test, because very aggressive values can flood the map.

### `RTSSpawnWeights`
Controls the relative mix of spawn strength tiers.

Example:

```json
"RTSSpawnWeights": [3, 2, 1]
```

This means the spawn mix is weighted toward weaker entries:

- 3 parts base/original units
- 2 parts mid-tier variants
- 1 part top-tier variants

A more dangerous example:

```json
"RTSSpawnWeights": [1, 2, 3]
```

would shift the balance toward stronger variants.

---

## 3. XP scaling

### `HungerForBurgerFlag`
Turns the XP-related tweak on or off.

### `HungerForBurgerHunger`
Controls how strongly the maximum XP gained from a mob is increased.
Higher values mean more XP scaling. Relation can be approximed as square root. Putting 2 will result around 140% max XP gained from mob, 4 will result in around 200%

---

## 4. Rune hero compensation buffs

### `KeepThemRelevantDammit`
Turns on special buffs for rune heroes with blocked or limited equipment, e.g. Gizmo or Blades

### `HeroModifierLimitedEQ`
Modifier applied to heroes with **limited** equipment slots.

### `HeroModifierNoEQ`
Modifier applied to heroes with **very restricted / no** equipment slots.

These blocks use the same stat fields as enemy mob variants:

- `StrengthMod`
- `StaminaMod`
- `AgilityMod`
- `DexterityMod`
- `CharismaMod`
- `IntelligenceMod`
- `WisdomMod`
- `ResistancesMod`
- `WalkMod`
- `FightMod`
- `CastMod`
- `Suffix`

For hero compensation blocks, leave `Suffix` empty unless you intentionally want renamed units.

---

## 5. Enemy variant tiers – `mobTierTable`

This is the list of enemy tiers the tool creates.
The order matters:

- first entry = lower extra tier
- later entries = stronger tiers

In your current file there are two enemy tiers:

- `Elite`
- `Oblivion`

Example meaning:

```json
{
  "StrengthMod": 1.4,
  "StaminaMod": 3.0,
  "Suffix": "Elite"
}
```

This creates a stronger enemy copy with `Elite` added to its name.

### Shared mob fields

- `StrengthMod` – melee / physical strength scaling
- `StaminaMod` – survivability scaling
- `AgilityMod` – agility scaling
- `DexterityMod` – dexterity scaling
- `CharismaMod` – charisma scaling
- `IntelligenceMod` – intelligence scaling
- `WisdomMod` – wisdom scaling
- `ResistancesMod` – fire/ice/mind/black resistance scaling
- `WalkMod` – movement speed scaling
- `FightMod` – attack speed / combat pace scaling
- `CastMod` – spell casting speed scaling
- `Suffix` – text added to the generated unit name

### Safe editing advice for mob tiers

Good beginner changes:

- adjust only `StaminaMod`, `StrengthMod`, and `Suffix`
- keep speed values close to `1.0`
- avoid extreme values like `5.0` on many different fields at once

---

## 6. Item variant tiers – `itemTierTable`

This is the list of generated equipment tiers.
Only equippable items are affected.

In your current file there are two item tiers:

- `Masterwork`
- `Legendary`

### Shared item fields

- `ArmorMod` – armor value
- `StrengthMod`, `StaminaMod`, `AgilityMod`, `DexterityMod`, `CharismaMod`, `IntelligenceMod`, `WisdomMod` – attribute bonuses
- `ResistancesMod` – resistance bonuses
- `WalkMod`, `FightMod`, `CastMod` – speed bonuses
- `HealthMod`, `ManaMod` – health/mana bonuses
- `WeaponSpeedMod` – weapon speed value
- `MinDamageMod`, `MaxDamageMod` – weapon damage range
- `MaxRangeMod` – weapon range
- `SellMod` – sell value
- `BuyMod` – buy value
- `Suffix` – text added to the generated item name

### Safe editing advice for item tiers

Best first edits:

- `MinDamageMod`
- `MaxDamageMod`
- `ArmorMod`
- `HealthMod`
- `ManaMod`
- `BuyMod`
- `SellMod`
- `Suffix`

Be careful with:

- `WeaponSpeedMod`
- `MaxRangeMod`
- stacking many attribute multipliers at once

---

## 7. Spell variant tiers – `spellTierTable`

This is the spell-variant list.
Spell variants are created for supported spells, and the tool also creates matching scroll / spellbook item entries for those new spell variants.

In your current file there is one spell tier:

- `Arch`

### Top-level spell fields

#### `Suffix`
Text added to the spell variant name.

#### `BuyPriceMult` and `SellPriceMult`
Price scaling for the generated spell scroll and spellbook items.

#### `SpellLineBlacklist`
List of spell line IDs that must not be varianted.
Leave empty unless you know specific problematic IDs.

#### `ArchetypeOverrideBySpellLineID`
Advanced exception list for forcing a spell line into a chosen archetype.
Best left empty for casual users.

#### `LevelCapAddOverrideBySpellLineID`
Advanced exception list for level-cap changes on specific spell lines.
Best left empty unless you are intentionally tuning edge cases.

---

## 8. Direct spell archetypes

Inside `spellTierTable`, direct-like spells are grouped into:

- `DirectDamage`
- `DamageOverTime`
- `Healing`
- `BuffDebuff`
- `CrowdControl`
- `Utility`

Each archetype block uses mostly the same fields:

- `ManaCostMult`
- `CastTimeMult`
- `RecastTimeMult`
- `MinRangeMult`
- `MaxRangeMult`
- `DamageMult`
- `HealMult`
- `DurationMult`
- `TickCountMult`
- `TickIntervalMult`
- `RadiusMult`
- `PercentMult`
- `ChanceMult`
- `LevelCapAdd`
- `GenericParamMult`

### What these usually mean

- `ManaCostMult` – mana cost
- `CastTimeMult` – cast time
- `RecastTimeMult` – cooldown / recast
- `MinRangeMult`, `MaxRangeMult` – range
- `DamageMult` – damage
- `HealMult` – healing
- `DurationMult` – duration
- `TickCountMult` – number of ticks
- `TickIntervalMult` – time between ticks
- `RadiusMult` – area size
- `PercentMult` – percentage-based effect strength
- `ChanceMult` – chance-based effect strength
- `LevelCapAdd` – additive level-cap adjustment
- `GenericParamMult` – catch-all scaling for parameters not covered elsewhere

### Beginner advice for spells

The easiest meaningful settings to change are:

- `DamageMult`
- `HealMult`
- `ManaCostMult`
- `RecastTimeMult`
- `DurationMult`
- `Suffix`

Only touch `GenericParamMult`, override dictionaries, or blacklist IDs if you are doing targeted balance work.

---

## 9. Summoning spell settings

Inside each spell tier there is also a `Summoning` block.
This handles summoning-specific spell scaling.

Fields:

- `ManaCostMult`
- `CastTimeMult`
- `RecastTimeMult`
- `TickIntervalMult`
- `ManaPerTickMult`
- `SummonedMobModifier`
- `InheritSuffixToSummon`

### What they mean

- `ManaCostMult` – summon spell mana cost
- `CastTimeMult` – summon cast time
- `RecastTimeMult` – summon cooldown
- `TickIntervalMult` – interval between upkeep ticks, if the spell uses them
- `ManaPerTickMult` – upkeep mana cost, if the spell uses it
- `SummonedMobModifier` – stat template applied to the summoned creature itself
- `InheritSuffixToSummon` – if true, the summon can inherit the spell tier name when no explicit summon suffix is set

### Safe editing advice for summons

Good first edits:

- `ManaCostMult`
- `RecastTimeMult`
- `SummonedMobModifier.StaminaMod`
- `SummonedMobModifier.StrengthMod`
- `SummonedMobModifier.Suffix`

---

## 10. Flat global modifiers

These are **global in-place modifiers**.
Unlike tier tables, they do not create new named variants. They directly scale the original data.

### `FlatUnitMod`
Applies to unit stats globally.

### `FlatItemMod`
Applies to equippable items globally.

### `FlatSummonModifier`
Used as the global summon-unit modifier in flat spell handling.

### `FlatSpellModifier`
Applies to spells globally.
It has the same structure as a spell tier, but usually without suffix naming.

### Important warning

Flat modifiers are very powerful because they affect the whole game baseline.
Use them for broad rebalance passes, not for fine tuning one class of content.

---

## 11. Empty arrays and disabling systems

You can disable entire variant families by using empty arrays:

- empty `mobTierTable` = no enemy variants
- empty `itemTierTable` = no item variants
- empty `spellTierTable` = no spell variants

This is useful when you want to test only one part of the system at a time.