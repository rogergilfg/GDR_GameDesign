# Gear System Overview

Gear assets drive the player’s equipped visuals, stats, and crafting targets. This folder contains the ScriptableObjects, databases, and runtime managers that keep equipment data in sync with character art.

## Key assets & scripts

| File | Purpose |
| --- | --- |
| `GearType.cs` | Enum of supported slots (Weapon, Head, Chest, etc.). Shared by inventory, crafting, and equipment systems. |
| `GearItem.cs` | ScriptableObject describing one piece of gear: slot, stats, animator override, rarity metadata, crafting costs, drop weights, etc. Designers author new gear by duplicating these assets. |
| `GearItemDatabase.asset / GearItemDatabase.cs` | Central list of every GearItem. Crafting, loot, and UI systems read from this database to populate menus. |
| `GearAnimatorSynchronizer.cs` | Keeps auxiliary gear animators in lockstep with the base player animator so armor/weapons play the same state machine and directional blend. |
| `PlayerGearManager.cs` | Handles equipping/unequipping GearItems at runtime, swapping animator controllers per slot, capturing default colors, invoking `GearChanged` events, and syncing with the player animator via `GearAnimatorSynchronizer`. |

## Typical workflow

1. Create GearItem assets (assign sprites, animator overrides, stats).
2. Add them to `GearItemDatabase` and expose them through crafting/loot systems.
3. `PlayerGearManager` listens for equipment changes (inventory, crafting, cheats) and updates the visuals/animators accordingly.
4. Optional helpers like `GearAnimatorSynchronizer` ensure each gear animator mirrors the main controller parameters.

Use these components as-is for the demo or swap in your own inventory logic while reusing the GearItem data model and animator sync utilities.***
