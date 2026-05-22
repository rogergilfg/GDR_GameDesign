using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using SmallScale.FantasyKingdomTileset;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScaleInc.CharacterCreatorFantasy
{
    [RequireComponent(typeof(Animator))]
    public class GenericTopDownController : MonoBehaviour
    {
        #region Enums

        public enum MovementMode
        {
            RelativeToMouse,
            ClickToMove,
            Cardinal,
            WASDOnly
        }

        public enum AttackMode
        {
            Melee,
            Ranged
        }

        public enum IdleVariant
        {
            Idle1 = 1,
            Idle2 = 2,
            Idle3 = 3,
            Idle4 = 4
        }

        #endregion

        #region Inspectorâ€Exposed Fields

        [Header("Physics Movement (collide with World)")]
        public LayerMask worldMask;     // set to your "World" layer (tilemap collider)
        [Tooltip("Small cushion to stay just off the wall.")]
        public float skin = 0.01f;


        [Header("Movement Settings")]
        [Tooltip("Choose how WASD/mouse/click input should be interpreted.")]
        public MovementMode movementMode = MovementMode.RelativeToMouse;

        [Tooltip("Walking speed (units/sec).")]
        public float walkSpeed = 2f;

        [Tooltip("Running speed (units/sec) when holding the Run key or equivalent.")]
        public float runSpeed = 4f;

        [Tooltip("If true, clicking UI elements will NOT trigger clickâ€toâ€move targets.")]
        public bool ignoreUIClicks = true;

        [Header("Combat Gate")]
        [Tooltip("If true, prevents starting a new attack while another attack is active (controlled by other scripts).")]
        public bool respectExternalAttackLock = true;

        // Set by PlayerMeleeHitbox during an active swing (and can be toggled by other systems)
        [HideInInspector] public bool attackLockedExternally = false;

        // Set by movement logic - true when the character is moving
        [HideInInspector] public bool isMoving = false;

        [Tooltip("If true and you have a Rigidbody2D, movement will use physics (velocity) rather than directly setting transform.position.")]
        public bool usePhysicsMovement = false;

        [Tooltip("If usePhysicsMovement is true, the Rigidbody2D component to manipulate.")]
        public Rigidbody2D attachedRigidbody;

        [Header("Key Bindings")]
        [Tooltip("Key used for walking (if held, uses walkSpeed; otherwise runSpeed).")]
        public KeyCode walkKey = KeyCode.LeftControl;

        [Tooltip("Key used for crouching (hold to crouch).")]
        public KeyCode crouchKey = KeyCode.C;

        [Tooltip("Key used for toggling base vs. alternate speed multiplier.")]
        public KeyCode toggleSpeedKey = KeyCode.T;

        [Header("Mounting")]
        [Tooltip("Press this key to toggle mounted/unmounted state.")]
        public KeyCode mountKey = KeyCode.T;
        [Tooltip("Optional level requirement before the mount toggle becomes available (0 = unlocked immediately).")]
        [Min(0)] public int requiredMountLevel = 0;
        [Tooltip("Optional VFX prefab spawned at the player's position when mounting or dismounting.")]
        public GameObject mountEffectPrefab;

        [Header("Attack Settings")]
        [Tooltip("Key or mouse button for the default (regular) attack.")]
        public KeyCode defaultAttackKey = KeyCode.Mouse1;

        [Tooltip("Select whether the character is Melee or Ranged. Affects which Animator trigger names are used.")]
        public AttackMode attackMode = AttackMode.Melee;

        [Tooltip("Key or mouse button for the primary attack (1 = leftâ€click by default).")]
        public KeyCode primaryAttackKey = KeyCode.Mouse0;

        [Tooltip("Key or mouse button for the secondary attack (2 = rightâ€click by default).")]
        public KeyCode secondaryAttackKey = KeyCode.Mouse1;

        [Tooltip("Enable numberâ€key mappings for Attack2 through Attack9 (1â€9).")]
        public bool enableNumberKeyAttacks = true;

        [Header("Idle Variant")]
        [Tooltip("Which idle variant (1â€“4) to use when standing still.")]
        public IdleVariant idleVariant = IdleVariant.Idle1;

        [Header("Playback Speed Options")]
        [Tooltip("Base playback speed multiplier (1.0 = normal).")]
        [Range(0.1f, 3f)]
        public float basePlaybackSpeed = 1f;

        [Tooltip("Alternate playback speed multiplier (e.g. 2x speed).")]
        [Range(0.1f, 3f)]
        public float alternatePlaybackSpeed = 2f;

        [Tooltip("If true, pressing the Toggle Speed key will swap between base and alternate playback speeds.")]
        public bool allowPlaybackSpeedToggle = true;

        [Header("States (Readâ€Only in Inspector)")]
        [SerializeField] private bool isCrouching = false;
        [SerializeField] private bool isMounted = false;
        public bool IsMountUnlocked => requiredMountLevel <= 0 || mountUnlocked;
        public int RequiredMountLevel => Mathf.Max(0, requiredMountLevel);

        [Header("Debug & Events")]
        [Tooltip("If true, will draw a gizmo line from character to current click target (for debugging ClickToMove).")]
        public bool showClickTargetGizmo = false;

        /// <summary>
        /// Event that fires whenever we send an Animator trigger (e.g. any Attack or Special or Die, etc.).
        /// </summary>
        public event Action<string> OnAnimationTriggerSent;

        #endregion

        #region Private/Internal Fields

        private Animator animator;
        private Camera   mainCamera;

        // ClickToMove
        private Vector3 clickTarget;
        private bool    hasClickTarget = false;

        // Facing memory
        private Vector2 lastFacingDir = Vector2.right;

        // Playback speed toggle
        private bool isUsingAlternateSpeed = false;

        // Attack alternation helpers
        private bool useAttack1Next = true;
        private bool useAttackRun1Next = true;

        private Vector2 _moveDirThisFrame;   // from Update
        private float   _speedThisFrame;     // from Update
        private Rigidbody2D _rb;
        private Collider2D  _col;

        private bool _baseSpeedCaptured = false;
        private float _baseRunSpeed;
        private readonly RaycastHit2D[] _castHits = new RaycastHit2D[8];
        private bool _crouchOverrideActive;
        private bool _crouchOverrideValue;
        private bool mountUnlocked;
        private bool mountUnlockNotified;
        private bool levelEventsHooked;
        private PlayerHealth subscribedPlayerHealth;
        private int mountAnimationRefreshFrames;


        #endregion

        #region Unity

        private void Awake()
        {
            animator = GetComponent<Animator>();
            mainCamera = Camera.main;

            if (usePhysicsMovement && attachedRigidbody == null)
            {
                attachedRigidbody = GetComponent<Rigidbody2D>();
                if (attachedRigidbody == null)
                    Debug.LogWarning("usePhysicsMovement is true but no Rigidbody2D was assigned or found. Movement will fallback to transform.position.");
            }

            _rb  = attachedRigidbody ? attachedRigidbody : GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();

            if (usePhysicsMovement && _rb)
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.gravityScale = 0f;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                _rb.freezeRotation = true;
            }

            mountUnlockNotified = requiredMountLevel <= 0;
            RefreshMountAvailability();
            TrySubscribeLevelEvents();
        }

        private void OnEnable()
        {
            SubscribePlayerHealth();
            TrySubscribeLevelEvents();
            RefreshMountAvailability();
        }

        private void OnDisable()
        {
            UnsubscribePlayerHealth();
            UnsubscribeLevelEvents();
        }

        void FixedUpdate()
        {
            if (PlayerHealth.IsPlayerDead) { if (usePhysicsMovement && _rb) _rb.linearVelocity = Vector2.zero; return; }

            if (!usePhysicsMovement || _rb == null || _col == null)
            {
                // If you really want non-physics movement, do it here (but it wonâ€™t collide).
                transform.position += (Vector3)_moveDirThisFrame * _speedThisFrame * Time.fixedDeltaTime;
                return;
            }

            Vector2 dir = _moveDirThisFrame;
            float   dist = dir.magnitude * _speedThisFrame * Time.fixedDeltaTime;

            if (dist <= 0f) { _rb.linearVelocity = Vector2.zero; return; }
            dir = (dir.sqrMagnitude > 0f) ? dir.normalized : Vector2.zero;

            // Cast the player's collider along intended move to find the first hit
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = worldMask, useTriggers = false };
            int hitCount = _col.Cast(dir, filter, _castHits, dist + skin);

            if (hitCount > 0)
            {
                // Move up to (but not into) the obstacle
                float hitDist = _castHits[0].distance - skin;
                if (hitDist < 0f) hitDist = 0f;
                dist = Mathf.Min(dist, hitDist);
            }

            _rb.MovePosition(_rb.position + dir * dist);
            _rb.linearVelocity = Vector2.zero; // donâ€™t accumulate velocity between steps
        }


        private void Update()
        {
            if (!levelEventsHooked)
            {
                TrySubscribeLevelEvents();
            }
            if (subscribedPlayerHealth == null || subscribedPlayerHealth != PlayerHealth.Instance)
            {
                UnsubscribePlayerHealth();
                SubscribePlayerHealth();
            }

            if (PlayerHealth.IsPlayerDead)
            {
                ForceDismount();
                if (usePhysicsMovement && attachedRigidbody != null)
                    attachedRigidbody.linearVelocity = Vector2.zero;
                // Clear movement booleans; keep facing as-is
                animator.SetBool("IsRun", false);
                animator.SetBool("IsWalk", false);
                animator.SetBool("IsRunBackwards", false);
                animator.SetBool("IsStrafeLeft", false);
                animator.SetBool("IsStrafeRight", false);
                return;
            }
            // 1) Toggle mount & speed
            HandleMountToggleInput();
            HandleSpeedToggleInput();

            // 2) Mouse direction
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = transform.position.z;
            Vector2 mouseDir = (mouseWorld - transform.position).normalized;

            // 3) WASD
            bool w = Input.GetKey(KeyCode.W)    || Input.GetKey(KeyCode.UpArrow);
            bool s = Input.GetKey(KeyCode.S)    || Input.GetKey(KeyCode.DownArrow);
            bool a = Input.GetKey(KeyCode.A)    || Input.GetKey(KeyCode.LeftArrow);
            bool d = Input.GetKey(KeyCode.D)    || Input.GetKey(KeyCode.RightArrow);
            bool walkHold  = Input.GetKey(walkKey);

            // 4) Click-to-move targeting
            if (movementMode == MovementMode.ClickToMove)
            {
                if (Input.GetMouseButtonDown(0) &&
                    (!ignoreUIClicks || EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                {
                    clickTarget    = mouseWorld;
                    hasClickTarget = true;
                }
            }
            else if (w || s || a || d)
            {
                hasClickTarget = false;
            }

            // 5) Build raw movement vector
            Vector2 rawMove   = Vector2.zero;
            Vector2 forwardDir = Vector2.right;

            switch (movementMode)
            {
                case MovementMode.RelativeToMouse:
                    forwardDir = mouseDir;
                    Vector2 rightRel = new Vector2(forwardDir.y, -forwardDir.x);
                    if (w) rawMove += forwardDir;
                    if (s) rawMove -= forwardDir;
                    if (a) rawMove -= rightRel;
                    if (d) rawMove += rightRel;
                    break;

                case MovementMode.ClickToMove:
                    if (hasClickTarget)
                    {
                        Vector2 toT = ((Vector2)clickTarget - (Vector2)transform.position);
                        if (toT.magnitude < 0.1f) hasClickTarget = false;
                        else                       rawMove = toT.normalized;
                    }
                    break;

                case MovementMode.Cardinal:
                case MovementMode.WASDOnly:
                    if (w) rawMove += Vector2.up;
                    if (s) rawMove += Vector2.down;
                    if (a) rawMove += Vector2.left;
                    if (d) rawMove += Vector2.right;
                    break;
            }

            Vector2 moveDir = rawMove.normalized;

            // 6) Determine facing direction
            if (movementMode == MovementMode.ClickToMove)
            {
                if (hasClickTarget)
                {
                    forwardDir = ((Vector2)clickTarget - (Vector2)transform.position).normalized;
                    lastFacingDir = forwardDir;
                }
                else forwardDir = lastFacingDir;
            }
            else if (movementMode == MovementMode.WASDOnly)
            {
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    forwardDir = moveDir;
                    lastFacingDir = forwardDir;
                }
                else forwardDir = lastFacingDir;
            }
            else
            {
                forwardDir = mouseDir;
                lastFacingDir = forwardDir;
            }

            // 7) Right vector for strafing/backwards
            Vector2 rightDir = new Vector2(forwardDir.y, -forwardDir.x);

            // 8) Move (defer actual motion to FixedUpdate)
            if (!_baseSpeedCaptured)
            {
                _baseRunSpeed = runSpeed;
                _baseSpeedCaptured = true;
            }
            float mountMultiplier = isMounted ? 1.714f : 1.0f; // ~1.2 / 0.7
            // We let AbilityRunner set the final runSpeed. Here we just apply the mount multiplier.
            float speed = walkHold ? walkSpeed : (runSpeed * mountMultiplier);
            _moveDirThisFrame = moveDir;
            _speedThisFrame   = speed;

            // 9) Crouch (hold) â€” cannot crouch while mounted
            bool newCrouch = Input.GetKey(crouchKey) && !isMounted;
            if (_crouchOverrideActive)
                newCrouch = _crouchOverrideValue && !isMounted;
            if (newCrouch && !isCrouching) ResetAllIdleVariants();
            isCrouching = newCrouch;

            // 10) Animator core states
            int dirIdx = GetDirectionIndex(forwardDir);
            animator.SetFloat("Direction", dirIdx);
            animator.SetInteger("DirIndex", dirIdx);
            animator.SetBool("IsCrouching", isCrouching);
            animator.SetBool("IsMounted",  isMounted);

            // 11) Idle variants
            bool useIdle2 = false, useIdle3 = false, useIdle4 = false;
            if (!isCrouching && !isMounted)
            {
                switch (idleVariant)
                {
                    case IdleVariant.Idle2: useIdle2 = true; break;
                    case IdleVariant.Idle3: useIdle3 = true; break;
                    case IdleVariant.Idle4: useIdle4 = true; break;
                }
            }
            animator.SetBool("UseIdle2", useIdle2);
            animator.SetBool("UseIdle3", useIdle3);
            animator.SetBool("UseIdle4", useIdle4);

            // 12) Movement flags
            isMoving = moveDir.sqrMagnitude > 0.01f;
            float dotF = Vector2.Dot(moveDir, forwardDir);
            float dotB = Vector2.Dot(moveDir, -forwardDir);
            float dotR = Vector2.Dot(moveDir, rightDir);
            float dotL = Vector2.Dot(moveDir, -rightDir);
            const float TH = 0.5f;

            bool runState = isMoving && dotF > TH && !walkHold;
            bool walkState = isMoving && dotF > TH && walkHold;
            bool runBackState = isMoving && dotB > TH;
            bool strafeLeftState = isMoving && dotL > TH;
            bool strafeRightState = isMoving && dotR > TH;

            if (mountAnimationRefreshFrames > 0)
            {
                runState = walkState = runBackState = strafeLeftState = strafeRightState = false;
                mountAnimationRefreshFrames--;
            }

            animator.SetBool("IsRun", runState);
            animator.SetBool("IsWalk", walkState);
            animator.SetBool("IsRunBackwards", runBackState);
            animator.SetBool("IsStrafeLeft", strafeLeftState);
            animator.SetBool("IsStrafeRight", strafeRightState);

            // 13) ATTACK INPUT (no dodge gating anymore)
            bool canStartAttack = (!respectExternalAttackLock || !attackLockedExternally);

            if (canStartAttack && Input.GetKey(primaryAttackKey))
            {
                if (isMounted)
                {
                    ForceDismount();
                }
                string trig;

                if (isMoving)
                {
                    // Alternate running attacks
                    trig = useAttackRun1Next ? "AttackRun" : "Attack2";
                    useAttackRun1Next = !useAttackRun1Next;
                }
                else
                {
                    // Alternate standing attacks
                    trig = useAttack1Next ? "Attack1" : "Attack2";
                    useAttack1Next = !useAttack1Next;
                }

                animator.SetTrigger(trig);
                OnAnimationTriggerSent?.Invoke(trig);
            }

            // number keys 1â€“9
            // if (canStartAttack && Input.GetKeyDown(KeyCode.Alpha1)) { animator.SetTrigger("Attack2");    OnAnimationTriggerSent?.Invoke("Attack2"); }
            // if (canStartAttack && Input.GetKeyDown(KeyCode.Alpha2)) { animator.SetTrigger("Attack1");    OnAnimationTriggerSent?.Invoke("Attack1"); }
            // if (canStartAttack && Input.GetKeyDown(KeyCode.Alpha3)) { animator.SetTrigger("Attack4");    OnAnimationTriggerSent?.Invoke("Attack4"); }
            // if (canStartAttack && Input.GetKeyDown(KeyCode.Alpha4)) { animator.SetTrigger("Attack5");    OnAnimationTriggerSent?.Invoke("Attack5"); }
            // if (canStartAttack && Input.GetKeyDown(KeyCode.Alpha5)) { animator.SetTrigger("Special1");   OnAnimationTriggerSent?.Invoke("Special1"); }
            // if (canStartAttack && Input.GetKeyDown(KeyCode.Alpha6)) { animator.SetTrigger("Special2");   OnAnimationTriggerSent?.Invoke("Special2"); }
            // if (canStartAttack && Input.GetKeyDown(KeyCode.Alpha7)) { animator.SetTrigger("Taunt");      OnAnimationTriggerSent?.Invoke("Taunt"); }

            // debug triggers unrestricted
            // if (Input.GetKeyDown(KeyCode.Alpha8)) { animator.SetTrigger("Die");        OnAnimationTriggerSent?.Invoke("Die"); }
            // if (Input.GetKeyDown(KeyCode.Alpha9)) { animator.SetTrigger("TakeDamage"); OnAnimationTriggerSent?.Invoke("TakeDamage"); }

            // 14) Playback speed
            float targetSpeed = isUsingAlternateSpeed ? alternatePlaybackSpeed : basePlaybackSpeed;
            animator.SetBool("Speed1x", Mathf.Approximately(targetSpeed, basePlaybackSpeed));
            animator.SetBool("Speed2x", Mathf.Approximately(targetSpeed, alternatePlaybackSpeed));
            animator.speed = targetSpeed;
        }

        private void OnDrawGizmos()
        {
            if (showClickTargetGizmo && movementMode == MovementMode.ClickToMove && hasClickTarget)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, clickTarget);
                Gizmos.DrawWireSphere(clickTarget, 0.1f);
            }
        }

        #endregion

        #region Helpers

        void ApplyCrouchImmediate(bool crouch)
        {
            if (crouch && !isCrouching) ResetAllIdleVariants();
            isCrouching = crouch;
            if (animator)
            {
                animator.SetBool("IsCrouching", isCrouching);
            }
        }

        public void SetCrouchOverride(bool active, bool crouchValue = true)
        {
            _crouchOverrideActive = active;
            _crouchOverrideValue = crouchValue;
            if (active)
            {
                ApplyCrouchImmediate(crouchValue && !isMounted);
            }
            else
            {
                ApplyCrouchImmediate(false);
            }
        }

        private void HandleMountToggleInput()
        {
            if (Input.GetKeyDown(mountKey))
            {
                TryToggleMount();
            }
        }

        public bool TryToggleMount()
        {
            if (!CanToggleMount())
            {
                return false;
            }

            isMounted = !isMounted;
            if (isMounted)
            {
                isCrouching = false;
                ResetAllIdleVariants();
            }
            animator.SetBool("IsMounted", isMounted);
            SpawnMountEffect();
            ScheduleMountAnimationRefresh();
            return true;
        }

        private bool CanToggleMount()
        {
            return IsMountUnlocked;
        }

        private void ForceDismount()
        {
            if (!isMounted)
            {
                return;
            }

            isMounted = false;
            animator.SetBool("IsMounted", false);
            SpawnMountEffect();
            ScheduleMountAnimationRefresh();
        }

        private void SpawnMountEffect()
        {
            if (mountEffectPrefab == null)
            {
                return;
            }

            Vector3 position = transform.position;
            Instantiate(mountEffectPrefab, position, Quaternion.identity);
        }

        private void TrySubscribeLevelEvents()
        {
            if (levelEventsHooked)
            {
                if (PlayerExperience.Instance == null)
                {
                    levelEventsHooked = false;
                }
                return;
            }

            PlayerExperience experience = PlayerExperience.Instance;
            if (experience == null)
            {
                return;
            }

            experience.LevelChanged += HandlePlayerLevelChanged;
            levelEventsHooked = true;
            RefreshMountAvailability();
        }

        private void UnsubscribeLevelEvents()
        {
            if (!levelEventsHooked)
            {
                return;
            }

            PlayerExperience experience = PlayerExperience.Instance;
            if (experience != null)
            {
                experience.LevelChanged -= HandlePlayerLevelChanged;
            }

            levelEventsHooked = false;
        }

        private void HandlePlayerLevelChanged(int newLevel)
        {
            RefreshMountAvailability(true);
        }

        private void RefreshMountAvailability(bool notify = false)
        {
            bool previous = mountUnlocked;
            PlayerExperience experience = PlayerExperience.Instance;
            mountUnlocked = requiredMountLevel <= 0 || (experience != null && experience.CurrentLevel >= requiredMountLevel);

            if (mountUnlocked && !previous)
            {
                if (!mountUnlockNotified && notify && CombatTextManager.Instance != null)
                {
                    CombatTextManager.Instance.SpawnStatus("Mount unlocked! Press T to ride.", transform.position + new Vector3(0f, 1.4f, 0f));
                    mountUnlockNotified = true;
                }
                else if (requiredMountLevel <= 0)
                {
                    mountUnlockNotified = true;
                }
            }
        }

        private void SubscribePlayerHealth()
        {
            if (subscribedPlayerHealth != null)
            {
                return;
            }

            PlayerHealth instance = PlayerHealth.Instance;
            if (instance == null)
            {
                return;
            }

            instance.OnDamageTaken += HandlePlayerDamageTaken;
            subscribedPlayerHealth = instance;
        }

        private void UnsubscribePlayerHealth()
        {
            if (subscribedPlayerHealth == null)
            {
                return;
            }

            subscribedPlayerHealth.OnDamageTaken -= HandlePlayerDamageTaken;
            subscribedPlayerHealth = null;
        }

        private void HandlePlayerDamageTaken(int damage)
        {
            if (damage > 0)
            {
                ForceDismount();
            }
        }

        private void ScheduleMountAnimationRefresh()
        {
            mountAnimationRefreshFrames = 2;
        }

        private void HandleSpeedToggleInput()
        {
            if (!allowPlaybackSpeedToggle) return;
            if (Input.GetKeyDown(toggleSpeedKey))
                isUsingAlternateSpeed = !isUsingAlternateSpeed;
        }

        private void ResetAllIdleVariants()
        {
            animator.SetBool("UseIdle2", false);
            animator.SetBool("UseIdle3", false);
            animator.SetBool("UseIdle4", false);
            idleVariant = IdleVariant.Idle1;
        }

        private int GetDirectionIndex(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            if (angle >= 337.5f || angle < 22.5f)   return 0; // East
            if (angle < 67.5f)                      return 4; // NE
            if (angle < 112.5f)                     return 3; // North
            if (angle < 157.5f)                     return 5; // NW
            if (angle < 202.5f)                     return 1; // West
            if (angle < 247.5f)                     return 7; // SW
            if (angle < 292.5f)                     return 2; // South
            return 6; // SE
        }

        #endregion
    }
}







