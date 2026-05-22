using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Plays a one-shot AudioClip at the caster or a chosen transform with configurable volume and mixer routing.")]
    [MovedFrom("AbilitySystem")]
    public sealed class PlayAudioClipStep : AbilityStep
    {
        [SerializeField]
        [Tooltip("Audio clip played when the step executes.")]
        private AudioClip clip;

        [SerializeField]
        [Tooltip("Volume multiplier applied to the clip.")]
        [Range(0f, 1f)]
        private float volume = 1f;

        [SerializeField]
        [Tooltip("If true, the clip follows the owner. Otherwise it plays at the owner's position.")]
        private bool attachToOwner = false;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (!clip) yield break;

            if (attachToOwner)
            {
                var source = context.Transform.GetComponent<AudioSource>();
                if (!source)
                {
                    source = context.Transform.gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                }
                source.clip = clip;
                source.volume = volume;
                source.Play();
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, context.Transform.position, volume);
            }

            yield break;
        }
    }
}





