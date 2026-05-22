using SmallScale.FantasyKingdomTileset.AbilitySystem;
using System;
using UnityEngine;

namespace SkillSystem
{
    public static class AbilityDragDropService
    {
        static AbilityDefinition s_CurrentAbility;
        static Sprite s_CurrentIcon;
        static AbilitySlotManager s_SourceManager;
        static int s_SourceSlotIndex = -1;

        public static event Action<AbilityDefinition, Sprite> DragStarted;
        public static event Action DragEnded;
        public static event Action<Vector2> DragMoved;

        public static bool HasPayload => s_CurrentAbility != null;
        public static AbilityDefinition Ability => s_CurrentAbility;
        public static Sprite Icon => s_CurrentIcon;
        public static AbilitySlotManager SourceManager => s_SourceManager;
        public static int SourceSlotIndex => s_SourceSlotIndex;
        public static bool HasSourceSlot => s_SourceManager && s_SourceSlotIndex >= 0;

        public static void BeginDrag(AbilityDefinition ability, Sprite icon, AbilitySlotManager sourceManager = null, int sourceIndex = -1)
        {
            if (!ability)
            {
                return;
            }

            s_CurrentAbility = ability;
            s_CurrentIcon = icon;
            s_SourceManager = sourceManager;
            s_SourceSlotIndex = sourceManager ? Mathf.Max(0, sourceIndex) : -1;
            DragStarted?.Invoke(s_CurrentAbility, s_CurrentIcon);
        }

        public static void UpdatePosition(Vector2 screenPosition)
        {
            if (!HasPayload)
            {
                return;
            }

            DragMoved?.Invoke(screenPosition);
        }

        public static void EndDrag()
        {
            if (!HasPayload)
            {
                return;
            }

            s_CurrentAbility = null;
            s_CurrentIcon = null;
            s_SourceManager = null;
            s_SourceSlotIndex = -1;
            DragEnded?.Invoke();
        }
    }
}





