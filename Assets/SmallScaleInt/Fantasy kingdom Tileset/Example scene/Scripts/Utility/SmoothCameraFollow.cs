using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using SmallScale.FantasyKingdomTileset.Building;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScaleInc.TopDownPixelCharactersPack1
{
    [RequireComponent(typeof(Camera))]
    public class SmoothCameraFollow : MonoBehaviour
    {
        [Header("Target & Offset")]
        [Tooltip("The transform the camera should follow.")]
        public Transform target;
        [Tooltip("World-space offset from the target.")]
        public Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("Smooth Movement")]
        [Tooltip("Time it takes for the camera to reach the target position.")]
        [Min(0f)] public float smoothTime = 0.25f;
        [Tooltip("Use separate smoothing on X and Y (nice for isometric).")]
        public bool separateAxisSmoothing = false;
        [Min(0f)] public float smoothTimeX = 0.25f;
        [Min(0f)] public float smoothTimeY = 0.25f;

        [Header("Look-Ahead")]
        public bool enableLookAhead = true;
        [Tooltip("How far ahead to look based on movement direction.")]
        public float lookAheadDistance = 0.6f;
        [Tooltip("How quickly look-ahead reacts to direction changes.")]
        public float lookAheadSpeed = 4f;
        [Tooltip("Minimum speed before look-ahead engages.")]
        public float lookAheadSpeedThreshold = 0.05f;

        [Header("Dead Zone (Soft Follow Bubble)")]
        [Tooltip("If > 0, camera wonÃ¢â‚¬â„¢t move while the target stays within this radius from the camera center (in world units).")]
        public float deadZoneRadius = 0f;

        [Header("Zoom")]
        public bool enableZoom = true;

        [Tooltip("Current zoom (Camera.orthographicSize).")]
        public float zoom = 5f;

        [Tooltip("Zoom limits.")]
        public Vector2 zoomLimits = new Vector2(3f, 12f);

        [Tooltip("How fast zoom reacts.")]
        public float zoomSmoothTime = 0.15f;

        [Tooltip("Allow mouse wheel to control zoom (legacy Input).")]
        public bool mouseWheelZoom = true;

        [Tooltip("Use percent-based zoom steps for symmetric in/out.")]
        public bool percentBasedZoom = true;

        [Tooltip("Percent step per scroll notch (0.05 = 5%).")]
        [Range(0.01f, 0.5f)] public float zoomPercentPerNotch = 0.12f;

        [Tooltip("If not using percent-based, additive step per notch.")]
        public float zoomUnitsPerNotch = 0.5f;

        [Header("Speed-Based Zoom")]
        [Tooltip("Automatically zoom based on target speed.")]
        public bool speedBasedZoom = false;
        [Tooltip("Map target speed (x) to desired zoom/orthographicSize (y).")]
        public AnimationCurve speedToZoom = AnimationCurve.Linear(0, 5f, 10f, 8f);
        [Tooltip("Blend factor when combining manual and speed-based zoom (0 = manual only, 1 = speed only).")]
        [Range(0f, 1f)] public float speedZoomBlend = 1f;

        [Header("Bounds")]
        [Tooltip("Clamp camera within these world-space bounds.")]
        public bool clampToBounds = false;
        [Tooltip("World-space rectangle (center/size) to keep camera inside.")]
        public Rect worldBounds = new Rect(new Vector2(-50, -50), new Vector2(100, 100));
        [Tooltip("Extra padding when clamping.")]
        public float boundsPadding = 0.5f;

        [Header("Pixel-Perfect (Optional)")]
        [Tooltip("Snap camera to pixel grid for crisp pixel art.")]
        public bool snapToPixelGrid = false;
        [Tooltip("Pixels per unit used by your sprites.")]
        public float pixelsPerUnit = 16f;

        [Header("Photo Mode (Free Camera)")]
        [SerializeField, Tooltip("Runtime flag that indicates whether the camera is currently in photo mode. Use SetPhotoMode at runtime rather than toggling this manually.")]
        private bool photoModeEnabled = false;
        [SerializeField, Min(0.1f), Tooltip("Movement speed (units/second) used while flying the camera in photo mode.")]
        private float photoModeMoveSpeed = 12f;
        [SerializeField, Min(0f), Tooltip("Acceleration towards the requested speed while in photo mode.")]
        private float photoModeAcceleration = 20f;
        [SerializeField, Min(0f), Tooltip("How quickly we slow down when no input is pressed in photo mode.")]
        private float photoModeDeceleration = 25f;

        [Header("Screen Shake")]
        [Tooltip("Global multiplier for shake intensity.")]
        public float shakeIntensity = 1f;
        [Tooltip("How fast shake decays over time.")]
        public float shakeDecay = 1.5f;
        [Tooltip("Frequency used by Perlin noise for shake.")]
        public float shakeFrequency = 25f;
        [Tooltip("Max rotation (Z) during shake in degrees.")]
        public float shakeMaxAngle = 3f;

        [Header("Editor Testing")]
        [Tooltip("Amplitude used when pressing 'Test Screen Shake' in the inspector.")]
        [Range(0f, 1f)] public float testShakeAmplitude = 0.7f;
        [Tooltip("Duration used when pressing 'Test Screen Shake' in the inspector.")]
        [Min(0f)] public float testShakeDuration = 0.35f;

        // Internals
        Camera _cam;
        Vector3 _velocity;     // for SmoothDamp (combined)
        float _velX;           // for SmoothDamp (per-axis)
        float _velY;
        Vector3 _currentLookAhead;
        Vector3 _lastTargetPos;
        #pragma warning disable 0414
        bool _hadInitial;
        #pragma warning restore 0414

        Vector3 _photoModePosition;
        Vector3 _photoModeVelocity;
        bool _photoModeSessionActive;

        float _zoomVel;        // for zoom SmoothDamp
        float _zoomTarget;     // target orthographicSize we ease towards

        float _shakeTimeLeft;
        float _shakeAmp;       // current amplitude (0..1)
        float _noiseSeedX;
        float _noiseSeedY;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true; // intended for orthographic 2D
            _noiseSeedX = Random.value * 1000f;
            _noiseSeedY = Random.value * 1000f;
        }

        void Start()
        {
            if (target != null)
            {
                _lastTargetPos = target.position;
                _hadInitial = true;
            }

            // Initialize zoom and target
            if (zoom <= 0f) zoom = _cam.orthographicSize > 0f ? _cam.orthographicSize : 5f;
            zoom = Mathf.Clamp(zoom, zoomLimits.x, zoomLimits.y);
            _zoomTarget = zoom;
            _cam.orthographicSize = zoom;
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            bool buildMenuBlockingInput = BuildMenuController.IsAnyMenuOpen;

            if (photoModeEnabled)
            {
                UpdatePhotoMode(dt, buildMenuBlockingInput);
                return;
            }

            if (target == null) return;

            // --- Compute target velocity & look-ahead ---
            Vector3 currentTargetPos = target.position;
            Vector3 delta = currentTargetPos - _lastTargetPos;
            Vector2 v2 = new Vector2(delta.x, delta.y) / dt;
            float speed = v2.magnitude;

            Vector3 desiredLookAhead = Vector3.zero;
            if (enableLookAhead && speed > lookAheadSpeedThreshold)
            {
                Vector3 dir = new Vector3(v2.x, v2.y, 0f).normalized;
                desiredLookAhead = dir * lookAheadDistance;
            }
            _currentLookAhead = Vector3.Lerp(_currentLookAhead, desiredLookAhead, 1f - Mathf.Exp(-lookAheadSpeed * dt));

            _lastTargetPos = currentTargetPos;

            // --- Desired focus point before dead-zone/bounds ---
            Vector3 focus = currentTargetPos + offset + _currentLookAhead;
            focus.z = transform.position.z; // keep camera z

            // --- Dead-zone behavior (keep target inside a circle) ---
            Vector3 camPos = transform.position;
            if (deadZoneRadius > 0f)
            {
                Vector2 toFocus = new Vector2(focus.x - camPos.x, focus.y - camPos.y);
                float dist = toFocus.magnitude;
                if (dist > deadZoneRadius)
                {
                    // Move only enough to place focus on the dead-zone edge
                    Vector2 move = toFocus.normalized * (dist - deadZoneRadius);
                    focus = new Vector3(camPos.x + move.x, camPos.y + move.y, camPos.z);
                }
                else
                {
                    // Keep current camera position if inside the bubble
                    focus = camPos;
                }
            }

            // --- Smooth movement ---
            Vector3 newPos = camPos;
            if (separateAxisSmoothing)
            {
                float newX = Mathf.SmoothDamp(camPos.x, focus.x, ref _velX, smoothTimeX);
                float newY = Mathf.SmoothDamp(camPos.y, focus.y, ref _velY, smoothTimeY);
                newPos = new Vector3(newX, newY, camPos.z);
            }
            else
            {
                newPos = Vector3.SmoothDamp(camPos, focus, ref _velocity, smoothTime);
            }

            HandleZoomInput(speed, buildMenuBlockingInput);

            // --- Bounds clamping (after zoom because camera size matters) ---
            newPos = ClampWithinBounds(newPos);

            ApplyCameraTransform(newPos, dt, allowShake: true);
        }

        void UpdatePhotoMode(float dt, bool buildMenuBlockingInput)
        {
            if (!_photoModeSessionActive)
            {
                _photoModeSessionActive = true;
                _photoModePosition = transform.position;
                _photoModeVelocity = Vector3.zero;
            }

            Vector2 moveInput = Vector2.zero;
            if (!buildMenuBlockingInput)
            {
                moveInput.x = Input.GetAxisRaw("Horizontal");
                moveInput.y = Input.GetAxisRaw("Vertical");
            }

            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            Vector3 desiredVelocity = new Vector3(moveInput.x, moveInput.y, 0f) * photoModeMoveSpeed;
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                float accel = Mathf.Max(0.01f, photoModeAcceleration);
                _photoModeVelocity = Vector3.MoveTowards(_photoModeVelocity, desiredVelocity, accel * dt);
            }
            else
            {
                float decel = Mathf.Max(0.01f, photoModeDeceleration);
                _photoModeVelocity = Vector3.MoveTowards(_photoModeVelocity, Vector3.zero, decel * dt);
            }

            _photoModePosition += _photoModeVelocity * dt;
            _photoModePosition.z = transform.position.z;

            HandleZoomInput(_photoModeVelocity.magnitude, buildMenuBlockingInput);

            Vector3 clamped = ClampWithinBounds(_photoModePosition);
            ApplyCameraTransform(clamped, dt, allowShake: false);
            _photoModePosition = transform.position;
        }

        void HandleZoomInput(float referenceSpeed, bool buildMenuBlockingInput)
        {
            if (!enableZoom)
            {
                return;
            }

            if (mouseWheelZoom && !buildMenuBlockingInput)
            {
                float scroll = 0f;
                if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.0001f)
                    scroll = Input.mouseScrollDelta.y;
                else
                    scroll = Input.GetAxis("Mouse ScrollWheel") * 10f;

                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    if (percentBasedZoom)
                    {
                        float step = Mathf.Clamp01(zoomPercentPerNotch);
                        float factorPerNotch = 1f - step;
                        float factor = Mathf.Pow(factorPerNotch, scroll);
                        _zoomTarget *= factor;
                    }
                    else
                    {
                        _zoomTarget -= scroll * zoomUnitsPerNotch;
                    }
                }
            }

            if (speedBasedZoom)
            {
                float speedZoom = speedToZoom.Evaluate(referenceSpeed);
                _zoomTarget = Mathf.Lerp(_zoomTarget, speedZoom, speedZoomBlend);
            }

            _zoomTarget = Mathf.Clamp(_zoomTarget, zoomLimits.x, zoomLimits.y);
            zoom = Mathf.SmoothDamp(_cam.orthographicSize, _zoomTarget, ref _zoomVel, zoomSmoothTime);
            _cam.orthographicSize = zoom;
        }

        Vector3 ClampWithinBounds(Vector3 candidate)
        {
            if (!clampToBounds)
            {
                return candidate;
            }

            Vector2 half = GetCameraHalfExtents();
            float minX = worldBounds.xMin + half.x + boundsPadding;
            float maxX = worldBounds.xMax - half.x - boundsPadding;
            float minY = worldBounds.yMin + half.y + boundsPadding;
            float maxY = worldBounds.yMax - half.y - boundsPadding;

            if (minX > maxX)
            {
                candidate.x = (worldBounds.xMin + worldBounds.xMax) * 0.5f;
            }
            else
            {
                candidate.x = Mathf.Clamp(candidate.x, minX, maxX);
            }

            if (minY > maxY)
            {
                candidate.y = (worldBounds.yMin + worldBounds.yMax) * 0.5f;
            }
            else
            {
                candidate.y = Mathf.Clamp(candidate.y, minY, maxY);
            }

            return candidate;
        }

        void ApplyCameraTransform(Vector3 desiredPosition, float dt, bool allowShake)
        {
            Vector2 shakeOffset = Vector2.zero;
            float shakeAngle = 0f;

            if (_shakeTimeLeft > 0f && _shakeAmp > 0f)
            {
                _shakeTimeLeft = Mathf.Max(0f, _shakeTimeLeft - dt * shakeDecay);
                if (allowShake && _shakeTimeLeft > 0f)
                {
                    float amp = _shakeAmp * shakeIntensity;
                    float t = Time.time * shakeFrequency;
                    float nx = (Mathf.PerlinNoise(_noiseSeedX, t) - 0.5f) * 2f;
                    float ny = (Mathf.PerlinNoise(_noiseSeedY, t + 100f) - 0.5f) * 2f;

                    shakeOffset = new Vector2(nx, ny) * amp * 0.2f;
                    shakeAngle = nx * amp * shakeMaxAngle;
                }
                else if (!allowShake)
                {
                    shakeOffset = Vector2.zero;
                    shakeAngle = 0f;
                }
            }

            if (snapToPixelGrid)
            {
                desiredPosition = SnapToPixel(desiredPosition);
            }

            desiredPosition.x += shakeOffset.x;
            desiredPosition.y += shakeOffset.y;

            transform.position = desiredPosition;
            transform.rotation = allowShake ? Quaternion.Euler(0f, 0f, shakeAngle) : Quaternion.identity;
        }

        Vector2 GetCameraHalfExtents()
        {
            float halfY = _cam.orthographicSize;
            float halfX = halfY * _cam.aspect;
            return new Vector2(halfX, halfY);
        }

        Vector3 SnapToPixel(Vector3 pos)
        {
            // World units per pixel based on current ortho size & screen height
            float worldUnitsPerPixel = (_cam.orthographicSize * 2f) / Mathf.Max(Screen.height, 1);
            pos.x = Mathf.Round(pos.x / worldUnitsPerPixel) * worldUnitsPerPixel;
            pos.y = Mathf.Round(pos.y / worldUnitsPerPixel) * worldUnitsPerPixel;
            return pos;
        }

        /// <summary>
        /// Triggers a screen shake. amplitude ~ [0..1], duration in seconds.
        /// </summary>
        public void Shake(float amplitude = 0.6f, float duration = 0.35f)
        {
            _shakeAmp = Mathf.Clamp01(Mathf.Max(_shakeAmp, amplitude));
            _shakeTimeLeft = Mathf.Max(_shakeTimeLeft, duration);
        }

        /// <summary>
        /// Instantly set the current zoom (orthographic size), clamped to limits.
        /// </summary>
        public void SetZoom(float orthographicSize)
        {
            _zoomTarget = Mathf.Clamp(orthographicSize, zoomLimits.x, zoomLimits.y);
            _cam.orthographicSize = _zoomTarget;
            zoom = _zoomTarget;
        }

        /// <summary>
        /// Immediately snaps the camera to the current target position instead of easing.
        /// Useful for large instant movements such as teleportation so the camera keeps up.
        /// </summary>
        public void SnapToTargetImmediately()
        {
            if (target == null)
            {
                return;
            }

            _lastTargetPos = target.position;
            _currentLookAhead = Vector3.zero;
            _velocity = Vector3.zero;
            _velX = 0f;
            _velY = 0f;

            Vector3 focus = target.position + offset;
            focus.z = transform.position.z;
            Vector3 newPos = focus;

            newPos = ClampWithinBounds(newPos);

            if (snapToPixelGrid)
            {
                newPos = SnapToPixel(newPos);
            }

            transform.position = newPos;
            transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// Smoothly zoom to size over time (helper coroutine).
        /// </summary>
        public System.Collections.IEnumerator ZoomTo(float targetSize, float time)
        {
            _zoomTarget = Mathf.Clamp(targetSize, zoomLimits.x, zoomLimits.y);
            float start = _cam.orthographicSize;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(time, 0.0001f);
                float s = Mathf.SmoothStep(start, _zoomTarget, t);
                _cam.orthographicSize = s;
                zoom = s;
                yield return null;
            }
        }

        /// <summary>
        /// Enables or disables free-fly photo mode. When disabling, the camera instantly snaps back to the follow target.
        /// </summary>
        public void SetPhotoMode(bool enabled)
        {
            if (photoModeEnabled == enabled)
            {
                return;
            }

            photoModeEnabled = enabled;

            if (enabled)
            {
                _photoModeSessionActive = true;
                _photoModePosition = transform.position;
                _photoModeVelocity = Vector3.zero;
                _shakeTimeLeft = 0f;
                _shakeAmp = 0f;
            }
            else
            {
                _photoModeSessionActive = false;
                _photoModeVelocity = Vector3.zero;
                SnapToTargetImmediately();
            }
        }

        /// <summary>
        /// Adjusts the movement speed used while photo mode is active.
        /// </summary>
        public void SetPhotoModeSpeed(float speed)
        {
            photoModeMoveSpeed = Mathf.Max(0.1f, speed);
        }

        /// <summary>
        /// Returns whether the camera is currently in photo mode.
        /// </summary>
        public bool IsPhotoModeActive => photoModeEnabled;

        /// <summary>
        /// Current configured photo mode move speed.
        /// </summary>
        public float PhotoModeSpeed => photoModeMoveSpeed;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam != null && _cam.orthographic == false) _cam.orthographic = true;
            if (zoomLimits.x > zoomLimits.y) zoomLimits = new Vector2(zoomLimits.y, zoomLimits.x);

            if (!Application.isPlaying && _cam != null)
            {
                if (zoom <= 0f) zoom = _cam.orthographicSize > 0f ? _cam.orthographicSize : 5f;
                zoom = Mathf.Clamp(zoom, zoomLimits.x, zoomLimits.y);
                _cam.orthographicSize = zoom;
                _zoomTarget = zoom;
            }
        }

        // Quick context menu for shake
        [ContextMenu("Test Screen Shake (Context)")]
        void ContextTestShake()
        {
            Shake(testShakeAmplitude, testShakeDuration);
        }
#endif
    }

#if UNITY_EDITOR
    // Editor button for testing shake in the same file/namespace
    [CustomEditor(typeof(SmoothCameraFollow))]
    public class SmoothCameraFollowEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var cam = (SmoothCameraFollow)target;

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Screen Shake"))
            {
                cam.Shake(cam.testShakeAmplitude, cam.testShakeDuration);
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
#endif
}







