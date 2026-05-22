using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Temporarily adjusts player stats (strength, defense, health, intelligence, knowledge, weapon damage) and restores them afterwards.")]
    [MovedFrom("AbilitySystem")]
    public sealed class ModifyPlayerStatsStep : AbilityStep
    {
        private enum ModificationMode
        {
            AddFlat,
            AddPercentOfCurrent
        }

        [System.Serializable]
        struct StatModifier
        {
            [Tooltip("Enable this stat modification.")]
            public bool enabled;

            [Tooltip("Choose whether to add a flat value or a percentage of the current stat.")]
            public ModificationMode mode;

            [Tooltip("Flat value added or percentage amount. Values greater than 1 are interpreted as whole percentages (e.g. 25 = +25%).")]
            public float value;

            [Tooltip("When Percentage mode yields zero (e.g. current stat is 0), this flat value is applied instead. 0 disables the fallback.")]
            public int fallbackFlatValue;

            [Tooltip("Minimum absolute delta applied when the computed change is non-zero. 0 disables the clamp.")]
            public int minimumAbsolute;

            public int ComputeDelta(int currentValue)
            {
                if (!enabled) return 0;

                float raw = 0f;
                switch (mode)
                {
                    case ModificationMode.AddPercentOfCurrent:
                        float percent = Mathf.Abs(value) > 1f ? value * 0.01f : value;
                        raw = currentValue * percent;
                        break;
                    default:
                        raw = value;
                        break;
                }

                int delta = Mathf.RoundToInt(raw);
                if (delta == 0)
                {
                    if (mode == ModificationMode.AddPercentOfCurrent && fallbackFlatValue != 0)
                    {
                        delta = fallbackFlatValue;
                    }
                    else if (!Mathf.Approximately(raw, 0f))
                    {
                        delta = raw > 0f ? 1 : -1;
                    }
                }

                if (minimumAbsolute > 0 && delta != 0)
                {
                    int sign = delta > 0 ? 1 : -1;
                    delta = sign * Mathf.Max(minimumAbsolute, Mathf.Abs(delta));
                }

                return delta;
            }
        }

        [SerializeField]
        [Tooltip("Duration the modifiers remain active (seconds).")]
        private float duration = 4f;

        [Header("Primary Stats")]
        [SerializeField] private StatModifier strength;
        [SerializeField] private StatModifier defense;
        [SerializeField] private StatModifier health;
        [SerializeField] private StatModifier intelligence;
        [SerializeField] private StatModifier knowledge;

        [Header("Offensive Stats")]
        [SerializeField] private StatModifier weaponDamage;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            PlayerStats playerStats = ResolvePlayerStats(context);
            if (!playerStats)
            {
                yield break;
            }

            var snapshot = playerStats.CurrentStats;

            var bonus = new PlayerStats.PlayerStatBonus
            {
                Strength = strength.ComputeDelta(snapshot.Strength),
                Defense = defense.ComputeDelta(snapshot.Defense),
                Health = health.ComputeDelta(snapshot.Health),
                Intelligence = intelligence.ComputeDelta(snapshot.Intelligence),
                Knowledge = knowledge.ComputeDelta(snapshot.Knowledge),
                WeaponDamage = weaponDamage.ComputeDelta(snapshot.WeaponDamage)
            };

            if (bonus.IsZero)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"ModifyPlayerStatsStep on '{context.Runner.name}' produced no stat change. Check that enabled stats have non-zero values or adjust fallback settings.", context.Runner);
#endif
                yield break;
            }

            playerStats.ApplyTemporaryStatBonus(in bonus);
            try
            {
                float remaining = Mathf.Max(0f, duration);
                if (remaining <= 0f)
                {
                    // Keep the bonus for at least one frame so dependent systems can observe it.
                    yield return null;
                }
                else
                {
                    float end = Time.time + remaining;
                    while (Time.time < end)
                    {
                        if (context.CancelRequested)
                        {
                            break;
                        }

                        yield return null;
                    }
                }
            }
            finally
            {
                var removal = bonus.Negated();
                playerStats.ApplyTemporaryStatBonus(in removal);
            }
        }

        static PlayerStats ResolvePlayerStats(AbilityRuntimeContext context)
        {
            if (context.Owner)
            {
                var stats = context.Owner.GetComponentInParent<PlayerStats>();
                if (stats) return stats;
            }

            return PlayerStats.Instance;
        }
    }
}





