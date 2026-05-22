# UI Helpers Overview

General-purpose HUD components live here. They bind the gameplay systems (abilities, health, mana, companions, passive buffs) to Unity UI elements so the demo scene feels cohesive.

## Highlights

| File | Purpose |
| --- | --- |
| `AbilityTooltip.cs` / `PassiveBuffUI.cs` / `PassiveBuffPanelController.cs` | Display ability/buff tooltips, icons, stacks, and durations. Integrate with the ability system to show contextual info. |
| `BossHealthBarUI.cs` | World-space boss bar prefab controlled by `EnemyHealth2D`. Supports showing/hiding, updating health values, and sticking to a dedicated canvas panel. |
| `PlayerHealthUIBinder.cs` / `PlayerManaUIBinder.cs` / `PlayerExperienceUI.cs` | Bind player stats to sliders/text. Listen for events from `PlayerHealth`, `PlayerMana`, and `PlayerExperience`. |
| `DynamicResourceHudUI.cs` | Shows current resource balances (coins/ore/etc.) by subscribing to `DynamicResourceManager`. |
| `GameplayOptionsPanel.cs` | Example options menu toggling fullscreen, resolution, SFX volumes, etc. |
| `CompanionCommandPanelController.cs` | UI for issuing commands to companions (follow, stay, attack). |
| `StartScreenController.cs` | Displays a start overlay, hides HUD, and disables player input until the user presses a Start button—useful for showcasing the demo scene without immediate control. |
| `CardFlipAnimator.cs` | Reusable animation helper for flipping UI cards/buttons. |

## Usage

Drop these scripts onto existing UI prefabs (health bar, buff row, resource panel) and assign references via the inspector. Most of them listen for events from the underlying gameplay system and update visuals automatically. They are intentionally lightweight so you can swap in your own art or layout without rewriting logic.***
