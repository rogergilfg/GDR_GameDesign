using System;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Base class for passive ability modifiers that apply bonuses while the ability is equipped.
    /// </summary>
    [Serializable]
    public abstract class PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("When enabled, this passive modifier is active.")]
        protected bool enabled = true;

        public bool Enabled => enabled;

        /// <summary>
        /// Apply this modifier's effects to the owner.
        /// Called when the passive ability is granted or on startup.
        /// </summary>
        public abstract void Apply(AbilityRunner runner);

        /// <summary>
        /// Remove this modifier's effects from the owner.
        /// Called when the passive ability is removed or on cleanup.
        /// </summary>
        public abstract void Remove(AbilityRunner runner);
    }

    /// <summary>
    /// Attribute to describe what a passive modifier does in the Unity inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PassiveModifierDescriptionAttribute : Attribute
    {
        public string Description { get; }
        public PassiveModifierDescriptionAttribute(string description)
        {
            Description = description;
        }
    }
}





