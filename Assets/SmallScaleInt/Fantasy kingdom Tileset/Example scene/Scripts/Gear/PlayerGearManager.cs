using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles equipping and unequipping gear on the player character. The manager keeps the
/// runtime animator controllers for each gear slot in sync with the equipped gear items and
/// ensures all visuals stay aligned with the main player animator.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "PlayerGearManager")]
public class PlayerGearManager : MonoBehaviour
{
    [Serializable]
    private class GearSlot
    {
        [SerializeField]
        private GearType gearType = GearType.Weapon;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        private RuntimeAnimatorController defaultAnimator;

        [SerializeField]
        private GearItem startingGear;

        [NonSerialized]
        private SpriteRenderer cachedSpriteRenderer;

        [NonSerialized]
        private bool hasCachedDefaultColor;

        [NonSerialized]
        private Color defaultColor = Color.white;

        public GearType GearType => gearType;
        public Animator Animator => animator;
        public RuntimeAnimatorController DefaultAnimator => defaultAnimator;
        public GearItem StartingGear => startingGear;

        public void CaptureDefaultColor()
        {
            if (hasCachedDefaultColor)
            {
                return;
            }

            SpriteRenderer renderer = GetSpriteRenderer();
            if (renderer == null)
            {
                return;
            }

            defaultColor = renderer.color;
            hasCachedDefaultColor = true;
        }

        public void ApplyColor(Color color)
        {
            SpriteRenderer renderer = GetSpriteRenderer();
            if (renderer == null)
            {
                return;
            }

            renderer.color = color;
        }

        public Color DefaultColor
        {
            get
            {
                if (!hasCachedDefaultColor)
                {
                    CaptureDefaultColor();
                }

                return defaultColor;
            }
        }

        private SpriteRenderer GetSpriteRenderer()
        {
            if (cachedSpriteRenderer == null && animator != null)
            {
                cachedSpriteRenderer = animator.GetComponent<SpriteRenderer>();
            }

            return cachedSpriteRenderer;
        }
    }

    [SerializeField]
    [Tooltip("Animator driving the base player animations.")]
    private Animator mainAnimator;

    [SerializeField]
    [Tooltip("Synchronizes gear animators with the main animator.")]
    private GearAnimatorSynchronizer animatorSynchronizer;

    [SerializeField]
    [Tooltip("Configuration for each supported gear slot on the player.")]
    private GearSlot[] gearSlots = Array.Empty<GearSlot>();

    [SerializeField]
    [Tooltip("Additional child animators (e.g. overlays) that should follow the main animator.")]
    private Animator[] auxiliaryAnimators = Array.Empty<Animator>();

    private readonly Dictionary<GearType, GearSlot> slotLookup = new Dictionary<GearType, GearSlot>();
    private readonly Dictionary<GearType, GearItem> equippedGear = new Dictionary<GearType, GearItem>();

    /// <summary>
    /// Invoked whenever the equipment state of a slot changes. Parameters: slot type, new gear, previous gear.
    /// </summary>
    public event Action<GearType, GearItem, GearItem> GearChanged;

    /// <summary>
    /// Provides read-only access to the currently equipped gear items.
    /// </summary>
    public IReadOnlyDictionary<GearType, GearItem> EquippedGear => equippedGear;

    /// <summary>
    /// Animator responsible for the player's core animation state.
    /// </summary>
    public Animator MainAnimator => mainAnimator;

    private void Reset()
    {
        mainAnimator = GetComponent<Animator>();
        animatorSynchronizer = GetComponent<GearAnimatorSynchronizer>();
    }

    private void Awake()
    {
        if (mainAnimator == null)
        {
            mainAnimator = GetComponent<Animator>();
        }

        if (animatorSynchronizer == null)
        {
            animatorSynchronizer = GetComponent<GearAnimatorSynchronizer>();
        }

        slotLookup.Clear();
        equippedGear.Clear();

        HashSet<Animator> registeredAnimators = null;

        if (animatorSynchronizer != null)
        {
            animatorSynchronizer.SetMainAnimator(mainAnimator);
            animatorSynchronizer.ClearGearAnimators();

            registeredAnimators = new HashSet<Animator>();

            RegisterAuxiliaryAnimator(animatorSynchronizer, registeredAnimators, auxiliaryAnimators);
            RegisterAuxiliaryAnimator(animatorSynchronizer, registeredAnimators, FindChildAnimator("Slash"));
        }

        foreach (GearSlot slot in gearSlots)
        {
            if (slot == null)
            {
                continue;
            }

            if (slotLookup.ContainsKey(slot.GearType))
            {
                Debug.LogWarning($"Duplicate gear slot definition for {slot.GearType} on {name}. Only the first slot will be used.");
                continue;
            }

            if (slot.Animator == null)
            {
                Debug.LogWarning($"Gear slot {slot.GearType} on {name} is missing an animator reference.");
                continue;
            }

            slotLookup.Add(slot.GearType, slot);
            equippedGear[slot.GearType] = null;

            slot.CaptureDefaultColor();
            ApplyAnimatorController(slot, slot.DefaultAnimator);
            slot.ApplyColor(slot.DefaultColor);

            if (animatorSynchronizer != null)
            {
                RegisterAuxiliaryAnimator(animatorSynchronizer, registeredAnimators, slot.Animator);
            }
        }

        foreach (GearSlot slot in gearSlots)
        {
            if (slot?.StartingGear == null)
            {
                continue;
            }

            Equip(slot.StartingGear, true);
        }
    }

    /// <summary>
    /// Returns the gear item currently equipped in the requested slot, or <c>null</c> if empty.
    /// </summary>
    public GearItem GetEquipped(GearType gearType)
    {
        return equippedGear.TryGetValue(gearType, out GearItem gear) ? gear : null;
    }

    /// <summary>
    /// Equips the supplied gear item in the appropriate slot.
    /// </summary>
    public bool Equip(GearItem gear)
    {
        return Equip(gear, false);
    }

    /// <summary>
    /// Unequips any gear currently occupying the specified slot.
    /// </summary>
    public bool Unequip(GearType gearType)
    {
        if (!slotLookup.TryGetValue(gearType, out GearSlot slot))
        {
            Debug.LogWarning($"Attempted to unequip gear from undefined slot {gearType} on {name}.");
            return false;
        }

        if (!equippedGear.TryGetValue(gearType, out GearItem previous) || previous == null)
        {
            if (slot.Animator != null && slot.Animator.runtimeAnimatorController == slot.DefaultAnimator)
            {
                return false;
            }

            previous = null;
        }

        ApplyAnimatorController(slot, slot.DefaultAnimator);
        slot.ApplyColor(slot.DefaultColor);
        equippedGear[gearType] = null;

        GearChanged?.Invoke(gearType, null, previous);
        return true;
    }

    private bool Equip(GearItem gear, bool isInitialSetup)
    {
        if (gear == null)
        {
            Debug.LogWarning("Attempted to equip a null gear item.");
            return false;
        }

        if (!slotLookup.TryGetValue(gear.GearType, out GearSlot slot))
        {
            Debug.LogWarning($"Attempted to equip gear of type {gear.GearType} on {name}, but no slot exists for that type.");
            return false;
        }

        if (gear.AnimatorController == null)
        {
            Debug.LogWarning($"Gear item {gear.name} does not define an animator controller.");
            return false;
        }

        if (!isInitialSetup)
        {
            PlayerExperience xp = PlayerExperience.Instance;
            if (xp != null && xp.CurrentLevel < gear.RequiredLevel)
            {
                Debug.LogWarning($"Cannot equip {gear.DisplayName}: requires level {gear.RequiredLevel} but player is level {xp.CurrentLevel}.");
                return false;
            }
        }

        GearItem previous = GetEquipped(gear.GearType);

        ApplyAnimatorController(slot, gear.AnimatorController);
        slot.ApplyColor(gear.SpriteColor);
        equippedGear[gear.GearType] = gear;

        if (!isInitialSetup)
        {
            GearChanged?.Invoke(gear.GearType, gear, previous);
        }

        return true;
    }

    private void ApplyAnimatorController(GearSlot slot, RuntimeAnimatorController controller)
    {
        if (slot == null)
        {
            return;
        }

        Animator animator = slot.Animator;
        if (animator == null)
        {
            return;
        }

        animator.runtimeAnimatorController = controller;
        animator.Rebind();
        animator.Update(0f);

        if (animatorSynchronizer != null)
        {
            animatorSynchronizer.SynchronizeAnimatorImmediately(animator);
        }
    }

    private void RegisterAuxiliaryAnimator(GearAnimatorSynchronizer synchronizer, HashSet<Animator> registeredAnimators, params Animator[] animators)
    {
        if (synchronizer == null || registeredAnimators == null || animators == null)
        {
            return;
        }

        foreach (Animator animator in animators)
        {
            if (animator == null || !registeredAnimators.Add(animator))
            {
                continue;
            }

            synchronizer.RegisterGearAnimator(animator);
        }
    }

    private Animator FindChildAnimator(string childName)
    {
        if (string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform childTransform = transform.Find(childName);
        if (childTransform == null)
        {
            return null;
        }

        return childTransform.GetComponent<Animator>();
    }
}



}






