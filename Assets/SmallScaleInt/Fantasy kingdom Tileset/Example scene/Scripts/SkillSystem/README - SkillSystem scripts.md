# Skill & Spellbook System Overview

Skill scripts cover both the passive skill tree UI and the spellbook/ability assignment UI. They demonstrate drag-and-drop ability slots, skill-node unlocking, and spell selection per hotkey.

## Major scripts

| File | Purpose |
| --- | --- |
| `SkillTreeDefinition.cs` + `*.asset` trees | ScriptableObjects describing each tree (nodes, connections, costs, unlock effects). Demo ships with Offense/Support/Marksman examples. |
| `SkillTreeState.cs` | Serializable runtime data for which nodes are unlocked/refunded. Persist or swap this asset to mimic character builds. |
| `SkillManager.cs` | Central authority that loads tree definitions, tracks spent points, validates unlock requirements, and exposes events for UI. |
| `SkillPanel.cs` / `SkillNodeView.cs` / `SkillTreeSelectionButton.cs` / `SkillWindowController.cs` | UI layer for browsing trees, displaying node info, spending/refunding points, and switching between trees. |
| `SpellBookController.cs` / `SpellBookView.cs` / `SpellBookAbilityButton.cs` / `AbilityDragDropService.cs` / `AbilityDragPreview.cs` | Spellbook UI that lets the player drag abilities into hotkey slots. Integrates with `AbilityRunner` and `AbilitySlotManager`. |

## Workflow

1. Define or tweak `SkillTreeDefinition` assets (set node graph, costs, rewards).
2. Place `SkillManager` + `SkillWindowController` in the scene; assign trees, UI references, and the player’s hotkey manager.
3. Use `SpellBookController` + drag-and-drop service to let the player assign unlocked abilities to action bar slots.
4. Persist `SkillTreeState` (or just the unlocked node ids) between sessions using your save system.

Because trees and abilities live in ScriptableObjects, designers can iterate on layouts and ability unlocks without coding. The demo implementation focuses on UI/UX—it’s up to your game logic to react to unlocked passives or newly slotted abilities.***
