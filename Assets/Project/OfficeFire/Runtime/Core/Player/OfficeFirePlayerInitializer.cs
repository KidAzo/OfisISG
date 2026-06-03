using System.Collections.Generic;
using UnityEngine;

namespace Woi.OfficeFire
{
    public sealed class OfficeFirePlayerInitializer : MonoBehaviour
    {
        private const string WasteCollectionTrackerObjectName = "WasteCollectTracker";
        [Header("Player")]
        [SerializeField]
        private Transform playerRoot;

        [SerializeField]
        private bool findPlayerByTagIfMissing = true;

        [SerializeField]
        private string playerTag = "Player";

        [Header("Spawn Points")]
        [SerializeField]
        private List<OfficeFireScenarioSpawnPoint> spawnPoints = new List<OfficeFireScenarioSpawnPoint>();

        [Header("VR / Rig Options")]
        [SerializeField]
        private Transform xrOriginRoot;

        [SerializeField]
        private bool resetRigidbodyVelocity = true;

        [SerializeField]
        private bool disableCharacterControllerDuringTeleport = true;

        public void SetPlayerRoot(Transform newPlayerRoot)
        {
            playerRoot = newPlayerRoot;
        }

        public bool TryGetSpawnPoint(OfficeFireScenarioId scenarioId, out Transform spawnPoint)
        {
            spawnPoint = null;
            if (scenarioId == OfficeFireScenarioId.None)
            {
                return false;
            }

            if (spawnPoints == null)
            {
                return false;
            }

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                OfficeFireScenarioSpawnPoint entry = spawnPoints[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.ScenarioId == scenarioId && entry.SpawnPoint != null)
                {
                    spawnPoint = entry.SpawnPoint;
                    return true;
                }
            }

            return false;
        }

        public void InitializePlayer(OfficeFireScenarioId scenarioId)
        {
            InitializePlayer(scenarioId, null);
        }

        public void InitializePlayer(OfficeFireScenarioId scenarioId, Transform customPlayerRoot)
        {
            if (scenarioId == OfficeFireScenarioId.None)
            {
                Debug.LogWarning("[OfficeFirePlayerInitializer] InitializePlayer(None) ignored.", this);
                return;
            }

            // Cannot reference WasteCollectTracker type here — Woi.OfficeFire.Core is a separate asmdef from gameplay scripts.
            if (customPlayerRoot == null && GameObject.Find(WasteCollectionTrackerObjectName) != null)
            {
                Debug.Log(
                    "[OfficeFirePlayerInitializer] Waste collection active — skipping fire-drill player teleport.",
                    this);
                return;
            }

            Transform movementRoot = customPlayerRoot != null ? customPlayerRoot : ResolveMovementRoot();
            if (movementRoot == null)
            {
                Debug.LogWarning(
                    "[OfficeFirePlayerInitializer] Player root missing (assign Player Root, XR Origin Root, or enable find-by-tag).",
                    this);
                return;
            }

            if (!TryGetSpawnPoint(scenarioId, out Transform spawn))
            {
                Debug.LogWarning(
                    $"[OfficeFirePlayerInitializer] Spawn point missing for scenario '{scenarioId}'.",
                    this);
                return;
            }

            Teleport(movementRoot, spawn, customPlayerRoot == null && xrOriginRoot != null);

            Debug.Log(
                $"[OfficeFirePlayerInitializer] Player initialized for scenario '{scenarioId}' at '{spawn.name}'.",
                this);
        }

        private Transform ResolveMovementRoot()
        {
            if (xrOriginRoot != null)
            {
                return xrOriginRoot;
            }

            return ResolvePlayerRoot();
        }

        private Transform ResolvePlayerRoot()
        {
            if (playerRoot != null)
            {
                return playerRoot;
            }

            if (findPlayerByTagIfMissing && !string.IsNullOrEmpty(playerTag))
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null)
                {
                    return tagged.transform;
                }
            }

            return null;
        }

        private void Teleport(Transform movementRoot, Transform spawn, bool usingXrOrigin)
        {
            if (usingXrOrigin)
            {
                Debug.Log("[OfficeFirePlayerInitializer] Using XR Origin root for teleport.", this);
            }

            CharacterController controller = movementRoot.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = movementRoot.GetComponentInChildren<CharacterController>();
            }

            bool disabledController = false;
            if (disableCharacterControllerDuringTeleport && controller != null)
            {
                controller.enabled = false;
                disabledController = true;
                Debug.Log(
                    "[OfficeFirePlayerInitializer] CharacterController disabled for teleport.",
                    movementRoot);
            }

            movementRoot.SetPositionAndRotation(spawn.position, spawn.rotation);

            if (resetRigidbodyVelocity)
            {
                Rigidbody body = movementRoot.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = movementRoot.GetComponentInChildren<Rigidbody>();
                }

                if (body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            if (disabledController && controller != null)
            {
                controller.enabled = true;
                Debug.Log(
                    "[OfficeFirePlayerInitializer] CharacterController re-enabled after teleport.",
                    movementRoot);
            }
        }
    }
}
