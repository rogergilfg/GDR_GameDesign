using UnityEngine;
using SmallScale.FantasyKingdomTileset;

namespace DungeonGeneration
{
    /// <summary>
    /// Bridges an existing Interactable portal with a generated dungeon, wiring destinations as the generator runs.
    /// Attach to an overworld portal so players can enter the freshly built dungeon without swapping scenes.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class DungeonPortalLink : MonoBehaviour
    {
        public enum PortalRole
        {
            EntranceToDungeon,
            ReturnToOverworld
        }

        [Header("Connections")]
        [SerializeField] private DungeonGenerator generator;
        [SerializeField] private PortalRole role = PortalRole.EntranceToDungeon;
        [SerializeField] private Interactable interactable;

        [Tooltip("Optional explicit transform used as the target when this portal sends players back to the overworld. Defaults to this transform.")]
        [SerializeField] private Transform returnAnchorOverride;

        [Tooltip("Additional offset applied when configuring the Interactable portal destination.")]
        [SerializeField] private Vector3 destinationOffset = Vector3.zero;

        [Header("Generation")]
        [Tooltip("Automatically ensure the dungeon exists when the scene starts.")]
        [SerializeField] private bool generateOnStart = true;

        [Tooltip("When enabled this portal becomes the generator's overworld return anchor (used by dungeon exit portals).")]
        [SerializeField] private bool registerAsReturnAnchor = true;

        [Tooltip("When enabled this portal becomes the generator's previous level anchor (used by the dungeon start portal).")]
        [SerializeField] private bool registerAsPreviousLevelAnchor = true;

        private void Reset()
        {
            interactable = GetComponent<Interactable>();
        }

        private void OnEnable()
        {
            EnsureInteractableReference();

            if (generator != null)
            {
                generator.DungeonGenerated += HandleDungeonGenerated;
            }

            RegisterAnchors();
            SyncDestination();
            SubscribePortalEvents();
        }

        private void Start()
        {
            if (generateOnStart && generator != null && role == PortalRole.EntranceToDungeon)
            {
                generator.EnsureDungeonReady();
            }

            SyncDestination();
        }

        private void OnDisable()
        {
            UnsubscribePortalEvents();

            if (generator != null)
            {
                generator.DungeonGenerated -= HandleDungeonGenerated;
            }
        }

        private void HandleDungeonGenerated(DungeonRuntimeData data)
        {
            SyncDestination(data);
        }

        private void SyncDestination(DungeonRuntimeData data = null)
        {
            if (generator == null)
            {
                return;
            }

            EnsureInteractableReference();

            if (interactable == null)
            {
                return;
            }

            data ??= generator.RuntimeData;
            if (data == null)
            {
                return;
            }

            switch (role)
            {
                case PortalRole.EntranceToDungeon:
                    ConfigureEntrancePortal(data);
                    break;

                case PortalRole.ReturnToOverworld:
                    ConfigureReturnPortal();
                    break;
            }
        }

        private void ConfigureEntrancePortal(DungeonRuntimeData data)
        {
            Transform target = data.StartPortal != null ? data.StartPortal : null;
            interactable.ConfigurePortalDestination(target, destinationOffset);
        }

        private void ConfigureReturnPortal()
        {
            Transform target = generator.OverworldReturnAnchor != null
                ? generator.OverworldReturnAnchor
                : EffectiveReturnAnchor;

            interactable.ConfigurePortalDestination(target, destinationOffset);
        }

        private void RegisterAnchors()
        {
            if (generator == null)
            {
                return;
            }

            if (registerAsReturnAnchor && generator.OverworldReturnAnchor == null)
            {
                generator.OverworldReturnAnchor = EffectiveReturnAnchor;
            }

            if (registerAsPreviousLevelAnchor && generator.PreviousLevelReturnAnchor == null)
            {
                generator.PreviousLevelReturnAnchor = EffectiveReturnAnchor;
            }
        }

        private Transform EffectiveReturnAnchor => returnAnchorOverride != null ? returnAnchorOverride : transform;

        private void SubscribePortalEvents()
        {
            if (role != PortalRole.EntranceToDungeon)
            {
                return;
            }

            EnsureInteractableReference();
            if (interactable == null)
            {
                return;
            }

            interactable.PortalTeleported -= HandleEntrancePortalTeleported;
            interactable.PortalTeleported += HandleEntrancePortalTeleported;
        }

        private void UnsubscribePortalEvents()
        {
            if (interactable == null)
            {
                return;
            }

            interactable.PortalTeleported -= HandleEntrancePortalTeleported;
        }

        private void HandleEntrancePortalTeleported(Interactable portal, Transform player)
        {
            if (role != PortalRole.EntranceToDungeon || generator == null)
            {
                return;
            }

            generator.AnnounceCurrentLevelAtPlayer(player);
        }

        private void EnsureInteractableReference()
        {
            if (interactable == null)
            {
                interactable = GetComponent<Interactable>();
            }
        }
    }
}







