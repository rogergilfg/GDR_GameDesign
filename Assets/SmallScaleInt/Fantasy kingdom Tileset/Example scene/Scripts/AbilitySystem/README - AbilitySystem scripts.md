# Ability System Overview

This folder introduces a modular ability pipeline shared by players and enemies. The behaviour of every ability is data-driven: you compose reusable requirements and steps in the inspector instead of writing bespoke MonoBehaviours for each variation.

## Core building blocks

| Component | Purpose |
| --- | --- |
| AbilityDefinition | ScriptableObject that stores cooldowns, resource cost, requirements, and the ordered list of execution steps. |
| AbilityRequirement | Managed-reference gate evaluated before the ability fires (distance, line-of-sight, health thresholds, leash checks, random chance, etc.). Each ability hosts its own instance so values can differ per ability. |
| AbilityStep | Managed-reference execution block (animator trigger, dash, DoT, summon, heal, buff, etc.). Steps live inside the ability asset, so tweaking parameters does not require new assets. |
| AbilityRunner | MonoBehaviour that executes abilities, tracks cooldowns/charges, and exposes cached components (animator, rigidbody, AI, controller, mana/health). |
| AbilityRuntimeContext | Runtime payload passed to requirements/steps containing owner references, targets, desired direction, and helper methods. |

### Requirement library (initial + new)

* Distance band, line-of-sight, enemy leash engagement, facing dot check.
* Owner/target health thresholds, random chance rolls.

### Step library highlights

* Animator trigger/bool, wait, spawn prefab, face target, play audio, invoke UnityEvents.
* Movement: directional dash, charge with collision/damage, temporary speed buffs.
* Combat effects: single-target damage, AoE bursts, damage-over-time fields, ally heals, mana/health grants, attribute point rewards, mass revives, summon spawners.
* Utility: Rigidbody locks, enemy AI pause, trail-based loot gather, flexible VFX/audio hooks.

These cover the behaviour of legacy abilities such as PlayerDodge2D, EnemyChargeAbility2D, EnemyHealAbility2D, EnemySummonAbility2D, and open the door to passives (movement buffs, resource grants), DoT zones, revive rituals, and more. Additional behaviours can be introduced by authoring new requirement/step classes without touching gameplay code.

## Runtime utilities

* AbilityHotkeyTrigger maps an input key to an ability on the local runner (with optional WASD/mouse direction).
* EnemyAbilityAutoCaster polls a set of abilities on an enemy and fires the first one that passes its requirements—useful for prototyping before wiring everything into the AI brain.

## Typical setup flow

1. Create an ability asset via *Create ▸ Ability System ▸ Ability Definition*.
2. Add requirements (right-click the managed-reference list to choose a requirement type) and tweak their inline settings.
3. Add steps in the same way (e.g., animator trigger → wait → dash → AoE damage → summon).
4. Attach an AbilityRunner to the actor and assign the ability.
5. Trigger the ability via code, AbilityHotkeyTrigger, EnemyAbilityAutoCaster, or your existing input/AI systems.

Keep the legacy ability scripts alongside the new system while you migrate: author ScriptableObject equivalents, test them in parallel, then retire the old components once parity is confirmed.

### Additional visual / utility steps

* ProjectileBurstStep – spawns directional projectile bursts with target alignment and sequential firing.
* SummonStep – configurable ally or creature spawning with randomised offsets, collision avoidance, and capacity limits.
* ReviveAlliesStep – searches for fallen allies (distance/LOS/reservation gates) and revives them with windup/recovery hooks.
* ScaleOverTimeStep – animates transform scale (grow/hold/shrink) for any ability.
* SpriteFlashStep – pulses sprite colours over time, restoring originals afterward.
* ModifyEnemyStatsStep – temporarily adjusts damage multipliers and toggles knockback suppression.\n



