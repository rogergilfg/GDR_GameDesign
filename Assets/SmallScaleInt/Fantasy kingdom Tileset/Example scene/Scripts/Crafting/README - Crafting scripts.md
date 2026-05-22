# Crafting System Overview

The **Crafting** folder drives the demo’s gear crafting station: UI controllers for browsing recipes, widgets for each button, and an interaction script that ties world objects to the crafting menu and resource systems. Use it as a reference wrapper around your own inventory/resource logic.

## Components

| File | Purpose |
| --- | --- |
| `CraftingMenuController.cs` | Manages the entire crafting UI. Populates the recipe list from a `GearItemDatabase`, shows detailed requirements, validates player resources via `PlayerInventory` + `DynamicResourceManager`, animates progress bars, and spawns crafted items into inventory. |
| `CraftingRecipeButton.cs` | UI element instantiated for every recipe. Displays icon/name/resource costs, handles hover/selection callbacks, greys out when insufficient resources, and forwards events back to the menu. |
| `CraftingInteractionController.cs` | World-space trigger that opens the menu when the player interacts. Handles controller/gamepad focus, pauses gameplay if needed, and closes the window when the player leaves or presses cancel. |
| `CraftButtonPrefab.prefab` | Example prefab referenced by `CraftingMenuController` for the action buttons (craft/cancel). Modify or replace to match your UI style. |

## Typical usage

1. **Populate databases** – List all craftable `GearItem` objects inside a `GearItemDatabase` and ensure corresponding resource types exist in `ResourceDatabase`.
2. **Wire UI** – Drop `CraftingMenuController` into your scene, assign references (inventory, resource manager, progress slider, detail panel templates, recipe button prefab, etc.).
3. **Hook interactions** – Attach `CraftingInteractionController` to an interactable object (forge, alchemy table). Point it at the menu controller so it can toggle visibility when the player presses the interaction key.
4. **Style buttons** – Adjust `CraftingRecipeButton` prefabs and `CraftButtonPrefab` to match your project; the scripts only expect certain component references (images, TMP labels, etc.).

## Extending/customising

* Replace `DynamicResourceManager` calls with your own resource API (gold/ingredients) by editing `CraftingMenuController`.
* Add filtering tabs (armor, weapons, consumables) by extending the controller to query gear types; the existing `ArmorGearTypes` hash shows one possible approach.
* Keep the interaction controller but hook it into other systems (quests, NPC dialogue) before opening the menu.
* Hide/show crafting recipes dynamically by checking unlock flags before instantiating each `CraftingRecipeButton`.

These scripts demonstrate the end-to-end flow (open menu → select recipe → validate cost → craft → update resources). Reuse as-is for prototypes or as a guide when implementing your production crafting UI.***
