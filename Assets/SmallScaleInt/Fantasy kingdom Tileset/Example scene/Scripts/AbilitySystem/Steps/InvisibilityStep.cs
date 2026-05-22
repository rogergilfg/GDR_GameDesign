using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Temporarily hides the caster, plays entry/exit VFX, optionally clears threat, and breaks when damage is taken or dealt.")]
    [MovedFrom("AbilitySystem")]
    public sealed class InvisibilityStep : AbilityStep
    {
        static readonly Dictionary<Transform, ActiveSession> ActiveSessions = new();
        [Header("Timing")]
        [SerializeField]
        [Tooltip("How long the caster remains invisible. 0 keeps the effect until it is broken by other conditions.")]
        [Min(0f)]
        float duration = 5f;

        [Header("Activation Visuals")]
        [SerializeField]
        [Tooltip("Optional VFX spawned when invisibility starts.")]
        GameObject activationPrefab;

        [SerializeField]
        [Tooltip("Optional VFX spawned when invisibility ends.")]
        GameObject exitPrefab;

        [SerializeField]
        [Tooltip("Parents the activation prefab under the caster transform.")]
        bool parentActivationToOwner = true;

        [SerializeField]
        [Tooltip("Parents the exit prefab under the caster transform.")]
        bool parentExitToOwner = true;

        [SerializeField]
        [Tooltip("Automatically destroys the activation prefab after this many seconds. 0 lets the prefab clean itself up.")]
        [Min(0f)]
        float activationPrefabLifetime = 2f;

        [SerializeField]
        [Tooltip("Automatically destroys the exit prefab after this many seconds. 0 lets the prefab clean itself up.")]
        [Min(0f)]
        float exitPrefabLifetime = 2f;

        [SerializeField]
        [Tooltip("When enabled, recasting while invisible exits stealth instead of reapplying it.")]
        bool recastCancelsStealth = true;

        public bool RecastCancelsStealth => recastCancelsStealth;

        [Header("Break Conditions")]
        [SerializeField]
        [Tooltip("When enabled, taking damage immediately ends the invisibility.")]
        bool breakOnDamageTaken = true;

        [SerializeField]
        [Tooltip("When enabled, dealing damage immediately ends the invisibility.")]
        bool breakOnDamageDealt = true;

        [Header("Threat Handling")]
        [SerializeField]
        [Tooltip("Clears the caster from all EnemyAI threat tables when the caster is a player or neutral NPC.")]
        bool clearThreatForFriendlyCasters = true;

        [Header("Movement & Posture")]
        [SerializeField]
        [Tooltip("Run/walk speed multiplier applied to the player while invisible (1 = unchanged).")]
        [Range(0f, 2f)]
        float playerSpeedMultiplier = 0.75f;

        [SerializeField]
        [Tooltip("If enabled, forces GenericTopDownController.isCrouching while the caster is invisible.")]
        bool crouchWhileInvisible = true;

        [Header("Sprite Renderers")]
        [SerializeField]
        [Tooltip("Primary sprite renderer for the caster. Leave empty to auto-detect.")]
        SpriteRenderer primarySprite;

        [SerializeField]
        [Tooltip("Additional sprite renderers (gear pieces, attachments) that should be faded.")]
        List<SpriteRenderer> additionalRenderers = new List<SpriteRenderer>();

        [SerializeField]
        [Tooltip("Automatically include all child sprite renderers.")]
        bool includeChildRenderers = true;

        [SerializeField]
        [Tooltip("Include inactive children when collecting sprite renderers.")]
        bool includeInactiveChildren = true;

        [Header("Tint Multipliers")]
        [SerializeField]
        [Tooltip("Colour multiplier (RGB) and alpha multiplier applied when the caster is a player.")]
        Color playerTint = new Color(1f, 1f, 1f, 0.35f);

        [SerializeField]
        [Tooltip("Colour multiplier (RGB) and alpha multiplier applied when the caster is neutral.")]
        Color neutralTint = new Color(1f, 1f, 1f, 0.35f);

        [SerializeField]
        [Tooltip("Colour multiplier (RGB) and alpha multiplier applied when the caster is an enemy.")]
        Color enemyTint = new Color(1f, 1f, 1f, 0f);

        [SerializeField]
        [Tooltip("Colour multiplier used for actors with Unknown owner kind.")]
        Color unknownTint = new Color(1f, 1f, 1f, 0.2f);

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (context == null || !context.Transform)
            {
                yield break;
            }

            Transform owner = context.Transform;
            if (recastCancelsStealth && TryCancelStealth(owner))
            {
                yield break;
            }
            GenericTopDownController controller = context.TopDownController;
            float originalRunSpeed = 0f;
            float originalWalkSpeed = 0f;
            bool speedModified = false;
            bool crouchApplied = false;
            bool stealthRegistered = false;
            var renderers = new List<SpriteRenderer>(8);
            var originals = new List<Color>(8);
            GatherRenderers(owner, renderers);
            CacheOriginalColours(renderers, originals);

            bool visualsApplied = ApplyTint(renderers, originals, context.OwnerKind);
            SpawnPrefab(activationPrefab, owner, parentActivationToOwner, activationPrefabLifetime);

            if (clearThreatForFriendlyCasters && (context.IsPlayerControlled || context.IsNeutralControlled))
            {
                ClearThreat(owner);
            }

            if (owner)
            {
                AbilityStealthUtility.Register(owner);
                stealthRegistered = true;
            }

            if (controller && context.IsPlayerControlled)
            {
                float multiplier = Mathf.Clamp(playerSpeedMultiplier, 0f, 2f);
                if (!Mathf.Approximately(multiplier, 1f))
                {
                    originalRunSpeed = controller.runSpeed;
                    originalWalkSpeed = controller.walkSpeed;
                    controller.runSpeed = originalRunSpeed * multiplier;
                    controller.walkSpeed = originalWalkSpeed * multiplier;
                    speedModified = true;
                }

                if (crouchWhileInvisible)
                {
                    controller.SetCrouchOverride(true);
                    crouchApplied = true;
                }
            }

            bool breakRequested = false;
            void RequestBreak() => breakRequested = true;

            PlayerHealth playerHealth = context.PlayerHealth;
            EnemyHealth2D enemyHealth = context.EnemyHealth;
            NeutralNpcAI neutralAI = context.NeutralAI;
            System.Action<int> damageTakenHandler = null;
            UnityAction neutralDamageAction = null;
            System.Action<Transform, EnemyAI.IDamageable, float> damageDealtHandler = null;

            if (breakOnDamageTaken)
            {
                if (playerHealth)
                {
                    damageTakenHandler = _ => RequestBreak();
                    playerHealth.OnDamageTaken += damageTakenHandler;
                }
                else if (enemyHealth)
                {
                    damageTakenHandler = _ => RequestBreak();
                    enemyHealth.OnDamageTaken += damageTakenHandler;
                }
                else if (neutralAI)
                {
                    neutralDamageAction = () => RequestBreak();
                    neutralAI.onDamaged.AddListener(neutralDamageAction);
                }
            }

            if (breakOnDamageDealt)
            {
                damageDealtHandler = (attacker, _, __) =>
                {
                    if (MatchesOwner(attacker, owner))
                    {
                        RequestBreak();
                    }
                };
                EnemyAI.DamageDealt += damageDealtHandler;
            }

            float elapsed = 0f;
            float total = Mathf.Max(0f, duration);
            bool infiniteDuration = duration <= 0f;
            RegisterActiveSession(owner, () => breakRequested = true);

            try
            {
                while (!breakRequested && !context.CancelRequested)
                {
                    if (!infiniteDuration)
                    {
                        elapsed += Time.deltaTime;
                        if (elapsed >= total)
                        {
                            break;
                        }
                    }

                    yield return null;
                }
            }
            finally
            {
                ActiveSessions.Remove(owner);
                if (damageTakenHandler != null)
                {
                    if (playerHealth)
                    {
                        playerHealth.OnDamageTaken -= damageTakenHandler;
                    }
                    else if (enemyHealth)
                    {
                        enemyHealth.OnDamageTaken -= damageTakenHandler;
                    }
                }

                if (neutralDamageAction != null && neutralAI != null)
                {
                    neutralAI.onDamaged.RemoveListener(neutralDamageAction);
                }

                if (damageDealtHandler != null)
                {
                    EnemyAI.DamageDealt -= damageDealtHandler;
                }

                if (visualsApplied)
                {
                    RestoreColours(renderers, originals);
                }

                if (speedModified && controller)
                {
                    controller.runSpeed = originalRunSpeed;
                    controller.walkSpeed = originalWalkSpeed;
                }

                if (crouchApplied && controller)
                {
                    controller.SetCrouchOverride(false);
                }

                if (stealthRegistered)
                {
                    AbilityStealthUtility.Unregister(owner);
                }

                SpawnPrefab(exitPrefab, owner, parentExitToOwner, exitPrefabLifetime);
            }
        }

        void GatherRenderers(Transform owner, List<SpriteRenderer> results)
        {
            results.Clear();
            if (!owner) return;

            var unique = new HashSet<SpriteRenderer>();
            void TryAdd(SpriteRenderer sr)
            {
                if (!sr) return;
                if (unique.Add(sr))
                {
                    results.Add(sr);
                }
            }

            if (primarySprite)
            {
                TryAdd(primarySprite);
            }

            if (additionalRenderers != null)
            {
                for (int i = 0; i < additionalRenderers.Count; i++)
                {
                    TryAdd(additionalRenderers[i]);
                }
            }

            if (includeChildRenderers)
            {
                var children = owner.GetComponentsInChildren<SpriteRenderer>(includeInactiveChildren);
                for (int i = 0; i < children.Length; i++)
                {
                    TryAdd(children[i]);
                }
            }
            else
            {
                TryAdd(owner.GetComponent<SpriteRenderer>());
            }

            if (results.Count == 0)
            {
                var fallback = owner.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < fallback.Length; i++)
                {
                    TryAdd(fallback[i]);
                }
            }
        }

        static void CacheOriginalColours(List<SpriteRenderer> renderers, List<Color> store)
        {
            store.Clear();
            for (int i = 0; i < renderers.Count; i++)
            {
                var sr = renderers[i];
                store.Add(sr ? sr.color : Color.white);
            }
        }

        bool ApplyTint(List<SpriteRenderer> renderers, List<Color> originals, AbilityActorKind ownerKind)
        {
            if (renderers.Count == 0)
            {
                return false;
            }

            Color tint = ownerKind switch
            {
                AbilityActorKind.Player => playerTint,
                AbilityActorKind.Neutral => neutralTint,
                AbilityActorKind.Enemy => enemyTint,
                _ => unknownTint
            };

            for (int i = 0; i < renderers.Count; i++)
            {
                var sr = renderers[i];
                if (!sr) continue;

                Color baseColor = i < originals.Count ? originals[i] : sr.color;
                sr.color = MultiplyColor(baseColor, tint);
            }

            return true;
        }

        static Color MultiplyColor(Color original, Color tint)
        {
            return new Color(
                original.r * tint.r,
                original.g * tint.g,
                original.b * tint.b,
                original.a * tint.a);
        }

        static void RestoreColours(List<SpriteRenderer> renderers, List<Color> originals)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                var sr = renderers[i];
                if (!sr) continue;

                Color source = i < originals.Count ? originals[i] : sr.color;
                sr.color = source;
            }
        }

        static void SpawnPrefab(GameObject prefab, Transform owner, bool parentToOwner, float cleanupDelay)
        {
            if (!prefab || !owner) return;

            GameObject instance = Object.Instantiate(prefab, owner.position, owner.rotation);
            if (parentToOwner && instance)
            {
                instance.transform.SetParent(owner, true);
            }

            if (cleanupDelay > 0f)
            {
                Object.Destroy(instance, cleanupDelay);
            }
        }

        static bool MatchesOwner(Transform attacker, Transform owner)
        {
            if (!attacker || !owner) return false;
            if (attacker == owner) return true;
            if (attacker.IsChildOf(owner)) return true;
            if (owner.IsChildOf(attacker)) return true;
            return false;
        }

        public static bool TryCancelStealth(Transform owner)
        {
            if (!owner) return false;
            if (ActiveSessions.TryGetValue(owner, out var session) && session != null)
            {
                session.TriggerExit?.Invoke();
                return true;
            }
            return false;
        }

        ActiveSession RegisterActiveSession(Transform owner, System.Action triggerExit)
        {
            if (!owner) return null;
            var session = new ActiveSession
            {
                TriggerExit = triggerExit
            };
            ActiveSessions[owner] = session;
            return session;
        }

        sealed class ActiveSession
        {
            public System.Action TriggerExit;
        }

        static void ClearThreat(Transform owner)
        {
            if (!owner) return;
            var enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (!enemy) continue;
                enemy.UnregisterHostile(owner);
            }
        }
    }
}





