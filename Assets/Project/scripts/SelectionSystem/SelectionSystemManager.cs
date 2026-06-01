using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.SelectionSystem
{
    /// <summary>
    /// Waste / gameplay selection: PC uses mouse click; VR uses right trigger + right-controller ray
    /// (<see cref="SelectionVrInteractionRay"/> → <see cref="FireVrGameplayInteractionRay"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class SelectionSystemManager : MonoBehaviour
    {
        private const string InteractEventPath =
            "Packages/com.woi.module.fire/Runtime/InputSystem/InputsSO/InputEvents/onInteractInput.asset";

        [Header("PC")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private InputAction mouseLeftClick;

        [Header("VR")]
        [SerializeField] private ScriptableEventNoParam interactInputEvent;
        [SerializeField] private SelectionVrInteractionRay vrInteractionRay;

        [Header("Raycast")]
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private LayerMask selectionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Gates")]
        [SerializeField] private List<MonoBehaviour> selectionGates = new();

        private SelectionSystemController selectionSystemController;
        private SelectionInputController selectionInputController;
        private bool vrInputSubscribed;
        private bool inputEnabled = true;

        private void Start()
        {
            EnsurePortingReady();
            ResolveVrRay();
            BuildController();

            if (IsVrMode())
                EnableVrInput();
            else
                EnablePcInput();
        }

        private void OnDisable()
        {
            DisablePcInput();
            DisableVrInput();
        }

        public void SetSelectionInputEnabled(bool enabled)
        {
            inputEnabled = enabled;

            if (!enabled)
            {
                DisablePcInput();
                DisableVrInput();
                return;
            }

            if (IsVrMode())
            {
                DisablePcInput();
                EnableVrInput();
            }
            else
            {
                DisableVrInput();
                EnablePcInput();
            }
        }

        private void BuildController()
        {
            selectionSystemController = new SelectionSystemController(
                mainCamera,
                maxDistance,
                selectionMask,
                triggerInteraction);
        }

        private void EnablePcInput()
        {
            if (!inputEnabled || IsVrMode() || mouseLeftClick == null)
                return;

            selectionInputController ??= new SelectionInputController(
                selectionSystemController,
                mouseLeftClick,
                CanProcessSelection);
            selectionInputController.Enable();
        }

        private void DisablePcInput()
        {
            selectionInputController?.Disable();
        }

        private void EnableVrInput()
        {
            if (!inputEnabled || !IsVrMode())
                return;

            ResolveInteractEvent();
            if (interactInputEvent != null && !vrInputSubscribed)
            {
                interactInputEvent.OnRaised += OnVrInteractInput;
                vrInputSubscribed = true;
            }
        }

        private void DisableVrInput()
        {
            if (interactInputEvent != null && vrInputSubscribed)
            {
                interactInputEvent.OnRaised -= OnVrInteractInput;
                vrInputSubscribed = false;
            }
        }

        private void OnVrInteractInput()
        {
            if (!CanProcessSelection())
                return;

            if (!TryGetVrRay(out Vector3 origin, out Vector3 direction))
                return;

            Transform skipRoot = FireVrGameplayInteractionRay.RegisteredRayOriginOrNull;
            ISelectable selectable = selectionSystemController.SelectFromWorldRay(origin, direction, skipRoot);
            selectable?.Select();
        }

        private static bool TryGetVrRay(out Vector3 origin, out Vector3 direction)
        {
            if (FireVrGameplayInteractionRay.TryGetRay(out origin, out direction))
                return true;

            Camera cam = Camera.main;
            if (cam == null)
            {
                origin = default;
                direction = default;
                return false;
            }

            origin = cam.transform.position;
            direction = cam.transform.forward;
            return direction.sqrMagnitude > 1e-8f;
        }

        private bool CanProcessSelection()
        {
            if (!inputEnabled)
                return false;

            for (int i = 0; i < selectionGates.Count; i++)
            {
                MonoBehaviour gateBehaviour = selectionGates[i];
                if (gateBehaviour is not ISelectionInputGate gate)
                    continue;

                if (!gate.CanSelect)
                    return false;
            }

            return true;
        }

        private void ResolveVrRay()
        {
            if (vrInteractionRay != null)
                return;

            vrInteractionRay = FindFirstObjectByType<SelectionVrInteractionRay>(FindObjectsInactive.Include);
        }

        private void ResolveInteractEvent()
        {
            if (interactInputEvent != null)
                return;

#if UNITY_EDITOR
            interactInputEvent =
                UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableEventNoParam>(InteractEventPath);
#endif
        }

        private static void EnsurePortingReady()
        {
            if (FirePlatformRuntime.IsSourceInitialized)
                return;

#if UNITY_EDITOR
            var porting = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableEnumPortingVariable>(
                "Packages/com.woi.module.fire/Runtime/Porting/PortingVariable.asset");
            if (porting != null)
                FirePlatformRuntime.TryInitialize(porting);
#endif
        }

        private static bool IsVrMode()
        {
            return FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR;
        }
    }
}
