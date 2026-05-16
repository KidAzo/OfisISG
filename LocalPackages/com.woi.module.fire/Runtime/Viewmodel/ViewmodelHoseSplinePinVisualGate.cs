using FireExtinguisher.Core;
using UnityEngine;
using Woi.Equipment;

namespace Woi.Viewmodel
{
    /// <summary>
    /// While the extinguisher is <b>equipped</b>: before pin → legacy hose on, spline off;
    /// after pin → spline hose on, legacy off (nozzle snap from <see cref="nozzlePoseAfterPin"/>).
    /// <b>XR port:</b> after pin the spline stays off until <see cref="ExtinguisherController.IsVrHoseSplineVisualReady"/>
    /// (nozzle proximity snap). <b>PC port:</b> unchanged — spline follows pin only.
    /// When the player <b>drops</b> the item (e.g. drop input), spline is hidden and legacy hose is shown again,
    /// even if the pin was still pulled or agent remains in the tube. Optional <see cref="nozzleVisualRoots"/> follow the same on/off.
    /// </summary>
    /// <remarks>
    /// Runs at default order -110 so <see cref="ViewmodelHoseSplineDriver"/> (-100) reads the snapped nozzle in the same frame.
    /// </remarks>
    [DefaultExecutionOrder(-110)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Viewmodel/Hose Spline Pin Visual Gate")]
    public sealed class ViewmodelHoseSplinePinVisualGate : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Defaults to a child ExtinguisherController.")]
        [SerializeField]
        ExtinguisherController extinguisherController;

        [Tooltip("Defaults to a child ViewmodelHoseSplineDriver.")]
        [SerializeField]
        ViewmodelHoseSplineDriver hoseSplineDriver;

        [Tooltip("Pickup item on this extinguisher; used to know equipped vs dropped in world.")]
        [SerializeField]
        ExtinguisherPickupItem pickupItem;

        [Tooltip("Empty whose world position/rotation define the nozzle after the pin (e.g. NozzlePosAfterPin).")]
        [SerializeField]
        Transform nozzlePoseAfterPin;

        [Header("Visual toggles")]
        [Tooltip("Spline + Extrude root (only while equipped, pin pulled).")]
        [SerializeField]
        GameObject splineVisualRoot;

        [Tooltip("Static hose mesh object(s) when spline hose is hidden.")]
        [SerializeField]
        GameObject[] legacyHoseRoots;

        [Header("Nozzle visual")]
        [Tooltip("Nozzle mesh / tip / FX roots: active only while spline hose is shown (equipped + pin). Hidden on drop or before pin. Prefer objects separate from the hose driver's nozzle anchor if that anchor must stay active.")]
        [SerializeField]
        GameObject[] nozzleVisualRoots;

        bool _wasShowingSplineHose;
        bool _loggedMissing;

        void Awake()
        {
            if (extinguisherController == null)
                extinguisherController = GetComponentInChildren<ExtinguisherController>(true);
            if (hoseSplineDriver == null)
                hoseSplineDriver = GetComponentInChildren<ViewmodelHoseSplineDriver>(true);
            if (pickupItem == null)
                pickupItem = GetComponent<ExtinguisherPickupItem>() ?? GetComponentInParent<ExtinguisherPickupItem>();
        }

        void Start()
        {
            if (extinguisherController == null || hoseSplineDriver == null)
            {
                LogOnce("Assign ExtinguisherController and ViewmodelHoseSplineDriver (or place under this hierarchy).");
                enabled = false;
                return;
            }

            if (pickupItem == null)
                LogOnce("No ExtinguisherPickupItem found — drop detection disabled; visuals follow pin only.");

            _wasShowingSplineHose = ComputeShowSplineHose();
            ApplyVisualForSplineHose(_wasShowingSplineHose, snapNozzle: _wasShowingSplineHose && !FirePlatformRuntime.IsVR);
        }

        void LateUpdate()
        {
            if (extinguisherController == null || hoseSplineDriver == null)
                return;

            bool show = ComputeShowSplineHose();
            if (show == _wasShowingSplineHose)
                return;

            if (show)
                // XR: nozzle pozisyonu VRExtinguisherPinPuller'da; PC: pim sonrası nozzlePoseAfterPin'e snap.
                ApplyVisualForSplineHose(true, snapNozzle: !FirePlatformRuntime.IsVR);
            else
                ApplyVisualForSplineHose(false, snapNozzle: false);

            _wasShowingSplineHose = show;
        }

        bool ComputeShowSplineHose()
        {
            if (pickupItem != null && !pickupItem.IsEquipped)
                return false;

            if (!extinguisherController.IsPinPulled)
                return false;

            // PC: pim çekilince spline açılır. XR: pim + boş el nozzle yakınlık snap'i (VRExtinguisherPinPuller) sonrası.
            if (FirePlatformRuntime.IsVR && !extinguisherController.IsVrHoseSplineVisualReady)
                return false;

            return true;
        }

        void ApplyVisualForSplineHose(bool showSplineHose, bool snapNozzle)
        {
            if (showSplineHose)
            {
                // Enable nozzle meshes before snapping so nozzleRoot (if under these roots) has valid hierarchy state.
                SetNozzleVisualsActive(true);

                if (snapNozzle && nozzlePoseAfterPin != null)
                    hoseSplineDriver.SnapNozzleToWorldPose(nozzlePoseAfterPin.position, nozzlePoseAfterPin.rotation);
                else if (snapNozzle && nozzlePoseAfterPin == null)
                    LogOnce("Nozzle Pose After Pin is not assigned — spline enabled but nozzle not snapped.");

                if (splineVisualRoot != null)
                    splineVisualRoot.SetActive(true);

                SetLegacyRootsActive(false);
            }
            else
            {
                SetNozzleVisualsActive(false);

                if (splineVisualRoot != null)
                    splineVisualRoot.SetActive(false);

                SetLegacyRootsActive(true);
            }
        }

        void SetNozzleVisualsActive(bool value)
        {
            if (nozzleVisualRoots == null)
                return;
            for (int i = 0; i < nozzleVisualRoots.Length; i++)
            {
                if (nozzleVisualRoots[i] != null)
                    nozzleVisualRoots[i].SetActive(value);
            }
        }

        void SetLegacyRootsActive(bool value)
        {
            if (legacyHoseRoots == null)
                return;
            for (int i = 0; i < legacyHoseRoots.Length; i++)
            {
                if (legacyHoseRoots[i] != null)
                    legacyHoseRoots[i].SetActive(value);
            }
        }

        void LogOnce(string message)
        {
            if (_loggedMissing)
                return;
            _loggedMissing = true;
            Debug.LogWarning("[ViewmodelHoseSplinePinVisualGate] " + message, this);
        }
    }
}
