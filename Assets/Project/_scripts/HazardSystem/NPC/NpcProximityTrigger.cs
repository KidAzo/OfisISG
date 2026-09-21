using UnityEngine;
using UnityEngine.Events;
using Woi.Player;

namespace HazardSystem.NPC
{
    public class NpcProximityTrigger : MonoBehaviour
    {
        [Header("NPC's")]
        [SerializeField] Transform npcA;
        [SerializeField] Transform npcB;

        [Header("Trigger")]
        [SerializeField] float triggerDistance = 2f;
        float playerTriggerDistance = 3f;

        [SerializeField] UnityEvent onCloseEnough;
        [SerializeField] UnityEvent onFarEnough;

        PlayerDistanceChecker _distanceChecker;

        bool _fired;
        bool _inRange;
        bool InPlayerTriggerRange => _distanceChecker != null && _distanceChecker.IsPlayerCloseEnough();

        Vector3 _startPositionA;
        Vector3 _startPositionB;

        void Start()
        {
            playerTriggerDistance = triggerDistance;

            Transform playerTransform = ResolvePlayerTransform();
            if (playerTransform == null)
            {
                Debug.LogWarning("[NpcProximityTrigger] Player transform not found; proximity trigger disabled.");
                enabled = false;
                return;
            }

            _distanceChecker = new PlayerDistanceChecker(transform, playerTransform, playerTriggerDistance);

            if (npcA != null)
                _startPositionA = npcA.position;
            if (npcB != null)
                _startPositionB = npcB.position;
        }

        void Update()
        {
            if (!_inRange && !InPlayerTriggerRange)
            {
                onFarEnough?.Invoke();
                return;
            }

            _inRange = true;

            if (_fired)
                return;

            _fired = true;
            onCloseEnough?.Invoke();
        }

        public void ResetTrigger()
        {
            _fired = false;
            _inRange = false;
            if (npcA != null)
                npcA.position = _startPositionA;
            if (npcB != null)
                npcB.position = _startPositionB;
        }

        static Transform ResolvePlayerTransform()
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                return player.transform;

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                return tagged.transform;

            return Camera.main != null ? Camera.main.transform : null;
        }
    }

    public class PlayerDistanceChecker
    {
        readonly Transform _npcTransform;
        readonly Transform _playerTransform;
        readonly float _triggerDistanceSqr;

        public PlayerDistanceChecker(Transform npcTransform, Transform playerTransform, float triggerDistance)
        {
            _npcTransform = npcTransform;
            _playerTransform = playerTransform;
            _triggerDistanceSqr = triggerDistance * triggerDistance;
        }

        public bool IsPlayerCloseEnough()
        {
            if (_npcTransform == null || _playerTransform == null)
                return false;

            float sqr = (_npcTransform.position - _playerTransform.position).sqrMagnitude;
            return sqr <= _triggerDistanceSqr;
        }
    }
}
