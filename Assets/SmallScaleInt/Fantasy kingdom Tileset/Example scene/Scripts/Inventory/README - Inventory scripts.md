# Inventory & Loot Overview

Inventory scripts power the demo’s loot drops and backpack UI. They demonstrate how GearItems enter/leave the player inventory, how pickups spawn, and how the UI responds to item selection.

## Components

| File | Purpose |
| --- | --- |
| `PlayerInventory.cs` | Core data container. Tracks owned `GearItem` instances, raises `InventoryChanged` events, and exposes helpers for add/remove/equip logic. |
| `InventoryUIController.cs` | Manages the backpack window: populates `InventoryItemButton` entries, handles selection, integrates with crafting/build menus (closing them when inventory opens), and drives tooltip text. |
| `InventoryItemButton.cs` | UI element representing a single inventory entry. Displays icon/rarity, forwards click/hover events, and highlights the equipped item. |
| `LootPickup.cs` | World-space loot prefab logic. Handles hover outlines, click-to-pickup, rare beam visuals, and awarding gear to `PlayerInventory`. |
| `EnemyRandomGearDropper.cs` | Component added to enemies to spawn loot pickups when they die. Supports guaranteed drops, random pools, and integrates with `GameBalanceManager` multipliers. |
| `LootDropDefinition.cs` | ScriptableObject describing weighted loot tables that `EnemyRandomGearDropper` can reference. |

## Typical flow

1. Player defeats an enemy with `EnemyRandomGearDropper` → `LootPickup` prefabs spawn using the configured drop table.
2. When the player collects the pickup, `PlayerInventory` receives the `GearItem` (and notifies UI listeners).
3. `InventoryUIController` rebuilds buttons via `InventoryItemButton`, allowing the player to equip or inspect items.

The scripts are designed to be modular—swap out the UI layer, feed drops from crafting, or extend `PlayerInventory` persistence while keeping the pick/drop flow intact.***
