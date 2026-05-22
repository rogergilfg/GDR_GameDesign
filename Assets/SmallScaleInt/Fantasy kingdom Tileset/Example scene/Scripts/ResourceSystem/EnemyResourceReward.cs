using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grants the player the configured resources when the attached enemy dies.
/// Automatically mirrors every resource from the assigned database so designers
/// can simply type the desired amount per resource.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth2D))]
[AddComponentMenu("Resources/Enemy Resource Reward")]
[MovedFrom(true, null, null, "EnemyResourceReward")]
public sealed class EnemyResourceReward : MonoBehaviour
{
    [Tooltip("Database used to populate the list of available resources.")]
    [SerializeField] private ResourceDatabase resourceDatabase;

    [Tooltip("Optional override for the resource manager. Defaults to DynamicResourceManager.Instance.")]
    [SerializeField] private DynamicResourceManager resourceManagerOverride;

    [Tooltip("When enabled the reward will spawn floating resource feedback.")]
    [SerializeField] private bool showFeedback = true;

    [Tooltip("When enabled the granted resources also award tile/resource experience.")]
    [SerializeField] private bool awardExperience = true;

    [SerializeField]
    private List<ResourceRewardEntry> rewards = new List<ResourceRewardEntry>();

    [Serializable]
    private struct ResourceRewardEntry
    {
        public ResourceTypeDef resource;
        public int amount;
    }

    private EnemyHealth2D trackedHealth;
    private bool subscribed;

    private void Awake()
    {
        trackedHealth = GetComponent<EnemyHealth2D>();
        SyncRewardEntries();
    }

    private void OnEnable()
    {
        SyncRewardEntries();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SyncRewardEntries();
    }
#endif

    private void Subscribe()
    {
        if (trackedHealth == null || subscribed) return;
        trackedHealth.OnDied += HandleEnemyDied;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (trackedHealth == null || !subscribed) return;
        trackedHealth.OnDied -= HandleEnemyDied;
        subscribed = false;
    }

    private void HandleEnemyDied()
    {
        if (rewards == null || rewards.Count == 0)
        {
            return;
        }

        ResourceSet grant = BuildGrantSet();
        if (grant == null || grant.IsEmpty)
        {
            return;
        }

        DynamicResourceManager manager = resourceManagerOverride != null
            ? resourceManagerOverride
            : DynamicResourceManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning("EnemyResourceReward requires a DynamicResourceManager in the scene.", this);
            return;
        }

        manager.GrantResources(grant, transform.position, showFeedback, awardExperience);
    }

    private ResourceSet BuildGrantSet()
    {
        ResourceSet set = new ResourceSet();
        if (rewards == null) return set;

        for (int i = 0; i < rewards.Count; i++)
        {
            var entry = rewards[i];
            if (entry.resource == null) continue;
            if (entry.amount <= 0) continue;
            set.Set(entry.resource, entry.amount);
        }

        return set;
    }

    private void SyncRewardEntries()
    {
        if (resourceDatabase == null)
        {
            return;
        }

        IReadOnlyList<ResourceTypeDef> resources = resourceDatabase.Resources;
        if (resources == null || resources.Count == 0)
        {
            rewards.Clear();
            return;
        }

        if (rewards == null)
        {
            rewards = new List<ResourceRewardEntry>();
        }

        var ordered = new List<ResourceRewardEntry>(resources.Count);
        for (int i = 0; i < resources.Count; i++)
        {
            ResourceTypeDef resource = resources[i];
            if (resource == null)
            {
                continue;
            }

            int existingIndex = FindEntryIndex(resource);
            if (existingIndex >= 0)
            {
                ordered.Add(rewards[existingIndex]);
            }
            else
            {
                ordered.Add(new ResourceRewardEntry
                {
                    resource = resource,
                    amount = 0
                });
            }
        }

        rewards.Clear();
        rewards.AddRange(ordered);
    }

    private int FindEntryIndex(ResourceTypeDef resource)
    {
        if (resource == null || rewards == null)
        {
            return -1;
        }

        for (int i = 0; i < rewards.Count; i++)
        {
            if (rewards[i].resource == resource)
            {
                return i;
            }
        }
        return -1;
    }
}
}



