using UnityEngine;

namespace SmallScale.FantasyKingdomTileset
{
    /// <summary>
    /// Simple helper you can attach to any prefab with an Animator.
    /// When <see cref="enableLoop"/> is toggled on, the current state on layer 0
    /// is restarted as it reaches the end, effectively forcing a loop even if
    /// the underlying clip is set to play once.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimatorLoopToggle : MonoBehaviour
    {
        [Header("Loop Control")]
        [Tooltip("If true, the current state on layer 0 will be restarted when it finishes, forcing a loop.")]
        public bool enableLoop = false;

        [Tooltip("Normalized time at which to restart the state (0 = immediately, 1 = when fully finished).")]
        [Range(0.8f, 1.1f)]
        public float restartThreshold = 0.99f;

        [Tooltip("Animator layer index to monitor (0 = default).")]
        [Min(0)]
        public int layerIndex = 0;

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (!enableLoop || _animator == null)
            {
                return;
            }

            if (layerIndex < 0 || layerIndex >= _animator.layerCount)
            {
                return;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(layerIndex);

            // If the underlying state already loops, we don't need to force anything.
            if (state.loop)
            {
                return;
            }

            if (state.normalizedTime >= restartThreshold)
            {
                _animator.Play(state.fullPathHash, layerIndex, 0f);
            }
        }
    }
}




