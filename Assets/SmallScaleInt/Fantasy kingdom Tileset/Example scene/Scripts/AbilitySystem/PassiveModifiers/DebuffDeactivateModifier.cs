using System.Collections.Generic;
using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Temporarily disables the target's logic, freezing enemies/companions/turrets without hiding them.
    /// </summary>
    [System.Serializable]
    [PassiveModifierDescription("Temporarily deactivates the owner's AI/logic so they cannot move or act.")]
    public sealed class DebuffDeactivateModifier : PassiveAbilityModifier
    {
        [Header("Targets")]
        [SerializeField]
        private bool pauseEnemyAI = true;

        [SerializeField]
        private bool disableNeutralAI = true;

        [SerializeField]
        private bool disableCompanionAI = true;

        [SerializeField]
        private bool disableTurretAI = true;

        [SerializeField]
        private bool disableGenericController = true;

        [Header("Ability Control")]
        [SerializeField]
        private bool disableAbilityRunner = true;

        [Header("Visuals & Physics")]
        [SerializeField]
        private bool freezeAnimator = true;

        [SerializeField]
        private bool freezeRigidbody = true;

        private EnemyAI _pausedEnemy;
        private readonly List<BehaviourState> _disabledBehaviours = new List<BehaviourState>();
        private bool _animatorFrozen;
        private bool _animatorWasEnabled;
        private Animator _frozenAnimator;
        private bool _rigidbodyFrozen;
        private Rigidbody2D _frozenBody;
        private Vector2 _storedVelocity;
        private float _storedAngularVelocity;
        private RigidbodyConstraints2D _storedConstraints;
        private AbilityRunner _cachedRunner;
        private bool _abilityRunnerDisabled;
        private bool _abilityRunnerWasEnabled;

        struct BehaviourState
        {
            public Behaviour Behaviour;
            public bool WasEnabled;
        }

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled || runner == null)
                return;

            _cachedRunner = runner;

            if (pauseEnemyAI && runner.CachedEnemyAI)
            {
                _pausedEnemy = runner.CachedEnemyAI;
                _pausedEnemy.SetExternalPause(true);
            }

            if (disableNeutralAI)
            {
                DisableBehaviour(runner.CachedNeutralNpcAI ?? runner.GetComponent<NeutralNpcAI>());
            }

            if (disableCompanionAI)
            {
                DisableBehaviour(runner.GetComponent<CompanionAI>());
            }

            if (disableTurretAI)
            {
                DisableBehaviour(runner.GetComponent<TurretAI>());
            }

            if (disableGenericController)
            {
                DisableBehaviour(runner.CachedTopDownController);
            }

            if (disableAbilityRunner)
            {
                _abilityRunnerWasEnabled = runner.enabled;
                if (runner.enabled)
                {
                    runner.CancelAllAbilities();
                    runner.enabled = false;
                    _abilityRunnerDisabled = true;
                }
                else
                {
                    _abilityRunnerDisabled = false;
                }
            }

            if (freezeAnimator)
            {
                FreezeAnimator(runner.CachedAnimator ?? runner.GetComponentInChildren<Animator>());
            }

            if (freezeRigidbody)
            {
                FreezeRigidbody(runner.CachedRigidbody2D ?? runner.GetComponent<Rigidbody2D>());
            }
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled)
                return;

            var targetRunner = runner ? runner : _cachedRunner;

            if (_pausedEnemy)
            {
                _pausedEnemy.SetExternalPause(false);
                _pausedEnemy = null;
            }

            for (int i = 0; i < _disabledBehaviours.Count; i++)
            {
                BehaviourState state = _disabledBehaviours[i];
                if (state.Behaviour)
                {
                    state.Behaviour.enabled = state.WasEnabled;
                }
            }
            _disabledBehaviours.Clear();

            if (_animatorFrozen && _frozenAnimator)
            {
                _frozenAnimator.enabled = _animatorWasEnabled;
            }
            _animatorFrozen = false;
            _frozenAnimator = null;

            if (_rigidbodyFrozen && _frozenBody)
            {
                _frozenBody.constraints = _storedConstraints;
                _frozenBody.linearVelocity = _storedVelocity;
                _frozenBody.angularVelocity = _storedAngularVelocity;
            }
            _rigidbodyFrozen = false;
            _frozenBody = null;

            if (disableAbilityRunner && targetRunner && _abilityRunnerDisabled)
            {
                targetRunner.enabled = _abilityRunnerWasEnabled;
            }
            _abilityRunnerDisabled = false;
            _cachedRunner = null;
        }

        void DisableBehaviour(Behaviour behaviour)
        {
            if (!behaviour)
                return;

            for (int i = 0; i < _disabledBehaviours.Count; i++)
            {
                if (_disabledBehaviours[i].Behaviour == behaviour)
                    return;
            }

            _disabledBehaviours.Add(new BehaviourState
            {
                Behaviour = behaviour,
                WasEnabled = behaviour.enabled
            });

            behaviour.enabled = false;
        }

        void FreezeAnimator(Animator animator)
        {
            if (!animator || _animatorFrozen)
                return;

            _animatorFrozen = true;
            _frozenAnimator = animator;
            _animatorWasEnabled = animator.enabled;
            animator.enabled = false;
        }

        void FreezeRigidbody(Rigidbody2D body)
        {
            if (!body || _rigidbodyFrozen)
                return;

            _rigidbodyFrozen = true;
            _frozenBody = body;
            _storedVelocity = body.linearVelocity;
            _storedAngularVelocity = body.angularVelocity;
            _storedConstraints = body.constraints;

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }
}



