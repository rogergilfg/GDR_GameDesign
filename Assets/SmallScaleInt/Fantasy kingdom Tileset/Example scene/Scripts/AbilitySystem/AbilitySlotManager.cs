using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [DisallowMultipleComponent]
    [MovedFrom("AbilitySystem")]
    public sealed class AbilitySlotManager : MonoBehaviour
    {
        [Serializable]
        public sealed class AbilitySlot
        {
            public AbilityDefinition ability;
        }

        [SerializeField]
        private AbilityRunner abilityRunner;

        [SerializeField]
        [Tooltip("Total number of available ability slots.")]
        [Range(1, 12)]
        private int slotCount = 10;

        [SerializeField]
        private AbilitySlot[] slots = Array.Empty<AbilitySlot>();

        public event Action<int, AbilityDefinition> SlotChanged;

        public AbilityRunner Runner => abilityRunner;
        public int SlotCount => slots?.Length ?? 0;

        public bool HasAnyAssignedAbility()
        {
            if (slots == null)
            {
                return false;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].ability != null)
                {
                    return true;
                }
            }

            return false;
        }

        void Awake()
        {
            EnsureSlots();
            if (!abilityRunner)
            {
                abilityRunner = GetComponent<AbilityRunner>();
            }
        }

        void OnValidate()
        {
            EnsureSlots();
        }

        void EnsureSlots()
        {
            if (slotCount < 1) slotCount = 1;
            if (slots == null || slots.Length != slotCount)
            {
                var newSlots = new AbilitySlot[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    if (slots != null && i < slots.Length && slots[i] != null)
                    {
                        newSlots[i] = slots[i];
                    }
                    else
                    {
                        newSlots[i] = new AbilitySlot();
                    }
                }

                slots = newSlots;
            }
        }

        public AbilityDefinition GetAbility(int index)
        {
            if (!IsValidIndex(index) || slots[index] == null)
            {
                return null;
            }

            return slots[index].ability;
        }

        public bool TryAssign(int index, AbilityDefinition ability)
        {
            if (!IsValidIndex(index) || !ability)
            {
                return false;
            }

            int existingIndex = FindSlotContaining(ability);
            if (existingIndex == index)
            {
                EnsureAbilityRegistered(ability);
                SlotChanged?.Invoke(index, ability);
                return true;
            }

            if (existingIndex >= 0)
            {
                slots[existingIndex].ability = null;
                SlotChanged?.Invoke(existingIndex, null);
            }

            EnsureAbilityRegistered(ability);
            slots[index].ability = ability;
            SlotChanged?.Invoke(index, ability);
            return true;
        }

        public bool TrySwap(int fromIndex, int toIndex)
        {
            if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex) || fromIndex == toIndex)
            {
                return false;
            }

            var fromAbility = slots[fromIndex]?.ability;
            var toAbility = slots[toIndex]?.ability;

            if (fromAbility == null && toAbility == null)
            {
                return false;
            }

            if (fromAbility == null)
            {
                slots[fromIndex].ability = toAbility;
                slots[toIndex].ability = null;
                SlotChanged?.Invoke(fromIndex, toAbility);
                SlotChanged?.Invoke(toIndex, null);
                return true;
            }

            if (toAbility == null)
            {
                slots[toIndex].ability = fromAbility;
                slots[fromIndex].ability = null;
                SlotChanged?.Invoke(fromIndex, null);
                SlotChanged?.Invoke(toIndex, fromAbility);
                return true;
            }

            slots[fromIndex].ability = toAbility;
            slots[toIndex].ability = fromAbility;
            SlotChanged?.Invoke(fromIndex, toAbility);
            SlotChanged?.Invoke(toIndex, fromAbility);
            return true;
        }

        public bool TryClear(int index)
        {
            if (!IsValidIndex(index) || slots[index] == null || slots[index].ability == null)
            {
                return false;
            }

            slots[index].ability = null;
            SlotChanged?.Invoke(index, null);
            return true;
        }

        public bool TryAutoAssign(AbilityDefinition ability)
        {
            if (!ability) return false;
            if (FindSlotContaining(ability) >= 0) return false;

            int freeIndex = FindFirstFreeSlot();
            if (freeIndex < 0)
            {
                return false;
            }

            return TryAssign(freeIndex, ability);
        }

        public int FindFirstFreeSlot()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == null || slots[i].ability == null)
                {
                    return i;
                }
            }

            return -1;
        }

        public int FindSlotContaining(AbilityDefinition ability)
        {
            if (!ability) return -1;

            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] != null && slots[i].ability == ability)
                {
                    return i;
                }
            }

            return -1;
        }

        public void RemoveAbility(AbilityDefinition ability)
        {
            if (!ability || slots == null) return;
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] != null && slots[i].ability == ability)
                {
                    slots[i].ability = null;
                    SlotChanged?.Invoke(i, null);
                }
            }
        }

        public void ReplaceAbility(AbilityDefinition oldAbility, AbilityDefinition newAbility)
        {
            if (!oldAbility || slots == null) return;

            int slotIndex = FindSlotContaining(oldAbility);
            if (slotIndex < 0) return;

            if (newAbility)
            {
                int existingIndex = FindSlotContaining(newAbility);
                if (existingIndex >= 0 && existingIndex != slotIndex)
                {
                    slots[existingIndex].ability = null;
                    SlotChanged?.Invoke(existingIndex, null);
                }

                EnsureAbilityRegistered(newAbility);
                slots[slotIndex].ability = newAbility;
                SlotChanged?.Invoke(slotIndex, newAbility);
            }
            else
            {
                slots[slotIndex].ability = null;
                SlotChanged?.Invoke(slotIndex, null);
            }
        }

        void EnsureAbilityRegistered(AbilityDefinition ability)
        {
            if (!abilityRunner) return;
            abilityRunner.TryAddAbility(ability);
        }

        bool IsValidIndex(int index) => index >= 0 && index < SlotCount;
    }
}





