# Trading System Overview

Trading scripts power the demo merchant interaction: opening a shop panel, listing items, and completing purchases via the resource system.

## Files

| File | Purpose |
| --- | --- |
| `TraderComponent.cs` | Attach to an NPC prefab to define what items they sell. References `LootDropDefinition`/`GearItem` lists, opening prices, refresh timers, and handles player interaction (open panel, close on walk-away). |
| `TraderPanelController.cs` | UI layer for the shop. Populates item buttons, shows resource costs, checks `DynamicResourceManager` + `PlayerInventory` for funds, and finalises purchases (adding gear, deducting coins). |

## Usage

1. Configure a trader NPC with `TraderComponent` (assign stock, optional respawn timers).
2. Place `TraderPanelController` in your UI scene and link it in the component so it can show/hide the panel.
3. Ensure `DynamicResourceManager` and `PlayerInventory` exist in the scene so purchases can succeed.

Feel free to extend the panel with buyback tabs, sell lists, or voice-overs—the provided scripts focus on the basic buy flow.***
