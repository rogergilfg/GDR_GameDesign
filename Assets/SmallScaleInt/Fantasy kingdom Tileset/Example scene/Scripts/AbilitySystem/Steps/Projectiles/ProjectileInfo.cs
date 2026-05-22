using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [MovedFrom("AbilitySystem")]
    public sealed class ProjectileInfo
    {
        [Tooltip("Projectile prefab to spawn. Leave null to use the owner's default projectile (EnemyAI.projectilePrefab).")]
        public Projectile2D prefabOverride;

        [Tooltip("Optional spawn transform override. When null, falls back to the owner's default spawn transform.")]
        public Transform spawnOverride;

        [Tooltip("If > 0, overrides the projectile speed used for this launch.")]
        public float speedOverride = -1f;

        [Tooltip("If >= 0, overrides the projectile damage used for this launch.")]
        public int damageOverride = -1;

        [Tooltip("If > 0, overrides the projectile lifetime used for this launch.")]
        public float lifeOverride = -1f;

        [Tooltip("When true, use hitMaskOverride instead of the owner's default mask.")]
        public bool useCustomHitMask = false;

        public LayerMask hitMaskOverride;

        [Tooltip("Tint applied to the projectile's sprite renderers when overrideSpriteColor is enabled.")]
        public Color spriteOverrideColor = Color.white;

        [Tooltip("When enabled, the projectile sprite renderers are tinted using spriteOverrideColor.")]
        public bool overrideSpriteColor = false;
    }
}








