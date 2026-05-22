using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Passive modifier that flashes the owner's sprites while active (same behaviour as SpriteFlashStep).
    /// </summary>
    [System.Serializable]
    [PassiveModifierDescription("Flashes the owner's sprite renderers using a configurable colour and frequency.")]
    public sealed class SpriteFlashModifier : PassiveAbilityModifier
    {
        [Header("Flash Settings")]
        [SerializeField]
        [Tooltip("Colour blended with the original sprite colours.")]
        private Color flashColor = new Color(1f, 0.2f, 0.2f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        private float flashMin = 0.2f;

        [SerializeField]
        [Range(0f, 1f)]
        private float flashMax = 0.85f;

        [SerializeField]
        [Tooltip("Flash cycles per second.")]
        private float flashFrequency = 6f;

        [SerializeField]
        [Tooltip("Include inactive children when gathering sprite renderers.")]
        private bool includeInactiveChildren = true;

        AbilityRunner _runner;
        List<SpriteRenderer> _sprites;
        List<Color> _originalColours;
        Coroutine _flashRoutine;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled || runner == null)
                return;

            _runner = runner;

            CollectSprites();
            if (_sprites == null || _sprites.Count == 0)
            {
                return;
            }

            _flashRoutine = runner.StartCoroutine(FlashRoutine());
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled)
                return;

            if (_flashRoutine != null && _runner != null)
            {
                _runner.StopCoroutine(_flashRoutine);
                _flashRoutine = null;
            }

            RestoreColours();
            _sprites = null;
            _originalColours = null;
            _runner = null;
        }

        void CollectSprites()
        {
            Transform root = _runner ? _runner.transform : null;
            if (!root)
            {
                _sprites = null;
                _originalColours = null;
                return;
            }

            _sprites = new List<SpriteRenderer>();
            root.GetComponentsInChildren(includeInactiveChildren, _sprites);
            _originalColours = new List<Color>(_sprites.Count);
            for (int i = 0; i < _sprites.Count; i++)
            {
                var sr = _sprites[i];
                if (SpriteColorUtility.TryGetOriginalColor(root, sr, out var baseColor))
                {
                    _originalColours.Add(baseColor);
                }
                else
                {
                    _originalColours.Add(sr ? sr.color : Color.white);
                }
            }
        }

        IEnumerator FlashRoutine()
        {
            float min = Mathf.Clamp01(Mathf.Min(flashMin, flashMax));
            float max = Mathf.Clamp01(Mathf.Max(flashMin, flashMax));
            float freq = Mathf.Max(0.01f, flashFrequency);

            while (true)
            {
                float pulse = (Mathf.Sin(Time.time * freq * Mathf.PI * 2f) + 1f) * 0.5f;
                float amount = Mathf.Lerp(min, max, pulse);

                for (int i = 0; i < _sprites.Count; i++)
                {
                    var sr = _sprites[i];
                    if (!sr)
                        continue;

                    Color baseColor = (i < _originalColours.Count) ? _originalColours[i] : sr.color;
                    sr.color = Color.Lerp(baseColor, flashColor, amount);
                }

                yield return null;
            }
        }

        void RestoreColours()
        {
            if (_sprites == null || _originalColours == null)
                return;

            for (int i = 0; i < _sprites.Count; i++)
            {
                var sr = _sprites[i];
                if (!sr)
                    continue;
                Color baseColor = (i < _originalColours.Count) ? _originalColours[i] : sr.color;
                sr.color = baseColor;
            }
        }
    }
}



