# Utility Scripts Overview

Miscellaneous helpers that are reused across multiple systems live here. They cover VFX lifetimes, damage popups, projectiles, tilepathfinding, roof reveal logic, and more.

## Notable scripts

| File | Purpose |
| --- | --- |
| `AutoDestroyAfter.cs` | Simple component that destroys a GameObject after X seconds (used by VFX). |
| `BoneShard2D.cs` | Controls the physics of bone shard projectiles spawned by enemy deaths. |
| `CloudAnimator.cs`, `GodRayAnimator.cs`, `TreeSwayAnimator.cs` | Ambient visual animators for clouds, god rays, and foliage. |
| `CombatTextManager.cs` / `CombatTextPopup.cs` | Floating text system used for damage, crits, resources, and status announcements. Supports pooling, curves, and the status fade-only option. |
| `DamageFeedback.cs` | Plays global hit stop, screen flash, and shake when the player takes damage. |
| `DestructibleProp2D.cs` | Allows world props (crates, barrels) to take damage and spawn loot/VFX when destroyed. |
| `Interactable.cs` | Base class for world interactables (crafting stations, traders). Handles prompt display, input, and derived interaction logic. |
| `IsometricSpriteSorter.cs` | Ensures isometric sprites sort properly by Y-position. |
| `Projectile2D.cs` | Shared projectile behaviour for players/enemies/turrets. Handles movement, swept collision, tile damage, crit text, etc. |
| `RoofRevealController.cs` / `RoofVisibilityController.cs` | Hides/reveals roof tilemaps when the player enters buildings. |
| `SmoothCameraFollow.cs` | Lightweight camera follow with damping and shake support (used by DamageFeedback). |
| `TilemapPathfinder.cs` | Grid-based pathfinding/cache used by EnemyAI, building placement, and other systems. |

## Usage

Import whichever components you need into your own project; they are deliberately decoupled from scene-specific logic. For example, `Projectile2D` only requires a hit mask and owner transform, while `TilemapPathfinder` can be initialised once and reused by any AI.***
