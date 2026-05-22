# Resource System Overview

Resource definitions and managers live here: ScriptableObjects for ores/herbs, runtime helpers for tracking currencies, and containers used by crafting/building systems.

## Main components

| File | Purpose |
| --- | --- |
| `ResourceTypeDef.cs` | ScriptableObject describing a single resource (icon, colour, pickup text, stack size, etc.). All `*.asset` files in this folder (Wood, Iron, Coin, etc.) are instances of this definition. |
| `ResourceDatabase.cs` | Registry of every `ResourceTypeDef`. Provides lookup helpers so menus can resolve string ids to actual assets. |
| `ResourceSet.cs` | Lightweight serializable struct storing a list of `ResourceAmount` entries. Used for crafting/build costs, quest rewards, etc. Includes helpers for add/remove/scaling. |
| `DynamicResourceManager.cs` | Runtime service that tracks the player’s resource balances. Handles spend/gain operations, raises events for HUDs, and integrates with `GameBalanceManager` multipliers. |

## Usage

1. Create new resources by duplicating a `ResourceTypeDef` asset (set icons/rarities).
2. Add the new asset to `ResourceDatabase`.
3. Reference those types in `ResourceSet` fields across crafting/building scripts.
4. Call `DynamicResourceManager.TrySpend` / `AddResource` to modify totals when the player crafts, gathers, or trades.

Keeping all resource metadata in ScriptableObjects makes it easy to expand the economy or localise resource names without touching code.***
