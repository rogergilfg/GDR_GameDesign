# Editors & Tools Overview

The **Editors** folder contains custom Unity editor tooling used to author tiles, catalogues, and dungeon layouts more efficiently. These scripts extend the inspector and add menu windows to speed up production.

## Tools

| File | Purpose |
| --- | --- |
| `AbilityRunnerEditor.cs` | Custom inspector for `AbilityRunner`. Groups cached component references, exposes cooldown/charge state for debugging, and offers quick buttons to test abilities directly in the editor. |
| `BuildCataloguePopulatorWindow.cs` | Editor window that scans `DestructibleTileData` assets and populates the `BuildCatalogue.asset` automatically, saving designers from manually dragging each part into the catalogue. |
| `DestructibleAnimatorBuilder.cs` | Utility for producing sprite animators for destructible props/tiles (batch sets up AnimatorControllers, animation clips, and assigns sprites). |
| `DestructibleTileBatchAuthorWindow.cs` + `DestructibleTileBatchProfile.cs` | Batch authoring workflow for destructible tiles. Configure naming conventions, tile palettes, rule sets, gear-drop defaults, and even alias tile-name lists, then auto-generate `DestructibleTileData` assets from a folder of sprites/tile assets. |
| `DungeonGeneratorEditor.cs` / `DungeonTemplatePrefabCreator.cs` | Adds inspector utilities for `DungeonGenerator` (validate profiles, preview room graphs) and a window to turn room scenes into prefab-based `DungeonRoomTemplate` assets. |
| `ResourceTypeDefEditor.cs` | Custom inspector for resources that adds quick colour/icon previews and category toggles, ensuring resource assets remain consistent. |
| `TileColliderAutoPlacerEditor.cs` | Editor inspector for the collider auto placer so designers can re-run population, validate ignore/special rules, and see summary stats without entering play mode. |

## Workflow tips

* Access the windows via the `SmallScale` menu tab (under Building, Destruction, Dungeon Generation, etc.) or the asset-context menus noted in each script.
* Most custom inspectors include “Validate” buttons—use them after modifying assets to catch missing references before playtesting.
* Batch creators (tile/destructible) are safe to rerun; they overwrite generated assets based on profile rules, keeping manual files untouched.

These editor scripts are optional but can significantly reduce setup time for large tile libraries or complex dungeon layouts. Feel free to adapt them to your own asset pipeline.***
