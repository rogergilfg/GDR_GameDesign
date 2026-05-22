# Experience System Overview

This folder encapsulates the demo’s XP reward plumbing so you can grant player experience whenever an enemy dies without duplicating logic.

## Components

| File | Purpose |
| --- | --- |
| `EnemyExperienceReward.cs` | MonoBehaviour you attach to any `EnemyHealth2D`. When the enemy dies it awards XP, consulting `GameBalanceManager` for multipliers and routing the value through `PlayerExperience`. Handles special cases such as kills performed by companions/turrets so XP spawn feedback appears near the player. |

## Usage

1. Add `EnemyExperienceReward` to an enemy prefab, set `experienceOnDeath`, and optionally tweak the combat-text offset.
2. Ensure `PlayerExperience` is present in the scene (demo prefab already provides it) so XP grants have a destination.
3. (Optional) Adjust global multipliers via `GameBalanceConfig` → `GameBalanceManager`.

That’s it—no other scripts live in this folder today, but it forms the foundation for more advanced XP hooks (quest turn-ins, crafting XP, etc.).***
