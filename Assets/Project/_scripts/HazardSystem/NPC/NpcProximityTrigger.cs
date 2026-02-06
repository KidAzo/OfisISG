using Reflex.Attributes;
using Sirenix.OdinInspector;
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

        [Inject] IPlayerService playerService;

        bool _fired;
        bool _inRange;
        bool InPlayerTriggerRange => _distanceChecker.IsPlayerCloseEnough();

        Vector3 _startPositionA;
        Vector3 _startPositionB;

        void Start()
        {
            playerTriggerDistance = triggerDistance;

            _distanceChecker = new PlayerDistanceChecker(transform,
            playerService.GetPlayerTransform(),
            playerTriggerDistance);

            _startPositionA = npcA.position;
            _startPositionB = npcB.position;
        }

        void Update()
        {
            if (!_inRange && !InPlayerTriggerRange)
            {
                Debug.Log("Player is not close enough to trigger NPC proximity.");
                onFarEnough?.Invoke();
                return;
            }

            _inRange = true;

            if (_fired) return;

            Debug.Log("NPCs are close enough to trigger event.");
            _fired = true;
            onCloseEnough?.Invoke();
        }

        public void ResetTrigger()
        {
            _fired = false;
            _inRange = false;
            npcA.position = _startPositionA;
            npcB.position = _startPositionB;
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
            float sqr = (_npcTransform.position - _playerTransform.position).sqrMagnitude;
            return sqr <= _triggerDistanceSqr;
        }
    }
}

