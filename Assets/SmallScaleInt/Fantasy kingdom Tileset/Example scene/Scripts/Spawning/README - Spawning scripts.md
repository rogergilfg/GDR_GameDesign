# Spawning Overview

This folder currently houses `EnemySpawnPoint`, a flexible spawner used throughout the demo for turrets, horde events, and timed encounters.

## EnemySpawnPoint.cs

Features:

* **Prefab lists** – Separate regular vs. elite prefab pools plus per-wave guaranteed spawn lists (for bosses/uniques).
* **Spawn controls** – Initial count, max simultaneous enemies, respawn timers, spawn radius, randomised counts, activation radius.
* **Wave/horde mode** – Multi-wave definitions with delays, looping, announcements (floating combat text), completion loot, and forced aggro toggles.
* **Runtime tracking** – Registers each enemy’s `EnemyHealth2D`, removes listeners on death, and supports auto-respawns.
* **Boss integration** – Alerts spawned enemies immediately (`forceAggroOnSpawn`) and disables return-to-post when used as turret/boss spawners.

### Usage

1. Drop an `EnemySpawnPoint` prefab into your scene, assign regular/elite lists and spawn radius.
2. Configure waves (counts, guaranteed prefabs, delays) or use the simple initial spawn mode.
3. Hook up activation (auto, proximity, or manual via script).
4. Optionally assign completion loot (guaranteed + random) to reward the player after the final wave.

Extend the script to add custom behaviours (e.g., random patrol assignments or spawn VFX) by modifying the `SpawnPrefabInstance` helper.***
