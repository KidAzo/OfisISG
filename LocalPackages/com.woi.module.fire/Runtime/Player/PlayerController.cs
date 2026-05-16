using System;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Cysharp.Threading.Tasks;
using WOI.Modules.SDK;

namespace Woi.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        private IPlayerService _playerService;

        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _sprintSpeed = 8f;
        [SerializeField] private float _gravity = -9.81f;
        [SerializeField] private float _groundCheckDistance = 0.2f;

        [Header("Look Settings")]
        [SerializeField] private Transform _cameraPivotTransform;
        public Camera playerCamera;
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private float _maxLookAngle = 80f;

        [Header("Input Actions")]
        [SerializeField] private ScriptableEventVector2 moveInputEvent;
        [SerializeField] private ScriptableEventVector2 lookInputEvent;
        [SerializeField] private ScriptableEventBool sprintInputEvent;

        private CharacterController _characterController;

        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _isSprinting;

        private Vector3 _velocity;
        private float _cameraPitch;
        private bool _lookArmed;

        private void Awake()
        {
            _playerService = ServiceLocator.Get<IPlayerService>();
            _playerService.RegisterPlayer(this);
            _characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            DelayedCursorLock().Forget();
        }

        private async UniTaskVoid DelayedCursorLock()
        {
            _lookInput = Vector2.zero;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: destroyCancellationToken);
            _lookArmed = true;
        }

        private void OnEnable()
        {
            moveInputEvent.OnRaised += OnMove;
            lookInputEvent.OnRaised += OnLook;
            sprintInputEvent.OnRaised += OnSprint;
        }

        private void OnDisable()
        {
            moveInputEvent.OnRaised -= OnMove;
            lookInputEvent.OnRaised -= OnLook;
            sprintInputEvent.OnRaised -= OnSprint;
        }

        private void Update()
        {
            HandleMovement();
            HandleLook();
            ApplyGravity();
        }

        private void HandleMovement()
        {
            float currentSpeed = _isSprinting ? _sprintSpeed : _walkSpeed;

            Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;
            _characterController.Move(move * currentSpeed * Time.deltaTime);
        }

        private void HandleLook()
        {
            if(!_lookArmed) return;

            float yaw = _lookInput.x * _mouseSensitivity;
            transform.Rotate(Vector3.up * yaw);

            _cameraPitch -= _lookInput.y * _mouseSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -_maxLookAngle, _maxLookAngle);

            _cameraPivotTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        }

        private void ApplyGravity()
        {
            if (IsGrounded() && _velocity.y < 0)
            {
                _velocity.y = -2f; // Small downward force to keep grounded
            }

            _velocity.y += _gravity * Time.deltaTime;
            _characterController.Move(_velocity * Time.deltaTime);
        }

        private bool IsGrounded()
        {
            return Physics.Raycast(transform.position, Vector3.down, _groundCheckDistance + 0.1f);
        }

        // Input callbacks
        private void OnMove(Vector2 context)
        {
            _moveInput = context;
        }

        private void OnLook(Vector2 context)
        {
            _lookInput = context;
        }

        private void OnSprint(bool context)
        {
            _isSprinting = context;
        }
    }

    public interface IPlayerService
    {
        Camera playerCamera { get; }
        Transform GetPlayerTransform();
        void SetPlayerLocomotion(Vector3 position);
        void RegisterPlayer(PlayerController player);
        event Action OnPlayerRegistered;
    }

    public interface IXRPlayerService 
    {
        Transform PlayerTransform { get; }
        XRRayInteractor XrRayInteractor { get; }
        Camera PlayerCamera { get; }
        void Register(XRRayInteractor interactor, Camera playerCamera, Transform playerTransform);
    }

    public class XRPlayerService : IXRPlayerService
    {
        public XRRayInteractor XrRayInteractor { get; private set; }
        public Camera PlayerCamera { get; private set; }
        public Transform PlayerTransform { get; private set; }

        public void Register(XRRayInteractor interactor, Camera playerCamera, Transform playerTransform)
        {
            XrRayInteractor = interactor;
            PlayerCamera = playerCamera;
            PlayerTransform = playerTransform;
        }
    }

    public class PlayerService : IPlayerService
    {
        private Transform _playerTransform;
        private PlayerController _playerController;
        private CharacterController ch;

        public Camera playerCamera => _playerController.playerCamera;


        public event Action OnPlayerRegistered;

        public Transform GetPlayerTransform()
        {
            return _playerTransform;
        }

        public void RegisterPlayer(PlayerController player)
        {
            _playerController = player;
            _playerTransform = player.transform;
            ch = player.GetComponent<CharacterController>();
            OnPlayerRegistered?.Invoke();
        }

        public void SetPlayerLocomotion(Vector3 position)
        {
            ch.enabled = false; // Disable CharacterController to avoid collision issues
            ch.transform.position = position;
            ch.transform.rotation = Quaternion.Euler(0, -90, 0);
            ch.enabled = true; // Re-enable CharacterController
        }
    }
}
