using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections;
using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
[MovedFrom(true, null, null, "PlayerHealth")]
public class PlayerHealth : MonoBehaviour, EnemyAI.IDamageable
{
    public static PlayerHealth Instance { get; private set; }
    public static bool IsPlayerDead { get; private set; } = false;
    public static event System.Action OnPlayerDied;
    public static event System.Action OnPlayerRespawned;

    // Event for damage taken (used by ability system procs)
    public event System.Action<int> OnDamageTaken;

    // Event for before death (used by resurrection abilities)
    // Return true from subscribers to cancel death
#pragma warning disable 0067
    public event System.Func<bool> OnBeforeDeath;
#pragma warning restore 0067

    [Header("Health")]
    public int maxHealth = 100;
    [SerializeField] public int currentHealth = 100;
    public float invulnAfterHit = 0.2f;

    [Header("Animator")]
    public Animator animator;                 // assign if not on same GO
    public string takeDamageTrigger = "TakeDamage";
    public string dieTrigger        = "Die";
    public string isDeadBool        = "IsDead"; // optional bool gate in your controller
    

    [Header("Known Die State Path (optional)")]
    [Tooltip("Exact Animator path, e.g. 'Base Layer.TakeDamage/Die/Die_E'. Leave empty to rely on transitions.")]
    public string dieStatePath = "";          // fill if you want to force-enter a specific death state

    [Header("Hold Last Frame")]
    public bool  holdLastFrameOnDeath = true;
    [Range(0.8f, 1f)] public float holdNormalizedTime = 0.99f;
    public float deathHoldTimeout = 5f;

    [Header("Shutdown On Death")]
    public float collidersDisableDelay = 0.15f;
    public float rigidbodySleepDelay   = 0.15f;
    public bool  disableControllerOnDeath = true;
    public float destroyAfterSeconds   = -1f; // <0 = keep the body

    [Header("Respawn")]
    public string respawnTrigger = "Respawn";

    [Header("Regeneration")]
    [SerializeField] private bool enableRegeneration = false;
    [SerializeField] private bool regenOnlyOutOfCombat = true;
    [SerializeField] private bool regenUsePercentage = true;
    [SerializeField, Range(0f, 1f)] private float regenPercentPerTick = 0.02f;
    [SerializeField, Min(0)] private int regenFlatAmountPerTick = 2;
    [SerializeField, Min(0.05f)] private float regenTickInterval = 1f;
    [SerializeField, Min(0f)] private float regenCombatLockDuration = 4f;


    // refs/state
    GenericTopDownController _controller;
    Rigidbody2D _rb;
    float _canBeHitAt = 0f;
    bool  _dead = false;
    int   _diePathHash = 0;
    Vector3 _spawnPoint;
    Quaternion _spawnRotation;
    float _nextRegenTickAt = -1f;
    float _regenCombatLockUntil = -1f;


    void Awake()
    {
        Instance = this;
        IsPlayerDead = false;

        if (!animator) animator = GetComponentInChildren<Animator>(true);
        _controller = GetComponent<GenericTopDownController>();
        _rb = GetComponent<Rigidbody2D>();

        // record initial spawn
        _spawnPoint   = transform.position;
        _spawnRotation = transform.rotation;

        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 1, maxHealth);

        if (!string.IsNullOrEmpty(dieStatePath))
            _diePathHash = Animator.StringToHash(dieStatePath);

        _nextRegenTickAt = -1f;
        _regenCombatLockUntil = -1f;
    }

    void Update()
    {
        HandleRegeneration();
    }

    public void TakeDamage(int amount, Vector2 hitDir)
    {
        TakeDamage(amount, hitDir, null);
    }

    public void TakeDamage(int amount, Vector2 hitDir, Transform attacker)
    {
        if (_dead) return;
        if (Time.time < _canBeHitAt) return;

        int finalDamage = Mathf.Max(1, amount);
        if (PlayerStats.Instance != null)
        {
            finalDamage = PlayerStats.Instance.ApplyIncomingDamageReduction(amount);
        }

        var shieldHandler = AbilityShieldHandler.GetExisting(transform);
        if (shieldHandler != null)
        {
            finalDamage = shieldHandler.AbsorbDamage(Mathf.Max(1, finalDamage));
            if (finalDamage <= 0)
            {
                _canBeHitAt = Time.time + invulnAfterHit;
                return;
            }
        }

        if (finalDamage <= 0)
        {
            _canBeHitAt = Time.time + invulnAfterHit;
            return;
        }

        currentHealth -= Mathf.Max(1, finalDamage);
        _canBeHitAt = Time.time + invulnAfterHit;

        if (enableRegeneration && regenOnlyOutOfCombat)
        {
            float lockUntil = Time.time + regenCombatLockDuration;
            _regenCombatLockUntil = _regenCombatLockUntil > lockUntil ? _regenCombatLockUntil : lockUntil;
        }

        // Invoke damage taken event for ability system procs
        OnDamageTaken?.Invoke(finalDamage);

        // Play global damage feedback (screen flash + optional hitstop + shake)
        DamageFeedback.I?.Play();

        // Apply thorns damage if we have any and an attacker exists
        if (attacker != null && PlayerStats.Instance != null)
        {
            int totalThornDamage = 0;

            // Calculate percentage-based thorns
            float thornPercent = PlayerStats.Instance.CurrentThornDamagePercent;
            if (thornPercent > 0f)
            {
                totalThornDamage += Mathf.RoundToInt(finalDamage * thornPercent);
            }

            // Add flat thorns
            int thornFlat = PlayerStats.Instance.CurrentThornDamageFlat;
            totalThornDamage += thornFlat;

            // Apply combined thorns damage back to the attacker
            if (totalThornDamage > 0)
            {
                var damageable = attacker.GetComponentInParent<EnemyAI.IDamageable>();
                if (damageable != null)
                {
                    Vector2 reflectDir = (attacker.position - transform.position).normalized;
                    int finalThornDamage = Mathf.Max(1, totalThornDamage);
                    damageable.TakeDamage(finalThornDamage, reflectDir);

                    // Show thorns combat text at attacker's position
                    if (CombatTextManager.Instance != null)
                    {
                        CombatTextManager.Instance.SpawnThornsDamage(finalThornDamage, attacker.position);
                    }
                }
            }
        }

        // Lethal â†’ go straight to death
        if (currentHealth <= 0)
        {
            StartCoroutine(DieSequence());
            return;
        }

        // Non-lethal anim trigger
        if (animator && HasParam(takeDamageTrigger, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(takeDamageTrigger);
    }

    IEnumerator DieSequence()
    {
        if (_dead) yield break;
        _dead = true;
        IsPlayerDead = true;
        OnPlayerDied?.Invoke();

        if (!animator) animator = GetComponentInChildren<Animator>(true);

        // Clear damage trigger to avoid racing with death transition
        if (animator && HasParam(takeDamageTrigger, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(takeDamageTrigger);

        // Gate in Animator: IsDead = true
        if (animator && HasParam(isDeadBool, AnimatorControllerParameterType.Bool))
            animator.SetBool(isDeadBool, true);

        // Fire Die trigger too (if your graph expects it)
        if (animator && HasParam(dieTrigger, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(dieTrigger);
            animator.SetTrigger(dieTrigger);
        }

        // Optionally force the known die state (bypasses extra Animator conditions)
        if (animator && _diePathHash != 0 && animator.HasState(0, _diePathHash))
            animator.CrossFadeInFixedTime(_diePathHash, 0f, 0);

        if (holdLastFrameOnDeath)
            StartCoroutine(HoldDeathPoseRoutine());

        yield return null; // let animator consume

        // Stop input/motion
        if (disableControllerOnDeath && _controller) _controller.enabled = false;

        if (_rb)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            StartCoroutine(SleepRigidbodySoon());
        }

        StartCoroutine(DisableCollidersSoon());

        if (destroyAfterSeconds > 0f)
            Destroy(gameObject, destroyAfterSeconds);
    }

    IEnumerator DisableCollidersSoon()
    {
        yield return new WaitForSeconds(collidersDisableDelay);
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
    }

    IEnumerator SleepRigidbodySoon()
    {
        yield return new WaitForSeconds(rigidbodySleepDelay);
        if (_rb) _rb.simulated = false;
    }

    IEnumerator HoldDeathPoseRoutine()
    {
        if (!animator) yield break;

        float t0 = Time.time;

        // Wait until we're actually in (or transitioning to) a death state
        while (Time.time - t0 < deathHoldTimeout)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            var nx = animator.GetNextAnimatorStateInfo(0);

            if (IsDeath(st) || IsDeath(nx))
            {
                // Wait until fully in the death state
                while (!IsDeath(st))
                {
                    yield return null;
                    st = animator.GetCurrentAnimatorStateInfo(0);
                }

                // Wait until near end of first cycle
                while (st.normalizedTime < holdNormalizedTime && IsDeath(st))
                {
                    yield return null;
                    st = animator.GetCurrentAnimatorStateInfo(0);
                }

                // Pin and freeze
                animator.Play(st.fullPathHash, 0, 0.999f);
                animator.Update(0f);
                animator.speed = 0f;
                yield break;
            }
            yield return null;
        }
    }

    bool IsDeath(AnimatorStateInfo st)
    {
        if (st.fullPathHash == 0) return false;
        if (_diePathHash != 0) return st.fullPathHash == _diePathHash;
        // If you didnâ€™t set dieStatePath, rely on IsDead bool + no exit transitions in Animator
        return false;
    }

    bool HasParam(string name, AnimatorControllerParameterType type)
    {
        if (!animator) return false;
        foreach (var p in animator.parameters)
            if (p.name == name && p.type == type) return true;
        return false;
    }


    public void RespawnAtSpawn(int hp = -1)
    {
        if (!_dead) return; // already alive

        // clear global flags + announce
        _dead = false;
        IsPlayerDead = false;
        OnPlayerRespawned?.Invoke();

        // HP & pose
        currentHealth = Mathf.Clamp(hp < 0 ? maxHealth : hp, 1, maxHealth);
        _nextRegenTickAt = -1f;
        _regenCombatLockUntil = regenOnlyOutOfCombat ? Time.time + regenCombatLockDuration : -1f;

        // Animator back to alive
        if (animator)
        {
            animator.speed = 1f;
            if (HasParam(isDeadBool, AnimatorControllerParameterType.Bool))
                animator.SetBool(isDeadBool, false);

            // clear leftover triggers
            if (HasParam(dieTrigger, AnimatorControllerParameterType.Trigger))
                animator.ResetTrigger(dieTrigger);
            if (HasParam(takeDamageTrigger, AnimatorControllerParameterType.Trigger))
                animator.ResetTrigger(takeDamageTrigger);

            // NEW: play respawn animation if defined
            if (!string.IsNullOrEmpty(respawnTrigger) && HasParam(respawnTrigger, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(respawnTrigger);
        }


        // re-enable movement/input/physics/colliders
        if (_rb)
        {
            _rb.simulated = true;
    #if UNITY_2022_2_OR_NEWER
            _rb.linearVelocity = Vector2.zero;
    #else
            _rb.velocity = Vector2.zero;
    #endif
            _rb.angularVelocity = 0f;
        }

        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            c.enabled = true;

        if (_controller) _controller.enabled = true;

        // snap back to spawn transform
        transform.SetPositionAndRotation(_spawnPoint, _spawnRotation);
    }

    // Teleport to the recorded spawn/home location regardless of death state.
    // Useful for debug/options panel and fast-travel style behavior.
    public void TeleportToSpawn(bool fullHeal = true)
    {
        // If we are dead, run the full respawn flow first
        if (_dead)
        {
            RespawnAtSpawn(fullHeal ? maxHealth : -1);
            return;
        }

        // If alive, optionally heal and ensure controller/physics are active
        if (fullHeal)
        {
            currentHealth = Mathf.Clamp(maxHealth, 1, maxHealth);
        }
        _nextRegenTickAt = -1f;
        _regenCombatLockUntil = regenOnlyOutOfCombat ? Time.time + regenCombatLockDuration : -1f;

        if (animator)
        {
            animator.speed = 1f;
            if (HasParam(isDeadBool, AnimatorControllerParameterType.Bool))
                animator.SetBool(isDeadBool, false);
        }

        if (_rb)
        {
            _rb.simulated = true;
#if UNITY_2022_2_OR_NEWER
            _rb.linearVelocity = Vector2.zero;
#else
            _rb.velocity = Vector2.zero;
#endif
            _rb.angularVelocity = 0f;
        }

        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            c.enabled = true;

        if (_controller) _controller.enabled = true;

        transform.SetPositionAndRotation(_spawnPoint, _spawnRotation);
    }

    /// <summary>
    /// Updates the respawn/spawn point to the provided position/rotation.
    /// </summary>
    public void SetSpawnPoint(Vector3 position, Quaternion rotation)
    {
        _spawnPoint = position;
        _spawnRotation = rotation;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void HandleRegeneration()
    {
        if (!enableRegeneration)
        {
            _nextRegenTickAt = -1f;
            return;
        }

        if (_dead) return;
        if (currentHealth >= maxHealth) return;

        float now = Time.time;

        if (regenOnlyOutOfCombat && now < _regenCombatLockUntil)
        {
            return;
        }

        float interval = Mathf.Max(0.05f, regenTickInterval);
        if (_nextRegenTickAt < 0f)
        {
            _nextRegenTickAt = now + interval;
            return;
        }

        if (now < _nextRegenTickAt)
        {
            return;
        }

        int amount = regenUsePercentage
            ? Mathf.Max(1, Mathf.RoundToInt(maxHealth * Mathf.Clamp01(regenPercentPerTick)))
            : Mathf.Max(1, regenFlatAmountPerTick);

        if (amount <= 0)
        {
            _nextRegenTickAt = now + interval;
            return;
        }

        currentHealth = Mathf.Clamp(currentHealth + amount, 1, maxHealth);
        _nextRegenTickAt = now + interval;
    }
}



}




