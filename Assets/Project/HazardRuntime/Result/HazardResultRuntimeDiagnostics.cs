using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Woi.HazardSystem
{
    /// <summary>
    /// Temporary read-only snapshot of the Hazard result UI interaction chain.
    /// Logs once when the result opens, again only on a right-trigger press, and on real pointer events.
    /// </summary>
    public sealed class HazardResultRuntimeDiagnostics : MonoBehaviour
    {
        const string Project = "OfisISG";
        const string Tag = "[HazardResultRuntime]";

        InputAction _uiPress;
        bool _wasRightPressed;

        public static void Begin(Transform resultPanel)
        {
            if (resultPanel == null)
                return;

            HazardResultRuntimeDiagnostics diagnostics = resultPanel.GetComponent<HazardResultRuntimeDiagnostics>();
            if (diagnostics == null)
                diagnostics = resultPanel.gameObject.AddComponent<HazardResultRuntimeDiagnostics>();

            diagnostics.Arm();
        }

        void Arm()
        {
            _uiPress = FindRightUiPress();
            _wasRightPressed = RightTriggerPressed(_uiPress);
            AttachPointerProbes();
            Debug.Log(BuildOpenSnapshot(), this);
        }

        void Update()
        {
            bool pressed = RightTriggerPressed(_uiPress);
            if (pressed && !_wasRightPressed)
                Debug.Log(BuildTriggerSnapshot(), this);

            _wasRightPressed = pressed;
        }

        void AttachPointerProbes()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || !IsWatchedButton(button.gameObject.name))
                    continue;
                if (button.GetComponent<HazardResultPointerProbe>() == null)
                    button.gameObject.AddComponent<HazardResultPointerProbe>();
            }
        }

        static bool IsWatchedButton(string objectName)
        {
            return objectName == "Quit" || objectName == "Founded" || objectName == "Found" || objectName == "Missed";
        }

        string BuildOpenSnapshot()
        {
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            GraphicRaycaster graphicRaycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            TrackedDeviceGraphicRaycaster trackedRaycaster = canvas != null ? canvas.GetComponent<TrackedDeviceGraphicRaycaster>() : null;
            EventSystem[] eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            EventSystem eventSystem = EventSystem.current;
            XRUIInputModule xrModule = eventSystem != null ? eventSystem.GetComponent<XRUIInputModule>() : null;
            InputSystemUIInputModule inputModule = eventSystem != null ? eventSystem.GetComponent<InputSystemUIInputModule>() : null;
            NearFarInteractor right = FindRightNearFar();
            XRPokeInteractor poke = FindRightPoke();
            Transform head = FindHead();
            Transform resultPose = FindNamed("ResultPos");
            Transform origin = FindXrOriginTransform();
            CurveInteractionCaster caster = right != null ? right.farInteractionCaster as CurveInteractionCaster : null;

            var log = new StringBuilder();
            log.Append(Tag).Append('\n');
            log.Append("project=").Append(Project).Append('\n');
            log.Append("resultPanelActive=").Append(gameObject.activeInHierarchy).Append('\n');
            log.Append("resultPanelPos=").Append(transform.position.ToString("F3")).Append('\n');
            log.Append("resultPanelRot=").Append(transform.rotation.eulerAngles.ToString("F1")).Append('\n');
            log.Append("canvasRenderMode=").Append(canvas != null ? canvas.renderMode.ToString() : "none").Append('\n');
            log.Append("canvasWorldCamera=").Append(canvas != null && canvas.worldCamera != null ? canvas.worldCamera.name : "none").Append('\n');
            log.Append("graphicRaycasterEnabled=").Append(graphicRaycaster != null && graphicRaycaster.enabled).Append('\n');
            log.Append("trackedRaycasterEnabled=").Append(trackedRaycaster != null && trackedRaycaster.enabled).Append('\n');
            log.Append("eventSystem=").Append(eventSystem != null ? eventSystem.name : "none").Append('\n');
            log.Append("currentInputModule=").Append(eventSystem != null && eventSystem.currentInputModule != null ? eventSystem.currentInputModule.GetType().Name : "none").Append('\n');
            log.Append("xrUiModuleEnabled=").Append(xrModule != null && xrModule.enabled).Append('\n');
            log.Append("rightInteractor=").Append(right != null ? PathOf(right.transform) : "none").Append('\n');
            log.Append("rightInteractorActive=").Append(right != null && right.isActiveAndEnabled).Append('\n');
            log.Append("farCasting=").Append(right != null && right.enableFarCasting).Append('\n');
            log.Append("nearCasting=").Append(right != null && right.enableNearCasting).Append('\n');
            log.Append("uiInteraction=").Append(right != null && right.enableUIInteraction).Append('\n');
            log.Append("hasSelection=").Append(right != null && right.hasSelection).Append('\n');
            log.Append("blockUIOnSelection=").Append(right != null && right.blockUIOnInteractableSelection).Append('\n');
            log.Append("blockedByInteractionGroup=").Append(right != null && right.IsBlockedByInteractionWithinGroup()).Append('\n');
            log.Append("uiPressActionEnabled=").Append(_uiPress != null && _uiPress.enabled).Append('\n');
            log.Append("uiPressValue=").Append(PressValue(_uiPress).ToString("F2")).Append('\n');
            log.Append("xrOriginPos=").Append(origin != null ? origin.position.ToString("F3") : "none").Append('\n');
            log.Append("headPos=").Append(head != null ? head.position.ToString("F3") : "none").Append('\n');
            log.Append("resultPos=").Append(resultPose != null ? resultPose.position.ToString("F3") : "none").Append('\n');
            AppendEventSystems(log, eventSystems, eventSystem, xrModule, inputModule);
            AppendInteractor(log, right, caster, poke);
            AppendCanvas(log, canvas, canvasRect, graphicRaycaster, trackedRaycaster);
            AppendButtons(log);
            AppendPose(log, canvasRect, head, right, caster);
            return log.ToString();
        }

        string BuildTriggerSnapshot()
        {
            NearFarInteractor right = FindRightNearFar();
            var log = new StringBuilder();
            log.Append(Tag).Append('\n');
            log.Append("project=").Append(Project).Append('\n');
            log.Append("event=trigger").Append('\n');

            RaycastResult result = default;
            bool hit = right != null && right.TryGetCurrentUIRaycastResult(out result) && result.gameObject != null;
            if (hit)
            {
                log.Append("uiHit=").Append(result.gameObject.name).Append('\n');
                log.Append("uiHitDistance=").Append(result.distance.ToString("F3")).Append('\n');
                log.Append("uiHitModule=").Append(result.module != null ? result.module.GetType().Name : "none").Append('\n');
                log.Append("uiHitSortingLayer=").Append(result.sortingLayer).Append('\n');
                log.Append("uiHitSortingOrder=").Append(result.sortingOrder).Append('\n');
            }
            else
            {
                log.Append("uiHit=none\nuiHitDistance=\nuiHitModule=\nuiHitSortingLayer=\nuiHitSortingOrder=\n");
                AppendRay(log, right);
            }

            log.Append("uiPressEnabled=").Append(_uiPress != null && _uiPress.enabled).Append('\n');
            log.Append("uiPressValue=").Append(PressValue(_uiPress).ToString("F2"));
            return log.ToString();
        }

        static void AppendEventSystems(StringBuilder log, EventSystem[] eventSystems, EventSystem current, XRUIInputModule xrModule, InputSystemUIInputModule inputModule)
        {
            log.Append("activeEventSystemCount=").Append(eventSystems.Length).Append('\n');
            for (int i = 0; i < eventSystems.Length; i++)
            {
                EventSystem system = eventSystems[i];
                log.Append("eventSystem[").Append(i).Append("]=").Append(system != null ? system.name : "null");
                log.Append(" current=").Append(system == current).Append('\n');
            }

            if (current != null)
            {
                BaseInputModule[] modules = current.GetComponents<BaseInputModule>();
                for (int i = 0; i < modules.Length; i++)
                    log.Append("module[").Append(i).Append("]=").Append(modules[i].GetType().Name).Append(modules[i].enabled ? "(on)" : "(off)").Append('\n');
            }

            log.Append("inputSystemUiModuleEnabled=").Append(inputModule != null && inputModule.enabled).Append('\n');
            AppendActionReference(log, "pointAction", xrModule != null ? xrModule.pointAction : null);
            AppendActionReference(log, "leftClickAction", xrModule != null ? xrModule.leftClickAction : null);
        }

        static void AppendActionReference(StringBuilder log, string label, InputActionReference reference)
        {
            InputAction action = reference != null ? reference.action : null;
            log.Append(label).Append('=');
            if (action == null)
            {
                log.Append("none\n");
                return;
            }

            log.Append(action.actionMap != null ? action.actionMap.name : "no-map").Append('/').Append(action.name);
            log.Append(" enabled=").Append(action.enabled);
            log.Append(" bindings=").Append(BindingList(action)).Append('\n');
        }

        static void AppendInteractor(StringBuilder log, NearFarInteractor right, CurveInteractionCaster caster, XRPokeInteractor poke)
        {
            if (right == null)
            {
                log.Append("rightInteractorMissing=true\n");
            }
            else
            {
                log.Append("rightActiveInHierarchy=").Append(right.gameObject.activeInHierarchy).Append('\n');
                log.Append("rightEnabled=").Append(right.enabled).Append('\n');
                log.Append("castSource=").Append(ReadCastSource(right)).Append('\n');
                log.Append("containingGroup=").Append(GroupName(right)).Append('\n');
                log.Append("castDistance=").Append(caster != null ? caster.castDistance.ToString("F2") : "n/a").Append('\n');
                log.Append("raycastMask=").Append(caster != null ? caster.raycastMask.value.ToString() : "n/a").Append('\n');
                AppendRay(log, right);
            }

            log.Append("poke=").Append(poke != null ? PathOf(poke.transform) : "none");
            if (poke != null)
            {
                log.Append(" active=").Append(poke.isActiveAndEnabled);
                log.Append(" blocked=").Append(poke.IsBlockedByInteractionWithinGroup());
            }
            log.Append('\n');
        }

        static void AppendRay(StringBuilder log, NearFarInteractor right)
        {
            if (right == null || right.curveOrigin == null)
            {
                log.Append("rayOrigin=none\nrayDirection=none\n");
                return;
            }

            Transform origin = right.curveOrigin;
            log.Append("rayOrigin=").Append(origin.position.ToString("F3")).Append('\n');
            log.Append("rayDirection=").Append(origin.forward.ToString("F3")).Append('\n');
        }

        static void AppendCanvas(StringBuilder log, Canvas canvas, RectTransform canvasRect, GraphicRaycaster graphicRaycaster, TrackedDeviceGraphicRaycaster tracked)
        {
            if (canvasRect == null)
            {
                log.Append("canvas=none\n");
                return;
            }

            log.Append("canvasScale=").Append(canvasRect.lossyScale.ToString("F4")).Append('\n');
            log.Append("canvasLayer=").Append(LayerMask.LayerToName(canvas.gameObject.layer)).Append('\n');
            log.Append("canvasActive=").Append(canvas.gameObject.activeInHierarchy).Append('\n');
            log.Append("canvasEnabled=").Append(canvas.enabled).Append('\n');
            log.Append("canvasSortingOrder=").Append(canvas.sortingOrder).Append('\n');
            log.Append("canvasSortingLayer=").Append(canvas.sortingLayerName).Append('\n');
            if (tracked != null)
            {
                log.Append("trackedCheck2D=").Append(tracked.checkFor2DOcclusion).Append('\n');
                log.Append("trackedCheck3D=").Append(tracked.checkFor3DOcclusion).Append('\n');
                log.Append("trackedBlockingMask=").Append(tracked.blockingMask.value).Append('\n');
                log.Append("trackedIgnoreReversed=").Append(tracked.ignoreReversedGraphics).Append('\n');
            }
            if (graphicRaycaster != null)
                log.Append("graphicBlockingObjects=").Append(graphicRaycaster.blockingObjects).Append('\n');

            CanvasGroup[] groups = canvas.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                log.Append("canvasGroup=").Append(group.name);
                log.Append(" interactable=").Append(group.interactable);
                log.Append(" blocksRaycasts=").Append(group.blocksRaycasts);
                log.Append(" alpha=").Append(group.alpha.ToString("F2")).Append('\n');
            }
        }

        void AppendButtons(StringBuilder log)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || !IsWatchedButton(button.gameObject.name))
                    continue;

                log.Append("button=").Append(PathOf(button.transform));
                log.Append(" enabled=").Append(button.enabled);
                log.Append(" interactable=").Append(button.interactable);
                log.Append(" active=").Append(button.gameObject.activeInHierarchy);
                log.Append(" target=").Append(button.targetGraphic != null ? button.targetGraphic.name : "none");
                log.Append(" navigation=").Append(button.navigation.mode);
                log.Append(" persistent=");
                int calls = button.onClick.GetPersistentEventCount();
                if (calls == 0)
                    log.Append("none");
                for (int c = 0; c < calls; c++)
                {
                    Object target = button.onClick.GetPersistentTarget(c);
                    log.Append(target != null ? target.name : "missing-target");
                    log.Append('.').Append(button.onClick.GetPersistentMethodName(c));
                    if (c + 1 < calls)
                        log.Append('|');
                }
                log.Append('\n');

                Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
                for (int g = 0; g < graphics.Length; g++)
                {
                    Graphic graphic = graphics[g];
                    if (graphic == null || !graphic.raycastTarget)
                        continue;
                    log.Append("  raycastGraphic=").Append(graphic.name);
                    log.Append(" type=").Append(graphic.GetType().Name);
                    log.Append(" active=").Append(graphic.gameObject.activeInHierarchy).Append('\n');
                }
            }
        }

        static void AppendPose(StringBuilder log, RectTransform canvasRect, Transform head, NearFarInteractor right, CurveInteractionCaster caster)
        {
            if (canvasRect == null || head == null)
            {
                log.Append("pose=unavailable\n");
                return;
            }

            Vector3 center = canvasRect.TransformPoint(canvasRect.rect.center);
            Vector3 toPanel = center - head.position;
            log.Append("hmdToCanvasCenter=").Append(toPanel.magnitude.ToString("F3")).Append('\n');
            log.Append("hmdToCanvasDirection=").Append(toPanel.sqrMagnitude > 0.0001f ? toPanel.normalized.ToString("F3") : "none").Append('\n');
            log.Append("hmdForwardToCanvasAngle=").Append(Vector3.Angle(head.forward, toPanel).ToString("F1")).Append('\n');

            if (right == null || right.curveOrigin == null)
            {
                log.Append("controllerRayToPlane=none\n");
                return;
            }

            var plane = new Plane(canvasRect.forward, center);
            var ray = new Ray(right.curveOrigin.position, right.curveOrigin.forward);
            log.Append("controllerRayToPlane=");
            if (plane.Raycast(ray, out float enter))
                log.Append(enter.ToString("F3")).Append('\n');
            else
                log.Append("no-hit signed=").Append(plane.GetDistanceToPoint(ray.origin).ToString("F3")).Append('\n');

            if (caster != null)
                log.Append("casterDistance=").Append(caster.castDistance.ToString("F2")).Append('\n');
        }

        static InputAction FindRightUiPress()
        {
            InputAction fallback = null;
            InputActionAsset[] assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            for (int i = 0; i < assets.Length; i++)
            {
                InputActionAsset asset = assets[i];
                if (asset == null)
                    continue;

                foreach (InputActionMap map in asset.actionMaps)
                {
                    InputAction action = map.FindAction("UI Press", false);
                    if (action == null)
                        continue;
                    if (fallback == null)
                        fallback = action;
                    if (BindingList(action).IndexOf("RightHand", System.StringComparison.Ordinal) >= 0)
                        return action;
                }
            }

            return fallback;
        }

        static bool RightTriggerPressed(InputAction action)
        {
            if (action == null || !action.enabled)
                return false;

            for (int i = 0; i < action.controls.Count; i++)
            {
                InputControl control = action.controls[i];
                if (control == null || control.path.IndexOf("RightHand", System.StringComparison.Ordinal) < 0)
                    continue;
                if (control is ButtonControl button && button.isPressed)
                    return true;
            }

            return false;
        }

        static float PressValue(InputAction action)
        {
            if (action == null || !action.enabled)
                return 0f;

            for (int i = 0; i < action.controls.Count; i++)
            {
                InputControl control = action.controls[i];
                if (control == null || control.path.IndexOf("RightHand", System.StringComparison.Ordinal) < 0)
                    continue;
                if (control is ButtonControl button)
                    return button.isPressed ? 1f : 0f;
                if (control is AxisControl axis)
                    return axis.ReadValue();
            }

            return 0f;
        }

        static string BindingList(InputAction action)
        {
            var text = new StringBuilder();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite)
                    continue;
                if (text.Length > 0)
                    text.Append('|');
                text.Append(string.IsNullOrEmpty(binding.effectivePath) ? binding.path : binding.effectivePath);
            }

            return text.Length > 0 ? text.ToString() : "none";
        }

        static string ReadCastSource(NearFarInteractor interactor)
        {
            FieldInfo field = typeof(NearFarInteractor).GetField("m_ValidTargetCastSource", BindingFlags.Instance | BindingFlags.NonPublic);
            object value = field != null ? field.GetValue(interactor) : null;
            return value != null ? value.ToString() : "unavailable";
        }

        static string GroupName(NearFarInteractor interactor)
        {
            if (interactor is not IXRGroupMember member || member.containingGroup == null)
                return "none";

            return member.containingGroup is Object groupObject ? groupObject.name : member.containingGroup.GetType().Name;
        }

        static NearFarInteractor FindRightNearFar()
        {
            NearFarInteractor[] found = FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && HasAncestor(found[i].transform, "Right Controller"))
                    return found[i];
            }

            return null;
        }

        static XRPokeInteractor FindRightPoke()
        {
            XRPokeInteractor[] found = FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && HasAncestor(found[i].transform, "Right Controller"))
                    return found[i];
            }

            return null;
        }

        static Transform FindHead()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                Transform parent = camera != null ? camera.transform.parent : null;
                if (camera != null && camera.CompareTag("MainCamera") && parent != null && parent.name == "Camera Offset")
                    return camera.transform;
            }

            return null;
        }

        static Transform FindXrOriginTransform()
        {
            Transform head = FindHead();
            Transform current = head != null ? head.parent : null;
            while (current != null)
            {
                if (current.name.StartsWith("XR Origin", System.StringComparison.Ordinal))
                    return current;
                current = current.parent;
            }

            return null;
        }

        static Transform FindNamed(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                    return transforms[i];
            }

            return null;
        }

        static bool HasAncestor(Transform transform, string ancestorName)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name == ancestorName)
                    return true;
                current = current.parent;
            }

            return false;
        }

        static string PathOf(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            for (int i = 0; i < 3 && current != null; i++)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }

    sealed class HazardResultPointerProbe : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => Log("PointerEnter");
        public void OnPointerExit(PointerEventData eventData) => Log("PointerExit");
        public void OnPointerDown(PointerEventData eventData) => Log("PointerDown");
        public void OnPointerUp(PointerEventData eventData) => Log("PointerUp");
        public void OnPointerClick(PointerEventData eventData) => Log("PointerClick");

        void Log(string eventName)
        {
            Debug.Log("[HazardResultRuntime]\nproject=OfisISG\nevent=" + eventName + "\nbutton=" + name, this);
        }
    }
}
