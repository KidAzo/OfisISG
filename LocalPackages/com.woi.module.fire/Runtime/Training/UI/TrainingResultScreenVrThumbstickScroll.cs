using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.XR;

namespace Woi.Game.Training.UI
{
    /// <summary>
    /// VR: sağ el thumbstick dikey ekseni ile sonuç <see cref="ScrollView"/> kaydırılır.
    /// Işın/pointer sonuç panelinin <see cref="ScrollView"/> üzerindeyken etkindir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingResultScreenVrThumbstickScroll : MonoBehaviour
    {
        [SerializeField]
        UIDocument document;

        [Tooltip("Boşsa XR sağ el cihazından primary2DAxis okunur. Atanırsa bu action’ın Y bileşeni kullanılır.")]
        [SerializeField]
        InputActionReference rightThumbstickAction;

        [SerializeField, Min(0f)]
        float scrollPixelsPerSecond = 560f;

        [SerializeField, Range(0f, 0.5f)]
        float stickDeadZone = 0.2f;

        ScrollView _scrollView;
        bool _pointerOverScroll;
        Coroutine _bindRoutine;

        void OnPointerEnterScroll(PointerEnterEvent _) => _pointerOverScroll = true;
        void OnPointerLeaveScroll(PointerLeaveEvent _) => _pointerOverScroll = false;

        void Reset()
        {
            document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (rightThumbstickAction != null && rightThumbstickAction.action != null)
                rightThumbstickAction.action.Enable();

            if (_bindRoutine == null)
                _bindRoutine = StartCoroutine(BindScrollWhenReady());
        }

        void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }

            UnbindScroll();

            if (rightThumbstickAction != null && rightThumbstickAction.action != null)
                rightThumbstickAction.action.Disable();
        }

        IEnumerator BindScrollWhenReady()
        {
            for (int i = 0; i < 120 && enabled; i++)
            {
                TryBindScroll();
                if (_scrollView != null)
                    break;
                yield return null;
            }

            _bindRoutine = null;
        }

        void LateUpdate()
        {
            if (!IsVrActive() || _scrollView == null || !_pointerOverScroll)
                return;

            float stickY = ReadRightThumbstickY();
            if (Mathf.Abs(stickY) < stickDeadZone)
                return;

            // Thumbstick aşağı (genelde y negatif) → içerik aşağı kayar (scrollOffset.y artar).
            Vector2 off = _scrollView.scrollOffset;
            off.y += -stickY * scrollPixelsPerSecond * Time.unscaledDeltaTime;
            _scrollView.scrollOffset = off;
        }

        static bool IsVrActive()
        {
            if (FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR)
                return true;

#pragma warning disable CS0618
            return XRSettings.isDeviceActive;
#pragma warning restore CS0618
        }

        void TryBindScroll()
        {
            if (_scrollView != null)
                return;

            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            _scrollView = root.Q<ScrollView>("result-screen-vr-scroll")
                          ?? root.Q<ScrollView>(className: "main-container")
                          ?? FindFirstScrollViewRecursive(root);

            if (_scrollView == null)
                return;

            _scrollView.RegisterCallback<PointerEnterEvent>(OnPointerEnterScroll);
            _scrollView.RegisterCallback<PointerLeaveEvent>(OnPointerLeaveScroll);
        }

        static ScrollView FindFirstScrollViewRecursive(VisualElement ve)
        {
            if (ve == null)
                return null;

            if (ve is ScrollView s)
                return s;

            int n = ve.hierarchy.childCount;
            for (int i = 0; i < n; i++)
            {
                VisualElement c = ve.hierarchy.ElementAt(i);
                ScrollView found = FindFirstScrollViewRecursive(c);
                if (found != null)
                    return found;
            }

            return null;
        }

        void UnbindScroll()
        {
            if (_scrollView == null)
                return;

            _scrollView.UnregisterCallback<PointerEnterEvent>(OnPointerEnterScroll);
            _scrollView.UnregisterCallback<PointerLeaveEvent>(OnPointerLeaveScroll);

            _scrollView = null;
            _pointerOverScroll = false;
        }

        float ReadRightThumbstickY()
        {
            if (rightThumbstickAction != null && rightThumbstickAction.action != null)
                return rightThumbstickAction.action.ReadValue<Vector2>().y;

            UnityEngine.XR.InputDevice dev =
                UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (!dev.isValid)
                return 0f;

            return dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 v)
                ? v.y
                : 0f;
        }
    }
}
