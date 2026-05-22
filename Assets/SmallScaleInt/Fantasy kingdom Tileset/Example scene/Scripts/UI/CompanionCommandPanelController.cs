using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SmallScaleInc.CharacterCreatorFantasy;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset.UI
{
    /// <summary>
    /// Displays quick action buttons for controlling active companions.
    /// Attack/Move wait for a click on the world to pick a destination, while Hold/Follow apply immediately.
    /// </summary>
    public sealed class CompanionCommandPanelController : MonoBehaviour
    {
        enum PendingPointerCommand
        {
            None,
            AttackMove,
            MoveToPoint
        }

        [Header("Panel")]
        [SerializeField]
        private GameObject panelRoot;

        [Header("Buttons")]
        [SerializeField]
        private Button attackButton;

        [SerializeField]
        private Button moveButton;

        [SerializeField]
        private Button holdButton;

        [SerializeField]
        private Button followButton;

        [Header("Input")]
        [SerializeField]
        private Camera sceneCamera;

        readonly List<CompanionAI> _aliveCompanionsBuffer = new();
        PendingPointerCommand _pendingPointerCommand = PendingPointerCommand.None;
        Button _activePointerButton;

        void OnEnable()
        {
            if (attackButton) attackButton.onClick.AddListener(HandleAttackButton);
            if (moveButton) moveButton.onClick.AddListener(HandleMoveButton);
            if (holdButton) holdButton.onClick.AddListener(HandleHoldButton);
            if (followButton) followButton.onClick.AddListener(HandleFollowButton);

            CompanionAI.ActiveCompanionsChanged += HandleCompanionListChanged;
            UpdatePanelVisibility();
        }

        void OnDisable()
        {
            if (attackButton) attackButton.onClick.RemoveListener(HandleAttackButton);
            if (moveButton) moveButton.onClick.RemoveListener(HandleMoveButton);
            if (holdButton) holdButton.onClick.RemoveListener(HandleHoldButton);
            if (followButton) followButton.onClick.RemoveListener(HandleFollowButton);

            CompanionAI.ActiveCompanionsChanged -= HandleCompanionListChanged;
            CancelPendingPointerCommand();
        }

        void Update()
        {
            if (_pendingPointerCommand == PendingPointerCommand.None)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                if (TryGetPointerWorldPosition(out Vector2 worldPos))
                {
                    bool attackMove = _pendingPointerCommand == PendingPointerCommand.AttackMove;
                    IssueMoveOrder(worldPos, attackMove);
                    CancelPendingPointerCommand();
                }
            }
            else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPendingPointerCommand();
            }
        }

        void HandleAttackButton()
        {
            BeginPendingPointerCommand(PendingPointerCommand.AttackMove, attackButton);
        }

        void HandleMoveButton()
        {
            BeginPendingPointerCommand(PendingPointerCommand.MoveToPoint, moveButton);
        }

        void HandleHoldButton()
        {
            GatherAliveCompanions(_aliveCompanionsBuffer);
            for (int i = 0; i < _aliveCompanionsBuffer.Count; i++)
            {
                var companion = _aliveCompanionsBuffer[i];
                companion.IssueHoldCommand(companion.transform.position);
            }

            CancelPendingPointerCommand();
        }

        void HandleFollowButton()
        {
            GatherAliveCompanions(_aliveCompanionsBuffer);
            for (int i = 0; i < _aliveCompanionsBuffer.Count; i++)
            {
                _aliveCompanionsBuffer[i].IssueFollowCommand();
            }

            CancelPendingPointerCommand();
        }

        public void TriggerAttackCommand()
        {
            HandleAttackButton();
        }

        public void TriggerFollowCommand()
        {
            HandleFollowButton();
        }

        public void TriggerHoldCommand()
        {
            HandleHoldButton();
        }

        void BeginPendingPointerCommand(PendingPointerCommand command, Button sourceButton)
        {
            if (!HasLivingCompanions())
            {
                UpdatePanelVisibility();
                return;
            }

            _pendingPointerCommand = command;
            if (_activePointerButton != null)
            {
                _activePointerButton.interactable = true;
            }

            _activePointerButton = sourceButton;
            if (_activePointerButton != null)
            {
                _activePointerButton.interactable = false;
            }
        }

        void CancelPendingPointerCommand()
        {
            _pendingPointerCommand = PendingPointerCommand.None;
            if (_activePointerButton != null)
            {
                _activePointerButton.interactable = true;
                _activePointerButton = null;
            }
        }

        void IssueMoveOrder(Vector2 worldPosition, bool attackMove)
        {
            GatherAliveCompanions(_aliveCompanionsBuffer);
            for (int i = 0; i < _aliveCompanionsBuffer.Count; i++)
            {
                _aliveCompanionsBuffer[i].IssueMoveCommand(worldPosition, attackMove);
            }
        }

        void GatherAliveCompanions(List<CompanionAI> buffer)
        {
            buffer.Clear();
            var list = CompanionAI.ActiveCompanions;
            for (int i = 0; i < list.Count; i++)
            {
                var companion = list[i];
                if (companion != null && companion.isActiveAndEnabled && !companion.IsDead)
                {
                    buffer.Add(companion);
                }
            }
        }

        void HandleCompanionListChanged()
        {
            UpdatePanelVisibility();
            if (!HasLivingCompanions())
            {
                CancelPendingPointerCommand();
            }
        }

        void UpdatePanelVisibility()
        {
            if (!panelRoot) return;
            panelRoot.SetActive(HasLivingCompanions());
        }

        bool HasLivingCompanions()
        {
            return CompanionAI.HasLivingCompanion();
        }

        bool TryGetPointerWorldPosition(out Vector2 world)
        {
            Camera cam = sceneCamera != null ? sceneCamera : Camera.main;
            if (cam == null)
            {
                world = Vector2.zero;
                return false;
            }

            Vector3 mouse = Input.mousePosition;
            float depth = Mathf.Abs(cam.transform.position.z);
            if (cam.orthographic)
            {
                Vector3 w = cam.ScreenToWorldPoint(mouse);
                world = new Vector2(w.x, w.y);
                return true;
            }
            else
            {
                mouse.z = depth <= 0.01f ? 10f : depth;
                Vector3 w = cam.ScreenToWorldPoint(mouse);
                world = new Vector2(w.x, w.y);
                return true;
            }
        }
    }
}





