using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps gear animators aligned with the player's primary animator so equipment visuals
/// stay in lockstep with the underlying character animation state.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "GearAnimatorSynchronizer")]
public class GearAnimatorSynchronizer : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Animator that drives the core character animations.")]
    private Animator mainAnimator;

    [SerializeField]
    [Tooltip("Child animators that should mirror the main animator's state.")]
    private List<Animator> gearAnimators = new List<Animator>();

    /// <summary>
    /// Assigns the animator that acts as the synchronization source.
    /// </summary>
    public void SetMainAnimator(Animator animator)
    {
        mainAnimator = animator;
    }

    /// <summary>
    /// Registers a new gear animator to be synchronized with the main animator.
    /// </summary>
    public void RegisterGearAnimator(Animator animator)
    {
        if (animator == null)
        {
            return;
        }

        if (gearAnimators.Contains(animator))
        {
            return;
        }

        ConfigureGearAnimator(animator);
        gearAnimators.Add(animator);

        SynchronizeAnimatorImmediately(animator);
    }

    /// <summary>
    /// Unregisters an animator so it is no longer synchronized.
    /// </summary>
    public void UnregisterGearAnimator(Animator animator)
    {
        if (animator == null)
        {
            return;
        }

        gearAnimators.Remove(animator);
    }

    /// <summary>
    /// Removes all registered gear animators.
    /// </summary>
    public void ClearGearAnimators()
    {
        gearAnimators.Clear();
    }

    private void Reset()
    {
        mainAnimator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (mainAnimator == null)
        {
            mainAnimator = GetComponent<Animator>();
        }

        gearAnimators.RemoveAll(animator => animator == null);

        foreach (Animator animator in gearAnimators)
        {
            ConfigureGearAnimator(animator);
        }
    }

    private void LateUpdate()
    {
        if (mainAnimator == null || gearAnimators.Count == 0)
        {
            return;
        }

        int layerCount = mainAnimator.layerCount;

        foreach (Animator gearAnimator in gearAnimators)
        {
            if (!CanSynchronize(gearAnimator))
            {
                continue;
            }

            SynchronizeAnimatorInternal(gearAnimator, layerCount);
        }
    }

    /// <summary>
    /// Forces a specific gear animator to immediately match the main animator.
    /// This is useful right after swapping controllers so the visuals do not
    /// momentarily revert to their default facing direction.
    /// </summary>
    public void SynchronizeAnimatorImmediately(Animator gearAnimator)
    {
        if (!CanSynchronize(gearAnimator))
        {
            return;
        }

        int layerCount = mainAnimator.layerCount;
        SynchronizeAnimatorInternal(gearAnimator, layerCount);
    }

    private void SynchronizeAnimatorInternal(Animator gearAnimator, int layerCount)
    {
        SynchronizeParameters(gearAnimator);

        gearAnimator.speed = mainAnimator.speed;
        gearAnimator.updateMode = mainAnimator.updateMode;

        int synchronizedLayers = Mathf.Min(layerCount, gearAnimator.layerCount);

        for (int layer = 0; layer < synchronizedLayers; layer++)
        {
            AnimatorStateInfo stateInfo = mainAnimator.GetCurrentAnimatorStateInfo(layer);
            gearAnimator.Play(stateInfo.fullPathHash, layer, stateInfo.normalizedTime);
            gearAnimator.SetLayerWeight(layer, mainAnimator.GetLayerWeight(layer));
        }

        // Force the animator to sample immediately so there is no one-frame delay
        // between the main animator advancing and the gear animator catching up.
        gearAnimator.Update(0f);
    }

    private bool CanSynchronize(Animator gearAnimator)
    {
        if (mainAnimator == null || gearAnimator == null)
        {
            return false;
        }

        if (gearAnimator.runtimeAnimatorController == null)
        {
            return false;
        }

        if (!mainAnimator.isInitialized || !gearAnimator.isInitialized)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Copies parameter values from the main animator so directional blend trees
    /// and other state machines evaluate consistently across gear animators.
    /// </summary>
    private void SynchronizeParameters(Animator gearAnimator)
    {
        if (!mainAnimator.isInitialized || !gearAnimator.isInitialized)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in mainAnimator.parameters)
        {
            int parameterHash = parameter.nameHash;

            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    gearAnimator.SetFloat(parameterHash, mainAnimator.GetFloat(parameterHash));
                    break;
                case AnimatorControllerParameterType.Int:
                    gearAnimator.SetInteger(parameterHash, mainAnimator.GetInteger(parameterHash));
                    break;
                case AnimatorControllerParameterType.Bool:
                    gearAnimator.SetBool(parameterHash, mainAnimator.GetBool(parameterHash));
                    break;
            }
        }
    }

    private static void ConfigureGearAnimator(Animator animator)
    {
        if (animator == null)
        {
            return;
        }

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.keepAnimatorStateOnDisable = true;
    }
}



}






