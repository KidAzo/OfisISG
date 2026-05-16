using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.XR;

namespace Woi.Equipment
{
    /// <summary>
    /// VR: sıkma (spray) başlayınca <b>tüpü tutmayan</b> elde (hortum/nozzle eli) önce güçlü, ardından
    /// basılı tutulduğu sürece daha düşük periyodik haptic. <see cref="ExtinguisherController"/> olaylarına bağlanır.
    /// </summary>
    [AddComponentMenu("Woi/Equipment/VR Spray Nozzle Hand Haptics")]
    [DefaultExecutionOrder(20)]
    public sealed class VrSprayNozzleHandHaptics : MonoBehaviour
    {
        [SerializeField]
        ExtinguisherController _controller;

        [Header("Hedef el")]
        [Tooltip("Hortum/nozzle bu elde. Boşsa: tüpü tutan VRHandExtinguisherGrabber’ın karşıtı.")]
        [SerializeField]
        VRHandExtinguisherGrabber _nozzleHandOverride;

        [Header("Başlangıç — güçlü darbe")]
        [SerializeField, Range(0f, 1f)]
        float _burstAmplitude = 0.88f;

        [SerializeField, Min(0.02f)]
        float _burstDurationSeconds = 0.14f;

        [Header("Sürmekte — daha düşük tekrar")]
        [SerializeField, Range(0f, 1f)]
        float _sustainAmplitude = 0.2f;

        [SerializeField, Min(0.01f)]
        float _sustainImpulseSeconds = 0.035f;

        [SerializeField, Min(0.04f)]
        float _sustainIntervalSeconds = 0.09f;

        [Tooltip("İlk güçlü darbeden sonra ilk düşük darbenin gecikmesi (s).")]
        [SerializeField, Min(0f)]
        float _delayBeforeSustainSeconds = 0.08f;

        bool _spraying;
        float _nextSustainUnscaledTime;
        XRNode _nozzleNode;

        void Reset()
        {
            _controller = GetComponent<ExtinguisherController>();
        }

        void Awake()
        {
            if (_controller == null)
                _controller = GetComponent<ExtinguisherController>();
        }

        void OnEnable()
        {
            if (_controller == null)
                return;

            _controller.OnSprayStarted += HandleSprayStarted;
            _controller.OnSprayStopped += HandleSprayStopped;
        }

        void OnDisable()
        {
            if (_controller == null)
                return;

            _controller.OnSprayStarted -= HandleSprayStarted;
            _controller.OnSprayStopped -= HandleSprayStopped;
            _spraying = false;
        }

        void Update()
        {
            if (!_spraying || _controller == null || !_controller.IsDischarging)
                return;

            if (Time.unscaledTime < _nextSustainUnscaledTime)
                return;

            TrySendImpulse(_nozzleNode, _sustainAmplitude, _sustainImpulseSeconds);
            _nextSustainUnscaledTime = Time.unscaledTime + _sustainIntervalSeconds;
        }

        void HandleSprayStarted()
        {
            if (!IsXrLikely())
                return;

            _nozzleNode = ResolveNozzleHandNode();
            TrySendImpulse(_nozzleNode, _burstAmplitude, _burstDurationSeconds);

            _spraying = true;
            _nextSustainUnscaledTime = Time.unscaledTime + _burstDurationSeconds + _delayBeforeSustainSeconds;
        }

        void HandleSprayStopped()
        {
            _spraying = false;
        }

        static bool IsXrLikely()
        {
#pragma warning disable CS0618
            if (XRSettings.isDeviceActive)
                return true;
#pragma warning restore CS0618

            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            return left.isValid || right.isValid;
        }

        XRNode ResolveNozzleHandNode()
        {
            if (_nozzleHandOverride != null)
                return _nozzleHandOverride.handType == VRHandType.Right ? XRNode.RightHand : XRNode.LeftHand;

            VRHandExtinguisherGrabber[] grabbers =
                FindObjectsByType<VRHandExtinguisherGrabber>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            VRHandExtinguisherGrabber tubeHand = null;
            for (int i = 0; i < grabbers.Length; i++)
            {
                VRHandExtinguisherGrabber g = grabbers[i];
                if (g != null && g.IsHoldingExtinguisher)
                {
                    tubeHand = g;
                    break;
                }
            }

            if (tubeHand == null)
                return XRNode.RightHand;

            return tubeHand.handType == VRHandType.Left ? XRNode.RightHand : XRNode.LeftHand;
        }

        static void TrySendImpulse(XRNode node, float amplitude, float duration)
        {
            amplitude = Mathf.Clamp01(amplitude);
            if (amplitude <= 0.001f || duration <= 0f)
                return;

            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
                return;

            // Bazı cihazlarda kanal 0 veya 1 (tetik motoru).
            if (!TryImpulse(device, 0, amplitude, duration))
                TryImpulse(device, 1, amplitude * 0.85f, duration);
        }

        static bool TryImpulse(InputDevice device, uint motorChannel, float amplitude, float duration)
        {
            if (!device.isValid)
                return false;

            return device.SendHapticImpulse(motorChannel, amplitude, duration);
        }
    }
}
