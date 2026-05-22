# AI Overview

AI scripts cover hostile enemies, companions, neutral NPCs, and automated turrets. They share common health/damage interfaces and pathfinding utilities from the Utility folder.

## Key behaviours

| File | Purpose |
| --- | --- |
| `EnemyAI.cs` | Feature-rich enemy brain for both melee and ranged archetypes. Handles roaming, aggro/leash logic, pathfinding, ranged/melee attack routines, stealth detection, tile stuck handling, and integration with abilities/projectiles. |
| `EnemyHealth2D.cs` | Health component used by all enemies. Supports boss health bars, ally alerts, knockback, hit flash, regeneration, loot trigger hooks, and event callbacks (`OnDamageTaken`, `OnDied`). |
| `CompanionAI.cs` + `CompanionHealth.cs` | AI for allied companions (neutrals that fight alongside the player). Handles follow/stay commands, target selection, ability usage, and shares the same damage interface for healing/buffs. |
| `NeutralNpcAI.cs` | Non-hostile NPC behaviour for townsfolk/quest givers. Supports idle routines, flee logic, and aggro response when attacked. |
| `TurretAI.cs` | Stationary AI that scans a radius, aims across eight directions, animates barrel recoil, tracks health/companion-style UI, and fires Projectile2D-based shots at enemies. |
| `DungeonPortalLink.cs` | (from previously documented folder) ties AI spawn/despawn to dungeon portals; keep here if you need to control AI when entering/leaving procedural runs. |

## Usage

* Attach `EnemyAI` + `EnemyHealth2D` to enemy prefabs. Configure stats, attack settings, loot, and boss toggles.
* Add `CompanionAI` to friendly NPCs and control them via the companion command panel.
* Use `NeutralNpcAI` for ambient townsfolk who may become hostile or flee based on conditions.
* Place `TurretAI` prefabs where you need automated defense; wire up projectile prefabs and optional lifetime bars.

All AI scripts rely on shared systems like `TilemapPathfinder`, `Projectile2D`, ability steps, and resource/XP managers—making it easy to keep consistency between player and AI behaviours.***
