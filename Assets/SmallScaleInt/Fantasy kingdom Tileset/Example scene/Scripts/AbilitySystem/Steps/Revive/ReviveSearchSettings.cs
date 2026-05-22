using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    public sealed class ReviveSearchSettings
    {
        [Tooltip("Layers treated as allies when scanning for corpses .")]
        public LayerMask allyMask;

        [Tooltip("Maximum search radius around the caster.")]
        public float searchRadius = 8f;

        [Tooltip("Minimum distance from the caster before a corpse is considered.")]
        public float minDistance = 0f;

        [Tooltip("Maximum distance from the caster. <= 0 disables the check.")]
        public float maxDistance = 0f;

        public enum TargetPreference { Nearest, OldestDeadFirst, NewestDeadFirst }

        [Tooltip("When multiple corpses are valid, choose based on this preference.")]
        public TargetPreference preference = TargetPreference.Nearest;

        [Tooltip("Require clear line of sight from caster to corpse using the specified blockers m ask.")]
        public bool requireLineOfSight = true;

        public LayerMask lineOfSightBlockers;
    }
}




