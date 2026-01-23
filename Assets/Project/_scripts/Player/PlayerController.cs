using UnityEngine;
using UnityEngine.InputSystem;

namespace Woi.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _sprintSpeed = 8f;
        [SerializeField] private float _gravity = -9.81f;
        [SerializeField] private float _groundCheckDistance = 0.2f;
        
        [Header("Look Settings")]
        [SerializeField] private Transform _cameraPivotTransform;
        [SerializeField] private float _mouseSensitivity = 2f;
        [SerializeField] private float _maxLookAngle = 80f;
        
        private CharacterController _characterController;
        private PlayerInputActions _playerActions;
        
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _isSprinting;
        
        private Vector3 _velocity;
        private float _cameraPitch;
        
        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _playerActions = new();
            
            // Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        private void OnEnable()
        {
            _playerActions.Enable();
            
            // Bind input events
            _playerActions.Player.Move.performed += OnMove;
            _playerActions.Player.Move.canceled += OnMove;
            
            _playerActions.Player.Look.performed += OnLook;
            _playerActions.Player.Look.canceled += OnLook;
            
            _playerActions.Player.Sprint.performed += OnSprint;
            _playerActions.Player.Sprint.canceled += OnSprint;
        }
        
        private void OnDisable()
        {
            _playerActions.Disable();
            
            _playerActions.Player.Move.performed -= OnMove;
            _playerActions.Player.Move.canceled -= OnMove;
            
            _playerActions.Player.Look.performed -= OnLook;
            _playerActions.Player.Look.canceled -= OnLook;
            
            _playerActions.Player.Sprint.performed -= OnSprint;
            _playerActions.Player.Sprint.canceled -= OnSprint;
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
            // Horizontal rotation (Y axis - rotate player)
            float yaw = _lookInput.x * _mouseSensitivity * Time.deltaTime;;
            transform.Rotate(Vector3.up * yaw);
            
            // Vertical rotation (X axis - rotate camera)
            _cameraPitch -= _lookInput.y * _mouseSensitivity * Time.deltaTime;;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -_maxLookAngle, _maxLookAngle);
            
            if (_cameraPivotTransform != null)
            {
                _cameraPivotTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
            }
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
        private void OnMove(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }
        
        private void OnLook(InputAction.CallbackContext context)
        {
            _lookInput = context.ReadValue<Vector2>();
        }
        
        private void OnSprint(InputAction.CallbackContext context)
        {
            _isSprinting = context.ReadValueAsButton();
        }
    }
}
