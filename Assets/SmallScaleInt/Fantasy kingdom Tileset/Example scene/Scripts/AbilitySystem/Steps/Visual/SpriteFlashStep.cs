using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Briefly tints or flashes sprite renderers to highlight the caster or a target using configurable colors.")]
    [MovedFrom("AbilitySystem")]
    public sealed class SpriteFlashStep : AbilityStep
    {
        private enum TargetMode
        {
            Owner,
            Custom
        }

        [Header("Target")]
        [SerializeField]
        private TargetMode targetMode = TargetMode.Owner;

        [SerializeField]
        private Transform customTarget;

        [SerializeField]
        [Tooltip("Include sprites on inactive children when gathering renderers.")]
        private bool includeInactiveChildren = true;

        [Header("Flash Settings")]
        [SerializeField]
        [Tooltip("Duration of the flashing effect (seconds).")]
        private float duration = 4f;

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

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Transform target = ResolveTarget(context);
            if (!target)
            {
                yield break;
            }

            List<SpriteRenderer> sprites = new List<SpriteRenderer>();
            target.GetComponentsInChildren(includeInactiveChildren, sprites);

            if (sprites.Count == 0)
            {
                yield break;
            }

            List<Color> originals = new List<Color>(sprites.Count);
            for (int i = 0; i < sprites.Count; i++)
            {
                var sr = sprites[i];
                if (SpriteColorUtility.TryGetOriginalColor(target, sr, out var baseColor))
                {
                    originals.Add(baseColor);
                }
                else
                {
                    originals.Add(sr ? sr.color : Color.white);
                }
            }

            float elapsed = 0f;
            float total = Mathf.Max(0.01f, duration);
            float min = Mathf.Clamp01(Mathf.Min(flashMin, flashMax));
            float max = Mathf.Clamp01(Mathf.Max(flashMin, flashMax));
            float freq = Mathf.Max(0.01f, flashFrequency);

            while (elapsed < total)
            {
                if (context.CancelRequested)
                {
                    RestoreColours(sprites, originals);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float pulse = (Mathf.Sin(elapsed * freq * Mathf.PI * 2f) + 1f) * 0.5f;
                float amount = Mathf.Lerp(min, max, pulse);

                for (int i = 0; i < sprites.Count; i++)
                {
                    var sr = sprites[i];
                    if (!sr) continue;

                    Color baseColor = (i < originals.Count) ? originals[i] : sr.color;
                    sr.color = Color.Lerp(baseColor, flashColor, amount);
                }

                yield return null;
            }

            RestoreColours(sprites, originals);
        }

        void RestoreColours(List<SpriteRenderer> sprites, List<Color> originals)
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                var sr = sprites[i];
                if (!sr) continue;
                Color baseColor = (i < originals.Count) ? originals[i] : sr.color;
                sr.color = baseColor;
            }
        }

        Transform ResolveTarget(AbilityRuntimeContext context)
        {
            return targetMode switch
            {
                TargetMode.Custom => customTarget ? customTarget : context.Transform,
                _ => context.Transform
            };
        }
    }
}






