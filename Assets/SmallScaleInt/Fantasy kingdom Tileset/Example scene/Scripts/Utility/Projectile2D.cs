using System;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

namespace SmallScaleInc.CharacterCreatorFantasy
{
    public class Projectile2D : MonoBehaviour
    {
        [Header("Visual Facing")]
        [Tooltip("If your sprite/animation faces +X (right) set 0. If it faces +Y (up) set -90, etc.")]
        public float artForwardDeg = 0f;

        [Tooltip("Assign if your Animator/SpriteRenderer is on a child; otherwise we rotate this transform.")]
        public Transform visualRoot;

        [Tooltip("Continuously reorient to velocity (true) or only once on spawn (false).")]
        public bool faceVelocity = true;

        [Header("Destructible Props")]
        [Tooltip("When enabled, projectile can hit and damage destructible props (objects with DestructibleProp2D).")]
        public bool damageDestructibleProps = false;

        [Tooltip("Number of hits to apply to destructible props. Props have maxHits and are destroyed when hits >= maxHits.")]
        [Min(1)]
        public int propHitAmount = 1;

        [Tooltip("Destroy projectile after hitting a destructible prop.")]
        public bool destroyOnPropHit = true;

        [Header("Tile Damage")]
        [Tooltip("When enabled, projectile damages tiles on contact with tilemap.")]
        public bool damageTiles = false;

        [Tooltip("Amount of damage to apply to tiles.")]
        [Min(1)]
        public int tileDamage = 1;

        [Tooltip("Destroy projectile after hitting a tile.")]
        public bool destroyOnTileHit = true;

        [Header("Collision / Obstacles")]
        [Tooltip("Radius used when checking for hits each frame.")]
        public float hitRadius = 0.15f;
        [Tooltip("Layers that block the projectile even if they are not valid damage targets (e.g. walls).")]
        public LayerMask obstacleMask;
        [Tooltip("Destroy the projectile whenever it hits an obstacle in obstacleMask.")]
        public bool destroyOnObstacleHit = true;

        Vector2 _velocity;
        int     _damage;
        float   _life;
        LayerMask _hitMask;
        Transform _owner;
        Vector2 _lastTravelDir = Vector2.right;
        Action<Transform> _damageableHitCallback;
        Action<Vector2> _tileHitCallback;

        void Awake()
        {
            if (!visualRoot) visualRoot = transform;
        }

        public void Init(Vector2 velocity, int damage, float life, LayerMask hitMask, Transform owner = null)
        {
            _velocity = velocity;
            _damage   = ResolveDamage(owner, damage);
            _life     = life;
            _hitMask  = hitMask;
            _owner    = owner;
            _lastTravelDir = _velocity.sqrMagnitude > 0.0001f ? _velocity.normalized : Vector2.right;

            // Snap facing immediately so the first frame looks correct
            if (_velocity.sqrMagnitude > 0.0001f)
            {
                float ang = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg + artForwardDeg;
                visualRoot.rotation = Quaternion.Euler(0f, 0f, ang);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 startPos = transform.position;
            Vector2 delta = _velocity * dt;
            float distance = delta.magnitude;
            Vector2 travelDir = distance > 0.0001f
                ? delta / distance
                : (_velocity.sqrMagnitude > 0.0001f ? _velocity.normalized : Vector2.right);
            if (travelDir.sqrMagnitude > 0.0001f)
                _lastTravelDir = travelDir;

            if (distance > 0.0001f && ProcessTravelHits(startPos, travelDir, distance))
                return;

            transform.position = startPos + delta;

            // Optional: keep facing velocity if it curves/homes
            if (faceVelocity && _velocity.sqrMagnitude > 0.0001f)
            {
                float ang = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg + artForwardDeg;
                visualRoot.rotation = Quaternion.Euler(0f, 0f, ang);
            }

            _life -= dt;
            if (_life <= 0f) { Destroy(gameObject); return; }

            // Simple circle probe each frame
            float radius = Mathf.Max(0.01f, hitRadius);
            var hits = Physics2D.OverlapCircleAll(transform.position, radius, _hitMask);
            foreach (var h in hits)
            {
                if (_owner && (h.transform == _owner || h.transform.IsChildOf(_owner)))
                    continue;

                if (HandleColliderImpact(h, transform.position, travelDir))
                    return;
            }

            // Check for tile collision
            if (damageTiles)
            {
                if (TryDamageTileAt(transform.position, _lastTravelDir))
                {
                    if (destroyOnTileHit)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }
            }
        }

        bool ProcessTravelHits(Vector2 origin, Vector2 direction, float distance)
        {
            int combinedMask = _hitMask.value;
            if (obstacleMask.value != 0)
                combinedMask |= obstacleMask.value;

            if (combinedMask == 0 || distance <= 0.0001f)
                return false;

            var hits = Physics2D.CircleCastAll(origin, Mathf.Max(0.01f, hitRadius), direction, distance, combinedMask);
            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (!hit.collider)
                    continue;
                if (_owner && (hit.collider.transform == _owner || hit.collider.transform.IsChildOf(_owner)))
                    continue;

                transform.position = hit.point;
                if (HandleColliderImpact(hit.collider, hit.point, direction))
                    return true;
            }

            return false;
        }

        bool HandleColliderImpact(Collider2D collider, Vector2 hitPoint, Vector2 travelDir)
        {
            if (!collider)
                return false;

            if (damageDestructibleProps)
            {
                var prop = collider.GetComponentInParent<DestructibleProp2D>();
                if (prop != null && !prop.ignoreIncomingDamage)
                {
                    prop.ApplyHit(propHitAmount);
                    if (destroyOnPropHit)
                    {
                        Destroy(gameObject);
                        return true;
                    }
                }
            }

            var dmg = collider.GetComponentInParent<EnemyAI.IDamageable>();
            if (dmg != null)
            {
                DealDamage(dmg, collider, travelDir);
                return true;
            }

            int colliderLayerBit = 1 << collider.gameObject.layer;
            bool blockedByObstacle = obstacleMask.value != 0 && (obstacleMask.value & colliderLayerBit) != 0;
            if (blockedByObstacle)
            {
                bool tileHit = TryDamageTileAt(hitPoint, travelDir);
                if (tileHit)
                {
                    if (destroyOnTileHit)
                    {
                        Destroy(gameObject);
                        return true;
                    }

                    _velocity = Vector2.zero;
                    return true;
                }

                if (destroyOnObstacleHit)
                {
                    Destroy(gameObject);
                    return true;
                }

                _velocity = Vector2.zero;
                return true;
            }

            return false;
        }

        void DealDamage(EnemyAI.IDamageable dmg, Collider2D hitCollider, Vector2 travelDir)
        {
            Vector2 impactDir = ResolveImpactDir(travelDir);
            Vector2 dir = impactDir.sqrMagnitude > 0.001f
                ? impactDir.normalized
                : Vector2.right;

            var playerHealth = dmg as PlayerHealth;
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(_damage, dir, _owner);
            }
            else
            {
                dmg.TakeDamage(_damage, dir);
            }

            EnemyHealth2D enemyHealth = null;
            if (dmg is Component dmgComponent)
            {
                enemyHealth = dmgComponent.GetComponentInParent<EnemyHealth2D>();
            }

            bool absorbed = enemyHealth != null && enemyHealth.LastDamageWasAbsorbed;
            int appliedDamage = 0;
            bool isCrit = false;

            if (!absorbed)
            {
                int baseDamage = IsOwnerPlayer() ? ResolveDamage(_owner, _damage) : _damage;
                var critResult = EvaluateCriticalHit(baseDamage);
                appliedDamage = critResult.Damage;
                isCrit = critResult.IsCritical;

                if (IsOwnerPlayer() && CombatTextManager.Instance)
                {
                    Vector3 popupPos = hitCollider ? hitCollider.bounds.center : transform.position;
                    CombatTextManager.Instance.SpawnDamage(appliedDamage, popupPos, isCrit);
                }
            }
            else if (IsOwnerPlayer())
            {
                Vector3 popupPos = hitCollider ? hitCollider.bounds.center : transform.position;
                AbilityEffectUtility.SpawnAbsorbedText(popupPos);
            }

            if (!absorbed)
            {
                EnemyAI.NotifyDamageDealt(dmg, _owner ? _owner : transform, appliedDamage);
            }

            Transform hitTransform = hitCollider ? hitCollider.transform : (dmg as Component)?.transform;
            _damageableHitCallback?.Invoke(hitTransform);
            Destroy(gameObject);
        }

        bool TryDamageTileAt(Vector2 position, Vector2 travelDir)
        {
            if (!damageTiles || !TileDestructionManager.I)
                return false;

            Vector2 dir = ResolveImpactDir(travelDir);
            if (dir.sqrMagnitude > 0.0001f)
                dir.Normalize();
            else
                dir = Vector2.right;

            float inset = Mathf.Max(0.02f, hitRadius * 0.5f);
            Vector2 samplePos = position + dir * inset;

            bool hit = false;
            if (TileDestructionManager.TryHitAtWorld(samplePos, Mathf.Max(1, tileDamage)))
            {
                hit = true;
            }
            else
            {
                float radius = Mathf.Max(0.2f, hitRadius);
                int tilesHit = TileDestructionManager.HitCircle(samplePos, radius, tileDamage);
                hit = tilesHit > 0;
            }

            if (hit)
            {
                _tileHitCallback?.Invoke(samplePos);
            }

            return hit;
        }

        Vector2 ResolveImpactDir(Vector2 travelDir)
        {
            if (travelDir.sqrMagnitude > 0.0001f)
                return travelDir;
            if (_lastTravelDir.sqrMagnitude > 0.0001f)
                return _lastTravelDir;
            if (_velocity.sqrMagnitude > 0.0001f)
                return _velocity;
            return Vector2.right;
        }

        int ResolveDamage(Transform owner, int baseDamage)
        {
            int finalDamage = Mathf.Max(1, baseDamage);
            if (!owner) return finalDamage;

            var melee = owner.GetComponent<PlayerMeleeHitbox>();
            if (!melee)
            {
                melee = owner.GetComponentInChildren<PlayerMeleeHitbox>();
            }

            if (melee != null)
            {
                finalDamage = Mathf.Max(1, melee.damage);
            }

            return finalDamage;
        }

        bool IsOwnerPlayer()
        {
            if (!_owner) return false;
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1 && _owner.gameObject.layer == playerLayer)
            {
                return true;
            }

            return _owner.GetComponent<PlayerMeleeHitbox>() != null || _owner.GetComponentInParent<PlayerMeleeHitbox>() != null;
        }

        (int Damage, bool IsCritical) EvaluateCriticalHit(int damage)
        {
            bool isCrit = false;

            if (!IsOwnerPlayer())
            {
                return (damage, false);
            }

            var melee = _owner ? _owner.GetComponent<PlayerMeleeHitbox>() ?? _owner.GetComponentInChildren<PlayerMeleeHitbox>() : null;
            if (melee == null)
            {
                return (damage, false);
            }

            bool critRolled = UnityEngine.Random.value < Mathf.Clamp01(melee.critChance);
            if (critRolled)
            {
                isCrit = true;
                damage = Mathf.RoundToInt(damage * Mathf.Max(1f, melee.critMultiplier));
                damage = Mathf.Max(1, damage);
            }

            return (damage, isCrit);
        }

        public void RegisterDamageableHitCallback(Action<Transform> callback)
        {
            _damageableHitCallback += callback;
        }

        public void ClearDamageableHitCallbacks()
        {
            _damageableHitCallback = null;
        }

        public void RegisterTileHitCallback(Action<Vector2> callback)
        {
            _tileHitCallback += callback;
        }

        public void ClearTileHitCallbacks()
        {
            _tileHitCallback = null;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.15f);
        }
#endif
    }
}







