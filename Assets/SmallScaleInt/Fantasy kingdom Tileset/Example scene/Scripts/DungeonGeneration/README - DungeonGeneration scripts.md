# Dungeon Generation Overview

This folder contains the procedural dungeon sample: ScriptableObject profiles describing room graphs, runtime generators that stamp tiles/prefabs, and helper components for linking portals between overworld ↔ dungeon scenes. Treat it as a reference implementation for grid-based dungeon runs built on Unity Tilemaps.

## Main pieces

| File | Purpose |
| --- | --- |
| `DungeonGenerationProfile.asset / DungeonGenerationProfile.cs` | ScriptableObject describing how to build a dungeon run: room templates, weighted connections, corridor lengths, prop spawn settings, tile palette references, light/VFX toggles, etc. Designers duplicate the asset to define multiple dungeon “biomes.” |
| `DungeonRoomTemplate.cs` + `RoomTemplates/` folder | Prefab-style authoring for individual rooms. Each template stores the ground/wall/object layouts, entrance/exit markers, portal sockets, collectible spawn points, and optional baked prefabs (torches, chests). |
| `DungeonGenerator.cs` | Runtime MonoBehaviour that reads a profile, selects rooms, carves corridors, paints tilemaps, drops props, and instantiates portals to connect back to the overworld or deeper floors. Handles cleanup, collider regeneration, events, and exposes `DungeonRuntimeData` for other systems (enemy spawners, quest hooks). |
| `DungeonPortalLink.cs` | Utility that wires up portal interactions so exiting the dungeon returns the player to the correct scene anchor (e.g., overworld spawn point). |

## Typical workflow

1. Create/duplicate a `DungeonGenerationProfile`, assign Tilemaps, room templates, corridor rules, and prop lists.
2. Author room templates inside `RoomTemplates/` by placing tiles/prefabs, tagging exits, and saving them via `DungeonRoomTemplate`.
3. Drop `DungeonGenerator` in your scene, link relevant Tilemaps, colliders, runtime roots, and your profile.
4. Optionally assign `DungeonPortalLink` components to the generated portals so your scene transitions work.
5. Trigger generation either automatically on start (demo default) or via your own scripts (quests, NPC dialogue, etc.).

## Customisation ideas

* Swap the profile’s Tilemap palette to reuse the generator for different biomes (ice, desert, castle).
* Extend `DungeonGenerator` partial class with extra metadata passes (enemy spawn markers, secret rooms).
* Replace built-in corridor logic with your own layout algorithm—just feed the resulting cells back into the Tilemap painting helpers.
* Link `DungeonRuntimeData` to your quest or loot tables to spawn dungeon-specific rewards.

Use these scripts as a foundation when experimenting with top-down procedural runs; the data-driven profile + room templates let you prototype new layouts quickly.***
