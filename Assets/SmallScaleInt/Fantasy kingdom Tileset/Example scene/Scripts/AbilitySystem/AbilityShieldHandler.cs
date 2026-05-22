using System.Collections.Generic;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Tracks temporary shield instances on an actor and absorbs incoming damage before it reaches health.
    /// Supports stacking shields, invulnerability toggles, idle VFX, and hit VFX feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityShieldHandler : MonoBehaviour
    {
        sealed class ShieldInstance
        {
            public int Remaining;
            public bool Invulnerable;
            public float ExpiresAt;
            public GameObject IdleVfx;
            public Transform AttachedTo;
            public GameObject HitVfxPrefab;
            public float HitVfxLifetime;
            public float HitVfxCooldown;
            public float LastHitFxAt;
        }

        readonly List<ShieldInstance> _activeShields = new();

        public static AbilityShieldHandler GetOrCreate(Transform target)
        {
            if (!target) return null;
            var handler = target.GetComponentInParent<AbilityShieldHandler>();
            if (!handler)
            {
                handler = target.gameObject.AddComponent<AbilityShieldHandler>();
            }
            return handler;
        }

        public static AbilityShieldHandler GetExisting(Transform target)
        {
            if (!target) return null;
            return target.GetComponentInParent<AbilityShieldHandler>();
        }

        public bool HasActiveShield => _activeShields.Count > 0;

        public void AddShield(int amount, float lifetime, GameObject idleVfxPrefab, GameObject hitVfxPrefab, float hitVfxLifetime, float hitVfxCooldown, Transform attachTarget, bool invulnerable, bool replaceExisting)
        {
            if (!invulnerable && amount <= 0) return;

            if (replaceExisting)
            {
                ClearAllShields();
            }

            CleanupExpiredShields();

            var instance = new ShieldInstance
            {
                Remaining = invulnerable ? int.MaxValue : Mathf.Max(1, amount),
                Invulnerable = invulnerable,
                ExpiresAt = lifetime > 0f ? Time.time + lifetime : -1f,
                AttachedTo = attachTarget ? attachTarget : transform,
                HitVfxPrefab = hitVfxPrefab,
                HitVfxLifetime = Mathf.Max(0f, hitVfxLifetime),
                HitVfxCooldown = Mathf.Max(0f, hitVfxCooldown)
            };

            if (idleVfxPrefab)
            {
                var parent = instance.AttachedTo ? instance.AttachedTo : transform;
                var spawnPos = parent ? parent.position : transform.position;
                instance.IdleVfx = Instantiate(idleVfxPrefab, spawnPos, Quaternion.identity, parent);
            }

            _activeShields.Add(instance);
        }

        public int AbsorbDamage(int incomingDamage)
        {
            CleanupExpiredShields();
            if (incomingDamage <= 0 || _activeShields.Count == 0)
                return incomingDamage;

            int remaining = incomingDamage;
            for (int i = 0; i < _activeShields.Count && remaining > 0;)
            {
                var shield = _activeShields[i];
                TrySpawnHitFx(shield);

                if (shield.Invulnerable)
                {
                    return 0;
                }

                int absorbed = Mathf.Min(remaining, shield.Remaining);
                shield.Remaining -= absorbed;
                remaining -= absorbed;

                if (shield.Remaining <= 0)
                {
                    CleanupShieldAt(i);
                }
                else
                {
                    i++;
                }
            }

            return remaining;
        }

        public void ClearAllShields()
        {
            for (int i = _activeShields.Count - 1; i >= 0; i--)
            {
                CleanupShieldAt(i);
            }
        }

        void Update()
        {
            CleanupExpiredShields();
        }

        void CleanupExpiredShields()
        {
            if (_activeShields.Count == 0) return;
            float now = Time.time;
            for (int i = _activeShields.Count - 1; i >= 0; i--)
            {
                var shield = _activeShields[i];
                if (shield.ExpiresAt >= 0f && now >= shield.ExpiresAt)
                {
                    CleanupShieldAt(i);
                }
            }
        }

        void CleanupShieldAt(int index)
        {
            if (index < 0 || index >= _activeShields.Count) return;
            var inst = _activeShields[index];
            if (inst.IdleVfx)
            {
                Destroy(inst.IdleVfx);
            }
            _activeShields.RemoveAt(index);
        }

        void TrySpawnHitFx(ShieldInstance shield)
        {
            if (!shield.HitVfxPrefab) return;
            float now = Time.time;
            if (shield.HitVfxCooldown > 0f && now < shield.LastHitFxAt + shield.HitVfxCooldown)
            {
                return;
            }

            shield.LastHitFxAt = now;
            var parent = shield.AttachedTo ? shield.AttachedTo : transform;
            var spawnPos = parent ? parent.position : transform.position;
            var vfx = Instantiate(shield.HitVfxPrefab, spawnPos, Quaternion.identity, parent);
            if (shield.HitVfxLifetime > 0f)
            {
                Destroy(vfx, shield.HitVfxLifetime);
            }
        }

        void OnDisable()
        {
            ClearAllShields();
        }

        void OnDestroy()
        {
            ClearAllShields();
        }
    }
}




