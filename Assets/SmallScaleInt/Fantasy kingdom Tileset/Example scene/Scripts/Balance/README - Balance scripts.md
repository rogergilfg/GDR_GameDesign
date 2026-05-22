# Balance Systems Overview

The **Balance** folder centralises global tuning knobs so you can rebalance player progress, enemy difficulty, and resource economy without editing dozens of prefabs. Designers author a single `GameBalanceConfig` asset and let the runtime `GameBalanceManager` broadcast those multipliers to interested systems (experience, resources, damage, build costs, etc.).

## Files

| File | Purpose |
| --- | --- |
| `GameBalanceConfig.asset` | Sample ScriptableObject instance that ships with values used in the demo scene. Duplicate and tweak or create your own via *Create → Game → Balance Config*. |
| `GameBalanceConfig.cs` | Defines the ScriptableObject fields (player experience multipliers, resource gain, damage scaling, enemy health/damage, tile XP, build cost adjustments). Every field is annotated with tooltips and `[Min]` attributes to prevent invalid values. |
| `GameBalanceManager.cs` | Singleton MonoBehaviour that exposes the config at runtime. It pushes the experience multiplier into `PlayerExperience`, offers helper methods (`GetAdjustedEnemyExperience`, `GetAdjustedBuildCost`, etc.), and ensures only positive values apply. Place this on a bootstrap object and assign your desired config asset. |

## Typical workflow

1. Create or duplicate a `GameBalanceConfig` asset and adjust its multipliers to taste.
2. Drop `GameBalanceManager` into the scene (or a bootstrap prefab) and assign the config asset in the inspector.
3. At runtime, call the manager’s helper APIs whenever you award XP/resources or calculate build costs so your logic stays in sync.
4. Designers can fine-tune difficulty/economy by editing the config without touching code or prefabs.

Because all balance data lives in one ScriptableObject, it’s trivial to swap configs per build (e.g., “demo” vs “production”) or expose them to modders.***
