# Player Core Scripts Overview

These scripts power the player character in the demo: movement, health/mana, melee hit detection, and respawn UI. They serve as a baseline controller you can extend or replace.

## Components

| File | Purpose |
| --- | --- |
| `GenericTopDownController.cs` | 2D top-down movement controller supporting WASD + gamepad input, sprint, crouch, context-aware animations, and hooks for ability steps (e.g., stealth toggles). Manages Rigidbody movement, rotation, and animation parameters. |
| `PlayerHealth.cs` | Implements `EnemyAI.IDamageable` for the player. Handles damage intake, invulnerability frames, shield absorption, knockback pauses, death/respawn flow, and HUD hooks. Broadcasts `OnPlayerDied` / `OnPlayerRespawned` events. |
| `PlayerMana.cs` | Simple mana pool with regen, cost checks, and events for UI updates. Abilities consume or restore mana through this component. |
| `PlayerExperience.cs` | Tracks player level, XP requirements, grants, and level-up FX. Offers static helpers (`GrantStatic`) so other systems can award XP anywhere. |
| `PlayerStats.cs` | Central stat sheet (strength, crit chance, movement modifiers). Provides getters used by abilities/projectiles and exposes upgrade hooks when leveling or equipping gear. |
| `PlayerMeleeHitbox.cs` | Controls melee weapon swing arcs. Handles attack colliders, crit chance/multiplier, integrates with ability steps, and exposes damage values to projectile logic. |
| `PlayerManaTextDisplay.cs` | Optional UI binder that displays current/maximum mana as text alongside the mana bar. |
| `RespawnPanelController.cs` | UI panel shown on death. Lets the player respawn, fades UI, and hides itself once `PlayerHealth` finishes the revival process. |

## Extending

* Swap the input bindings in `GenericTopDownController` to match your project (Input System / legacy).
* Hook `PlayerHealth` events to other systems (e.g., slow-motion on death, analytics).
* Replace `PlayerMeleeHitbox` swing data with your weapon system while keeping the interface for projectiles/abilities.

Use this folder as a starting point for your own playable character or as reference when integrating the tileset into an existing controller.***
