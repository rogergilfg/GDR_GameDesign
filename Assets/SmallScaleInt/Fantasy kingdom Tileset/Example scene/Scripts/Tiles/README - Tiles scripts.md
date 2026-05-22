# Tile & Destruction System Overview

Tile scripts define destructible tiles, databases, and runtime destruction logic. Building/crafting systems consume these assets to spawn breakable environments.

## Components

| File | Purpose |
| --- | --- |
| `DestructibleTileData.cs` | ScriptableObject describing a destructible tile: tile references for ground/wall/object maps, HP, gear-drop settings, category, rebuild prefab, collider flags, etc. Tiles can now drop guaranteed gear or random gear pulled from the shared `GearItemDatabase`/`LootPickup` prefab. |
| `Destructible Tile Database.asset / DestructibleTileDatabase.cs` | Registry of every `DestructibleTileData`. Used by build menus, dungeon generation, and destruction manager to look up definitions from tile coordinates. |
| `TileColliderAutoPlacer.cs` | Editor/runtime helper that mirrors visual tilemaps into collider tilemaps automatically based on naming conventions and special rules. |
| `TileDestructionManager.cs` | Runtime singleton that tracks tile HP, handles damage requests (`HitCircle`, `HitCone`, `TryHitAtWorld`), spawns VFX, removes colliders/shadows, awards XP/resources, and now spawns `LootPickup` gear drops using the same logic as `EnemyRandomGearDropper`. |

## Workflow

1. Author tiles in `DestructibleTileData` (set sprites, HP, optional gear drops, rubble, category). When using the batch-author window, you can list alias tile names so multiple sprites inherit the same config automatically.
2. Add them to the `Destructible Tile Database` so systems can find them by cell.
3. Call `TileDestructionManager` from attacks/projectiles/environment to damage tiles.
4. Build menus and dungeon generator reference the same definitions to ensure placement/destruction stay consistent.

Because tile behaviour lives in ScriptableObjects, you can quickly tune HP/loot and have every system reflect the change without editing code.***
