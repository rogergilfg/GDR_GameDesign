using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

/// <summary>
/// Manages the passive buff panel, displaying all active passive abilities.
/// Shows permanent passives and proc-based buffs with countdown and stacks.
/// </summary>
[MovedFrom(true, null, null, "PassiveBuffPanelController")]
public class PassiveBuffPanelController : MonoBehaviour
{
    public static PassiveBuffPanelController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform buffContainer;
    [SerializeField] private GameObject buffIconPrefab;

    [Header("Settings")]
    [SerializeField] private float cleanupInterval = 0.5f;

    private readonly Dictionary<AbilityDefinition, PassiveBuffUI> _activeBuffs = new();
    private readonly List<PassiveBuffUI> _toRemove = new();
    private float _nextCleanupTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        if (Time.time >= _nextCleanupTime)
        {
            CleanupExpiredBuffs();
            _nextCleanupTime = Time.time + cleanupInterval;
        }
    }

    /// <summary>
    /// Register a permanent passive ability (no proc trigger).
    /// </summary>
    public void RegisterPermanentPassive(AbilityDefinition ability)
    {
        if (!ability || !ability.IsPassive) return;
        if (_activeBuffs.ContainsKey(ability)) return;

        // Check if this ability has any proc triggers
        bool hasProc = false;
        if (ability.PassiveModifiers != null)
        {
            foreach (var modifier in ability.PassiveModifiers)
            {
                if (modifier is ProcTriggerModifier)
                {
                    hasProc = true;
                    break;
                }
            }
        }

        // Only show permanent passives if they don't have proc triggers
        // (proc triggers will be shown when they activate)
        if (hasProc) return;

        CreateBuffIcon(ability, null, 0f, 0f, 0);
    }

    /// <summary>
    /// Register or update a proc-based passive ability.
    /// </summary>
    public void RegisterProcPassive(AbilityDefinition ability, ProcTriggerModifier procTrigger, float duration, float endTime, int currentStacks)
    {
        if (!ability || procTrigger == null) return;

        if (_activeBuffs.TryGetValue(ability, out PassiveBuffUI existingBuff))
        {
            // Update existing buff
            existingBuff.UpdateStackCount(currentStacks);
            existingBuff.RefreshDuration(endTime);
        }
        else
        {
            // Create new buff icon
            CreateBuffIcon(ability, procTrigger, duration, endTime, currentStacks);
        }
    }

    /// <summary>
    /// Register or update a timed passive ability (buff/debuff with duration).
    /// </summary>
    public void RegisterDebuff(AbilityDefinition ability, float duration, float endTime)
    {
        if (!ability) return;

        if (_activeBuffs.TryGetValue(ability, out PassiveBuffUI existingBuff))
        {
            // Update existing buff (refresh duration)
            existingBuff.RefreshDuration(endTime);
        }
        else
        {
            // Create new buff icon
            CreateDebuffIcon(ability, duration, endTime);
        }
    }

    /// <summary>
    /// Update stack count for an existing buff.
    /// </summary>
    public void UpdateBuffStacks(AbilityDefinition ability, int currentStacks)
    {
        if (_activeBuffs.TryGetValue(ability, out PassiveBuffUI buff))
        {
            buff.UpdateStackCount(currentStacks);
        }
    }

    /// <summary>
    /// Refresh duration for an existing buff (when proc refreshes).
    /// </summary>
    public void RefreshBuffDuration(AbilityDefinition ability, float newEndTime)
    {
        if (_activeBuffs.TryGetValue(ability, out PassiveBuffUI buff))
        {
            buff.RefreshDuration(newEndTime);
        }
    }

    /// <summary>
    /// Unregister a passive ability (when it's removed or deactivated).
    /// </summary>
    public void UnregisterPassive(AbilityDefinition ability)
    {
        if (!ability) return;

        if (_activeBuffs.TryGetValue(ability, out PassiveBuffUI buff))
        {
            buff.Deactivate();
            Destroy(buff.gameObject);
            _activeBuffs.Remove(ability);
        }
    }

    /// <summary>
    /// Clear all buffs.
    /// </summary>
    public void ClearAllBuffs()
    {
        foreach (var buff in _activeBuffs.Values)
        {
            if (buff) Destroy(buff.gameObject);
        }
        _activeBuffs.Clear();
    }

    private void CreateBuffIcon(AbilityDefinition ability, ProcTriggerModifier procTrigger, float duration, float endTime, int currentStacks)
    {
        if (!buffIconPrefab || !buffContainer) return;

        GameObject iconObj = Instantiate(buffIconPrefab, buffContainer);
        PassiveBuffUI buffUI = iconObj.GetComponent<PassiveBuffUI>();

        if (!buffUI)
        {
            buffUI = iconObj.AddComponent<PassiveBuffUI>();
        }

        if (procTrigger != null)
        {
            buffUI.Initialize(ability, procTrigger, duration, endTime, currentStacks);
        }
        else
        {
            buffUI.Initialize(ability);
        }

        _activeBuffs[ability] = buffUI;
    }

    private void CreateDebuffIcon(AbilityDefinition ability, float duration, float endTime)
    {
        if (!buffIconPrefab || !buffContainer) return;

        GameObject iconObj = Instantiate(buffIconPrefab, buffContainer);
        PassiveBuffUI buffUI = iconObj.GetComponent<PassiveBuffUI>();

        if (!buffUI)
        {
            buffUI = iconObj.AddComponent<PassiveBuffUI>();
        }

        // Initialize with timed passive settings (reads isDebuff from ability)
        buffUI.InitializeDebuff(ability, duration, endTime, ability.IsDebuff);

        _activeBuffs[ability] = buffUI;
    }

    private void CleanupExpiredBuffs()
    {
        _toRemove.Clear();

        foreach (var kvp in _activeBuffs)
        {
            if (kvp.Value == null || !kvp.Value.IsActive)
            {
                _toRemove.Add(kvp.Value);
            }
        }

        foreach (var buff in _toRemove)
        {
            if (buff != null)
            {
                var ability = buff.Ability;
                if (ability != null && _activeBuffs.ContainsKey(ability))
                {
                    _activeBuffs.Remove(ability);
                }
                Destroy(buff.gameObject);
            }
        }
    }
}



}




