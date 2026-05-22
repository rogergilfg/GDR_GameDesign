# Building System Overview

The **Building** folder contains the demo’s tile-placement pipeline: catalogue assets describing buildable parts, UI scripts for browsing/unlocking them, and runtime controllers that apply costs and paint tiles on the correct tilemaps. It is intentionally modular so you can replace only the bits you need while reusing the rest.

## Key assets and scripts

| File | Purpose |
| --- | --- |
| `BuildCatalogue.asset / BuildCatalogue.cs` | ScriptableObject list of `DestructibleTileData` definitions that the menu exposes. Designers reorder/toggle parts here without touching UI. The asset registers its definitions with `BuildUnlockService` on enable. |
| `BuildPartCategory.cs` | Enum used to group parts (Ground, Walls, Roofs, Objects, etc.) so UI tabs and tilemap routing stay in sync. |
| `BuildMenuController.cs` | Opens/closes the menu, populates scroll content with `BuildPartButton` prefabs, closes conflicting windows (inventory/skills) and raises `BuildPartSelected` events the placement controller listens for. |
| `BuildPartButton.cs` | UI component that binds a definition to a button, previews its icon/cost, and forwards selection/hover events back to the menu. |
| `ResourceCostPanel.cs` | Displays the resource requirements for the currently highlighted build part, coloring entries red when the player lacks enough resources. |
| `BuildUnlockService.cs` | Central unlock registry. Tracks which parts are available, exposes helper methods to lock/unlock categories, and persists choices across play sessions if desired. |
| `TilemapBuildController.cs` | In-game placement brain. Handles preview sprites, input, tilemap selection per category, collider regeneration, integration with `GameBalanceManager` (build cost multiplier) and `BuildMenuController` selection events. |

## Typical flow

1. **Define parts** – Create/modify `DestructibleTileData` assets, then add them to a `BuildCatalogue`. Assign categories, sprites, collider data, and resource costs per part.
2. **Set up UI** – Place `BuildMenuController` in the scene, wire its tab buttons, scroll container, `BuildCatalogue`, `ResourceCostPanel`, and cross-close dependencies (inventory/skills). The controller instantiates `BuildPartButton` for each entry.
3. **Placement** – Attach `TilemapBuildController` to a bootstrap GameObject. Assign the menu, each target tilemap (ground/walls/roof/objects), preview tilemap, broken-objects tilemap, camera, and any collider bindings. Hook its public methods to your input if you want custom toggles.
4. **Costs & unlocks** – Configure resource costs on each part. `TilemapBuildController` queries the player’s resources + `GameBalanceManager.BuildCostMultiplier` before committing placement. Use `BuildUnlockService` to gate categories or specific parts (quests, skill unlocks, etc.).

### Extending the system

* Swap out `TilemapBuildController` with your own placement rules while still consuming `BuildMenuController` events.
* Replace `BuildPartButton` visuals/layout without touching data—just keep the same serialized fields/events.
* Add new `BuildPartCategory` values and map them to additional tilemaps inside `TilemapBuildController`.
* Integrate your save system by serializing `BuildUnlockService` state or the `playerPlacedTiles` dictionary maintained by `TilemapBuildController`.

Use these scripts as a reference implementation: they cover the entire UX from browsing parts to paying resources, but you can drop the pieces you don't need in production.***
