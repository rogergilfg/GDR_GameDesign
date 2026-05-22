using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;
using UnityEngine;

[MovedFrom(true, null, null, "CombatTextManager")]
public class CombatTextManager : MonoBehaviour
{
    public static CombatTextManager Instance { get; private set; }

    [Header("Global Enable")]
    [Tooltip("Master switch to enable/disable all floating combat text visuals at runtime.")]
    public bool enableFloatingCombatText = true;

    public static bool Enabled
    {
        get => Instance ? Instance.enableFloatingCombatText : false;
        set { if (Instance) Instance.enableFloatingCombatText = value; }
    }

    [Header("Scene Refs")]
    public Canvas worldSpaceCanvas;         // World Space canvas
    public CombatTextPopup popupPrefab;     // Prefab with TMP + script

    [Header("Normal Style")]
    public Color normalColor = Color.white;
    public float normalScale = 1.0f;
    public float normalDuration = 0.9f;
    public Vector2 normalInitialVelocity = new Vector2(0.2f, 2.5f);
    public float normalGravity = 3.5f;

    [Header("Crit Style")]
    public Color critColor = new Color(1f, 0.9f, 0.1f, 1f);
    public float critScale = 1.25f;
    public float critDuration = 1.05f;
    public Vector2 critInitialVelocity = new Vector2(0.25f, 3.6f);
    public float critGravity = 4.0f;

    [Header("Thorns Style")]
    public bool showThornsDamage = true;
    public Color thornsColor = new Color(0.9f, 0.4f, 0.9f, 1f);
    public float thornsScale = 1.0f;
    public float thornsDuration = 0.85f;
    public Vector2 thornsInitialVelocity = new Vector2(-0.3f, 2.2f);
    public float thornsGravity = 3.2f;

    [Header("Lifesteal Style")]
    public Color lifestealColor = new Color(0.9f, 0.2f, 0.3f, 1f);
    public float lifestealScale = 1.05f;
    public float lifestealDuration = 0.9f;
    public Vector2 lifestealInitialVelocity = new Vector2(0.15f, 2.4f);
    public float lifestealGravity = 3.0f;

    [Header("Randomization")]
    public Vector2 randomKick = new Vector2(0.5f, 0.4f);
    public float spawnJitterY = 0.15f;
    public float perHitSpiral = 0.12f;

    [Header("Heal Style")]
    public Color healColor = new Color(0.35f, 1f, 0.5f, 1f);
    public float healScale = 1.05f;
    public float healDuration = 1.0f;
    public Vector2 healInitialVelocity = new Vector2(0.1f, 2.2f);
    public float healGravity = 2.8f;

    [Header("Mana Style")]
    public Color manaColor = new Color(0.4f, 0.7f, 1f, 1f);

    [Header("Status Style")]
    public Color statusColor = new Color(0.6f, 1f, 0.8f, 1f);
    public float statusScale = 1.05f;
    public float statusDuration = 1.0f;
    public Vector2 statusInitialVelocity = new Vector2(0.1f, 2.2f);
    public float statusGravity = 2.8f;
    public AnimationCurve heightCurveStatus; // optional; if null weâ€™ll use heightCurveNormal

    [Header("Resource Style")]
    public Color resourceColor = new Color(0.85f, 1f, 0.7f, 1f);
    public float resourceScale = 1.0f;
    public float resourceDuration = 0.95f;
    public Vector2 resourceInitialVelocity = new Vector2(0.08f, 2.0f);
    public float resourceGravity = 2.6f;
    public AnimationCurve heightCurveResource;

    [Header("Experience Style")]
    public Color experienceColor = new Color(0.65f, 0.85f, 1f, 1f);
    public float experienceScale = 1.05f;
    public float experienceDuration = 0.95f;
    public Vector2 experienceInitialVelocity = new Vector2(0.12f, 2.35f);
    public float experienceGravity = 2.7f;
    public AnimationCurve heightCurveExperience;

    [Header("Level Up Style")]
    public Color levelUpColor = new Color(1f, 0.92f, 0.4f, 1f);
    public float levelUpScale = 1.3f;
    public float levelUpDuration = 1.1f;
    public Vector2 levelUpInitialVelocity = new Vector2(0.18f, 2.6f);
    public float levelUpGravity = 2.5f;
    public AnimationCurve heightCurveLevelUp;

    // Optional: slightly softer scale/height for heals
    public AnimationCurve heightCurveHeal = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.12f),
        new Keyframe(0.7f, 0.04f),
        new Keyframe(1f, 0f)
    );

    [Header("Spawn Offsets")]
    public Vector3 damageSpawnOffset = Vector3.zero;
    public Vector3 critSpawnOffset = Vector3.zero;
    public Vector3 thornsSpawnOffset = Vector3.zero;
    public Vector3 lifestealSpawnOffset = Vector3.zero;
    public Vector3 healSpawnOffset = Vector3.zero;
    public Vector3 manaSpawnOffset = Vector3.zero;
    public Vector3 statusSpawnOffset = Vector3.zero;
    public Vector3 lootSpawnOffset = Vector3.zero;
    public Vector3 resourceSpawnOffset = Vector3.zero;
    public Vector3 experienceSpawnOffset = Vector3.zero;
    public Vector3 levelUpSpawnOffset = Vector3.zero;

    [Header("Screen Space Offsets")]
    public Vector2 damageScreenOffset = Vector2.zero;
    public Vector2 critScreenOffset = Vector2.zero;
    public Vector2 thornsScreenOffset = Vector2.zero;
    public Vector2 lifestealScreenOffset = Vector2.zero;
    public Vector2 healScreenOffset = Vector2.zero;
    public Vector2 manaScreenOffset = Vector2.zero;
    public Vector2 statusScreenOffset = Vector2.zero;
    public Vector2 lootScreenOffset = Vector2.zero;
    public Vector2 resourceScreenOffset = Vector2.zero;
    public Vector2 experienceScreenOffset = Vector2.zero;
    public Vector2 levelUpScreenOffset = Vector2.zero;

    [Header("Smart Placement")]
    [Tooltip("When enabled, new combat text attempts to avoid overlapping existing popups.")]
    public bool enableSmartSpacing = false;

    [Tooltip("Minimum world-space distance to maintain between active popups when smart spacing is enabled.")]
    public float smartSpacingRadius = 0.35f;

    [Tooltip("Maximum number of adjustment passes when searching for a free spot.")]
    public int smartSpacingMaxIterations = 6;

    [Tooltip("World-space offset applied on each adjustment pass when resolving overlaps.")]
    public Vector3 smartSpacingStep = new Vector3(0f, 0.35f, 0f);

    [Tooltip("Optional horizontal jitter applied while stacking texts during smart spacing.")]
    public float smartSpacingHorizontalJitter = 0.12f;

    [Header("Curves")]
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0f, 0.8f),
        new Keyframe(0.06f, 1.35f),
        new Keyframe(0.18f, 1.0f),
        new Keyframe(0.8f, 1.0f),
        new Keyframe(1f, 0.95f)
    );
    public AnimationCurve heightCurveNormal = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.15f),
        new Keyframe(0.6f, 0.05f),
        new Keyframe(1f, 0f)
    );

    [Header("Status Overrides")]
    [Tooltip("When enabled, status text stays stationary (no float) and simply fades in/out.")]
    public bool statusFadeOnly = false;
    public AnimationCurve heightCurveCrit = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 0.35f),
        new Keyframe(0.7f, 0.1f),
        new Keyframe(1f, 0f)
    );

    readonly Queue<CombatTextPopup> _pool = new Queue<CombatTextPopup>();
    readonly List<CombatTextPopup> _activePopups = new List<CombatTextPopup>();
    Camera _cam;
    int _spawnCount;
    static readonly AnimationCurve s_flatHeightCurve = AnimationCurve.Constant(0f, 1f, 0f);
    static readonly AnimationCurve s_constantScaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _cam = Camera.main;

        // optional warm-up to avoid first-burst hitch
        Prewarm(16);
    }

    void Prewarm(int count)
    {
        if (!popupPrefab) return;
        for (int i = 0; i < count; i++)
        {
            var p = Instantiate(popupPrefab, worldSpaceCanvas ? worldSpaceCanvas.transform : transform);
            p.Init(_cam);
            p.gameObject.SetActive(false);
            _pool.Enqueue(p);
        }
    }

    CombatTextPopup Get()
    {
        if (_pool.Count > 0) return _pool.Dequeue();
        var p = Instantiate(popupPrefab, worldSpaceCanvas ? worldSpaceCanvas.transform : transform);
        p.Init(_cam);
        return p;
    }

    public void ReturnToPool(CombatTextPopup popup)
    {
        if (!popup) return;
        _activePopups.Remove(popup);
        popup.gameObject.SetActive(false);
        _pool.Enqueue(popup);
    }

    void RegisterActive(CombatTextPopup popup)
    {
        if (!popup) return;
        if (!_activePopups.Contains(popup))
        {
            _activePopups.Add(popup);
        }
    }

    Vector3 ResolveSmartSpacing(Vector3 desiredWorldPos)
    {
        if (!enableSmartSpacing || _activePopups.Count == 0)
        {
            return desiredWorldPos;
        }

        float radius = Mathf.Max(0.01f, smartSpacingRadius);
        int maxIterations = Mathf.Max(1, smartSpacingMaxIterations);
        Vector3 step = smartSpacingStep.sqrMagnitude > Mathf.Epsilon
            ? smartSpacingStep
            : new Vector3(0f, radius, 0f);

        Vector3 candidate = desiredWorldPos;
        for (int i = 0; i < maxIterations; i++)
        {
            bool overlaps = false;
            for (int j = 0; j < _activePopups.Count; j++)
            {
                CombatTextPopup popup = _activePopups[j];
                if (!popup || !popup.isActiveAndEnabled || !popup.IsActive)
                {
                    continue;
                }

                Vector3 existing = popup.CurrentWorldPosition;
                float distance = Vector2.Distance(
                    new Vector2(candidate.x, candidate.y),
                    new Vector2(existing.x, existing.y));

                if (distance < radius)
                {
                    overlaps = true;
                    candidate += step;
                    if (smartSpacingHorizontalJitter > 0f)
                    {
                        candidate.x += Random.Range(-smartSpacingHorizontalJitter, smartSpacingHorizontalJitter);
                    }
                    break;
                }
            }

            if (!overlaps)
            {
                return candidate;
            }
        }

        return candidate;
    }

    public void SpawnDamage(int amount, Vector3 worldPos, bool isCrit)
    {
        if (!enableFloatingCombatText) return;
        var p = Get();
        worldPos += isCrit ? critSpawnOffset : damageSpawnOffset;
        var screenOffset = isCrit ? critScreenOffset : damageScreenOffset;

        // offset so numbers don't pile up
        Vector2 rnd = new Vector2(
            Random.Range(-randomKick.x, randomKick.x),
            Random.Range(0f, randomKick.y)
        );
        float spiralOff = perHitSpiral * (_spawnCount++ % 6);
        worldPos += new Vector3(spiralOff, spawnJitterY, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        if (isCrit)
        {
            p.Play(
                worldPos,
                amount.ToString(),
                critColor,
                critScale,
                critDuration,
                critInitialVelocity + rnd,
                critGravity,
                alphaCurve,
                scaleCurve,
                heightCurveCrit,
                screenOffset
            );
        }
        else
        {
            p.Play(
                worldPos,
                amount.ToString(),
                normalColor,
                normalScale,
                normalDuration,
                normalInitialVelocity + rnd,
                normalGravity,
                alphaCurve,
                scaleCurve,
                heightCurveNormal,
                screenOffset
            );
        }

        RegisterActive(p);
    }

    public void SpawnThornsDamage(int amount, Vector3 worldPos)
    {
        if (!enableFloatingCombatText) return;
        if (!showThornsDamage || amount <= 0) return;

        var p = Get();
        worldPos += thornsSpawnOffset;

        // offset slightly to the left/different direction from normal damage
        Vector2 rnd = new Vector2(
            Random.Range(-randomKick.x * 0.7f, randomKick.x * 0.3f),
            Random.Range(0f, randomKick.y * 0.8f)
        );
        float spiralOff = perHitSpiral * (_spawnCount++ % 6);
        worldPos += new Vector3(-spiralOff * 0.8f, spawnJitterY + 0.1f, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        p.Play(
            worldPos,
            amount.ToString(),
            thornsColor,
            thornsScale,
            thornsDuration,
            thornsInitialVelocity + rnd,
            thornsGravity,
            alphaCurve,
            scaleCurve,
            heightCurveNormal,
            thornsScreenOffset
        );

        RegisterActive(p);
    }

    public void SpawnLifesteal(int amount, Vector3 worldPos)
    {
        if (!enableFloatingCombatText) return;
        if (amount <= 0) return;

        var p = Get();
        worldPos += lifestealSpawnOffset;

        // offset slightly to the right/different direction from normal damage
        Vector2 rnd = new Vector2(
            Random.Range(-randomKick.x * 0.4f, randomKick.x * 0.6f),
            Random.Range(0f, randomKick.y * 0.9f)
        );
        float spiralOff = perHitSpiral * (_spawnCount++ % 6);
        worldPos += new Vector3(spiralOff * 0.9f, spawnJitterY + 0.12f, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        // Display lifesteal with special styling (prefix with +)
        p.Play(
            worldPos,
            "+" + amount.ToString(),
            lifestealColor,
            lifestealScale,
            lifestealDuration,
            lifestealInitialVelocity + rnd,
            lifestealGravity,
            alphaCurve,
            scaleCurve,
            heightCurveHeal != null ? heightCurveHeal : heightCurveNormal,
            lifestealScreenOffset
        );

        RegisterActive(p);
    }

    public void SpawnHeal(int amount, Vector3 worldPos)
    {
        if (!enableFloatingCombatText) return;
        if (amount <= 0) return;
        var p = Get();
        worldPos += healSpawnOffset;

        // nudge so it doesnâ€™t overlap with damage numbers
        Vector2 rnd = new Vector2(
            Random.Range(-randomKick.x * 0.5f, randomKick.x * 0.5f),
            Random.Range(0f, randomKick.y)
        );
        float spiralOff = perHitSpiral * (_spawnCount++ % 6);
        worldPos += new Vector3(spiralOff * 0.6f, spawnJitterY + 0.05f, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        p.Play(
            worldPos,
            "+" + amount.ToString(),      // prefix plus sign for heals
            healColor,
            healScale,
            healDuration,
            healInitialVelocity + rnd,
            healGravity,
            alphaCurve,
            scaleCurve,
            heightCurveHeal != null ? heightCurveHeal : heightCurveNormal,
            healScreenOffset
        );

        RegisterActive(p);
    }

    public void SpawnMana(float amount, Vector3 worldPos)
    {
        if (!enableFloatingCombatText) return;
        if (amount <= 0f) return;

        var p = Get();
        worldPos += manaSpawnOffset;

        Vector2 rnd = new Vector2(
            Random.Range(-randomKick.x * 0.5f, randomKick.x * 0.5f),
            Random.Range(0f, randomKick.y)
        );

        float spiralOff = perHitSpiral * (_spawnCount++ % 6);
        worldPos += new Vector3(spiralOff * 0.6f, spawnJitterY + 0.05f, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        string label = "+" + amount.ToString("0.#");

        p.Play(
            worldPos,
            label,
            manaColor,
            healScale,
            healDuration,
            healInitialVelocity + rnd,
            healGravity,
            alphaCurve,
            scaleCurve,
            heightCurveHeal != null ? heightCurveHeal : heightCurveNormal,
            manaScreenOffset
        );

        RegisterActive(p);
    }

    /// <summary>
    /// Spawns a generic status text (e.g., "Revived", "Stunned") at a world position.
    /// </summary>
    public void SpawnStatus(string text, Vector3 worldPos)
    {
        if (!enableFloatingCombatText) return;
        if (string.IsNullOrEmpty(text)) return;

        var p = Get();
        worldPos += statusSpawnOffset;

        Vector2 rnd;
        float spiralOff = 0f;
        if (statusFadeOnly)
        {
            rnd = Vector2.zero;
            _spawnCount++;
        }
        else
        {
            // light randomization so it doesn't overlap other text
            rnd = new Vector2(
                Random.Range(-randomKick.x * 0.5f, randomKick.x * 0.5f),
                Random.Range(0f,            randomKick.y)
            );
            spiralOff = perHitSpiral * (_spawnCount++ % 6);
        }

        float verticalKick = statusFadeOnly ? 0f : (spawnJitterY + 0.05f);
        worldPos += new Vector3(spiralOff * 0.6f, verticalKick, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        Vector2 initialVelocity = statusFadeOnly ? Vector2.zero : statusInitialVelocity + rnd;
        float gravity = statusFadeOnly ? 0f : statusGravity;
        AnimationCurve heightCurve = statusFadeOnly
            ? s_flatHeightCurve
            : (heightCurveStatus != null ? heightCurveStatus : heightCurveNormal);
        AnimationCurve scaleCurveToUse = statusFadeOnly ? s_constantScaleCurve : scaleCurve;

        p.Play(
            worldPos,
            text,
            statusColor,
            statusScale,
            statusDuration,
            initialVelocity,
            gravity,
            alphaCurve,
            scaleCurveToUse,
            heightCurve,
            statusScreenOffset
        );

        RegisterActive(p);
    }

    /// <summary>
    /// Spawns loot pickup text using the provided rarity color.
    /// </summary>
    /// <param name="itemName">Display name of the item that was collected.</param>
    /// <param name="rarityColor">Color associated with the item's rarity.</param>
    /// <param name="worldPos">World position to display the floating text.</param>
    public void SpawnLootPickup(string itemName, Color rarityColor, Vector3 worldPos)
    {
        if (!enableFloatingCombatText) return;
        if (string.IsNullOrEmpty(itemName)) return;

        var p = Get();
        worldPos += lootSpawnOffset;

        Vector2 rnd = new Vector2(
            Random.Range(-randomKick.x * 0.35f, randomKick.x * 0.35f),
            Random.Range(0f, randomKick.y * 0.5f)
        );

        float spiralOff = perHitSpiral * (_spawnCount++ % 6);
        worldPos += new Vector3(spiralOff * 0.4f, spawnJitterY * 0.5f, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        p.Play(
            worldPos,
            itemName,
            rarityColor,
            statusScale,
            statusDuration,
            statusInitialVelocity + rnd,
            statusGravity,
            alphaCurve,
            scaleCurve,
            heightCurveStatus != null ? heightCurveStatus : heightCurveNormal,
            lootScreenOffset
        );

        RegisterActive(p);
    }

    // Legacy overload removed; use string label overload below

    /// <summary>
    /// Spawns a resource gain/loss popup using the resource styling with a custom label.
    /// Useful for dynamic resources that are not based on the legacy enum.
    /// </summary>
    public void SpawnResourceGain(string label, Vector3 worldPos)
    {
        if (!enableFloatingCombatText) return;
        if (string.IsNullOrEmpty(label)) return;

        var p = Get();
        worldPos += resourceSpawnOffset;
        Vector2 rnd = new Vector2(
            Random.Range(-randomKick.x * 0.5f, randomKick.x * 0.5f),
            Random.Range(0f, randomKick.y)
        );

        float spiralOff = perHitSpiral * (_spawnCount++ % 6);
        worldPos += new Vector3(spiralOff * 0.4f, spawnJitterY * 0.6f, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        p.Play(
            worldPos,
            label,
            resourceColor,
            resourceScale,
            resourceDuration,
            resourceInitialVelocity + rnd,
            resourceGravity,
            alphaCurve,
            scaleCurve,
            heightCurveResource != null ? heightCurveResource : heightCurveNormal,
            resourceScreenOffset
        );

        RegisterActive(p);
    }

    /// <summary>
    /// Spawns an experience gain popup (e.g., "+20 XP") at the provided world position.
    /// </summary>
    public void SpawnExperienceGain(string label, Vector3 worldPos)
    {
        if (!enableFloatingCombatText) return;
        if (string.IsNullOrEmpty(label)) return;

        var p = Get();
        worldPos += experienceSpawnOffset;
        Vector2 rnd = new Vector2(
            Random.Range(-randomKick.x * 0.35f, randomKick.x * 0.35f),
            Random.Range(0f, randomKick.y * 0.75f)
        );

        float spiralOff = perHitSpiral * (_spawnCount++ % 6);
        worldPos += new Vector3(spiralOff * 0.35f, spawnJitterY * 0.75f, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        p.Play(
            worldPos,
            label,
            experienceColor,
            experienceScale,
            experienceDuration,
            experienceInitialVelocity + rnd,
            experienceGravity,
            alphaCurve,
            scaleCurve,
            heightCurveExperience != null ? heightCurveExperience : heightCurveNormal,
            experienceScreenOffset
        );

        RegisterActive(p);
    }

    /// <summary>
    /// Spawns a celebratory level-up popup at the provided world position.
    /// </summary>
    public void SpawnLevelUp(string label, Vector3 worldPos)
    {
        if (!enableFloatingCombatText) return;
        if (string.IsNullOrEmpty(label)) return;

        var p = Get();
        worldPos += levelUpSpawnOffset;
        Vector2 rnd = new Vector2(
            Random.Range(-randomKick.x * 0.25f, randomKick.x * 0.25f),
            Random.Range(0f, randomKick.y * 0.5f)
        );

        float spiralOff = perHitSpiral * (_spawnCount++ % 6);
        worldPos += new Vector3(spiralOff * 0.3f, spawnJitterY + 0.35f, 0f);
        worldPos = ResolveSmartSpacing(worldPos);

        var heightCurve = heightCurveLevelUp != null
            ? heightCurveLevelUp
            : (heightCurveStatus != null ? heightCurveStatus : heightCurveNormal);

        p.Play(
            worldPos,
            label,
            levelUpColor,
            levelUpScale,
            levelUpDuration,
            levelUpInitialVelocity + rnd,
            levelUpGravity,
            alphaCurve,
            scaleCurve,
            heightCurve,
            levelUpScreenOffset
        );

        RegisterActive(p);
    }

}



}




