using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Training
{
    /// <summary>
    /// IsTrigger collider: atanmış <see cref="FireSource"/> <b>aktif</b> ve <b>henüz sönmemişken</b>
    /// oyuncu bu hacimdeyken <see cref="ForcedCriticalProximityRegistry"/> ile kritik yakınlık kabul edilir.
    /// Yangın söner veya devre dışı kalırsa çıkış beklemeden registry güncellenir.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Woi/Training/Fire Critical Proximity Volume")]
    public sealed class FireCriticalProximityVolume : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Bu hacim hangi yangın için kritik tepki üretsin (HUD / anons o yangına bağlı olmalı).")]
        [SerializeField] private FireSource _fireSource;

        [Header("Who counts as player")]
        [Tooltip("Boşsa: Player tag veya root’ta CharacterController ile eşleşen colliderlar.")]
        [SerializeField] private LayerMask _playerLayers;

        [SerializeField] private string _playerTag = "Player";

        [Tooltip("Açıksa yalnızca Player Layers ile eşleşen colliderlar sayılır (tag/CC yok sayılır).")]
        [SerializeField] private bool _useLayerMask;

        /// <summary>Ham trigger overlap (birden fazla collider olabilir).</summary>
        int _physicalOverlapCount;

        /// <summary>Bu hacim için registry’de tuttuğumuz tek artış (true = Increment yapıldı).</summary>
        bool _registryHeld;

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null)
                c.isTrigger = true;
        }

        void LateUpdate() =>
            SyncRegistryWithFireState();

        void OnTriggerEnter(Collider other)
        {
            if (_fireSource == null || !IsConsideredPlayer(other))
                return;

            _physicalOverlapCount++;
            SyncRegistryWithFireState();
        }

        void OnTriggerExit(Collider other)
        {
            if (_fireSource == null || !IsConsideredPlayer(other))
                return;

            if (_physicalOverlapCount > 0)
                _physicalOverlapCount--;

            SyncRegistryWithFireState();
        }

        void OnDisable()
        {
            _physicalOverlapCount = 0;
            ReleaseRegistryIfHeld();
        }

        void SyncRegistryWithFireState()
        {
            bool wantRegistry = _physicalOverlapCount > 0 && ShouldVolumeForceProximity();

            if (wantRegistry == _registryHeld)
                return;

            if (wantRegistry)
            {
                ForcedCriticalProximityRegistry.Increment(_fireSource);
                _registryHeld = true;
            }
            else
            {
                ReleaseRegistryIfHeld();
            }
        }

        void ReleaseRegistryIfHeld()
        {
            if (!_registryHeld || _fireSource == null)
            {
                _registryHeld = false;
                return;
            }

            ForcedCriticalProximityRegistry.Decrement(_fireSource);
            _registryHeld = false;
        }

        /// <summary>Sahnede referans geçerli, bileşen etkin ve yangın tamamen sönmemiş olmalı.</summary>
        bool ShouldVolumeForceProximity()
        {
            if (_fireSource == null)
                return false;

            if (!_fireSource.isActiveAndEnabled)
                return false;

            if (_fireSource.IsExtinguished)
                return false;

            return true;
        }

        bool IsConsideredPlayer(Collider other)
        {
            if (other == null)
                return false;

            if (_useLayerMask && _playerLayers.value != 0)
                return ((_playerLayers.value & (1 << other.gameObject.layer)) != 0);

            if (!string.IsNullOrEmpty(_playerTag) && other.CompareTag(_playerTag))
                return true;

            Transform t = other.transform;
            if (t.GetComponentInParent<CharacterController>() != null)
                return true;

            return false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            Collider c = GetComponent<Collider>();
            if (c != null && !c.isTrigger)
                Debug.LogWarning($"[{nameof(FireCriticalProximityVolume)}] Collider on '{name}' should be a trigger.", this);
        }
#endif
    }
}
