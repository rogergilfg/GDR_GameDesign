# Minimap System Overview

Minimap scripts handle rendering a top-down overview of the scene and syncing icon UI with world objects.

## Files

| File | Purpose |
| --- | --- |
| `MinimapController.cs` | Central brain: renders the world to a dedicated camera/render texture, manages panning/zoom, handles fog/visibility, and spawns UI representations for every registered icon. Supports both always-on and “press to expand” minimap modes. |
| `MinimapIcon.cs` | Component you place on world objects (player, enemies, NPCs). Stores sprite/colour/category metadata and registers with the controller at runtime. |
| `MinimapIconUI.cs` | Instantiated by the controller for each registered icon. Follows the icon’s world position, applies category-specific styling, and handles culling when the icon leaves the minimap bounds. |
| `MiniMapRenderTEX.renderTexture` | Sample render texture referenced by the UI to display the minimap camera output. Replace with your own resolution/settings as needed. |

## Usage

1. Drop `MinimapController` into your UI scene, assign its render texture, camera, icon templates, and optional fog mask.
2. Add `MinimapIcon` to any world object you want visible on the minimap (configure sprite/colour).
3. The controller automatically spawns `MinimapIconUI` instances that track each icon’s position on the minimap.

This setup keeps icon logic decoupled from world objects, making it easy to add new icon types or map layers without touching the rest of the gameplay code.***
