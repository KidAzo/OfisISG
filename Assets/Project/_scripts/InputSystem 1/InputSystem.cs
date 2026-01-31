// using System;
// using UnityEngine;
// using UnityEngine.InputSystem;

// public class InputSystem : MonoBehaviour
// {
// 	//private PlayerInputActions actions;

// 	public event Action OnEscapeEvent;
// 	public event Action OnInteractionEvent;
// 	public event Action<int> OnNumericKeyPressed;
// 	public event Action OnHazardFixedKeyPressed;
// 	public event Action OnGetHazardResultPressed;

// 	private void Awake()
// 	{
// 		//actions = new PlayerInputActions();
// 	}

// 	private void OnEnable()
// 	{
// 		actions.UI.Escape.performed += OnEscapePerformed;

// 		actions.Player.Interactions.performed += OnInteractionPerformed;

// 		actions.Player.Numerics.performed += OnNumericsPerformed;

// 		//actions.Player.HazardFixed.performed += OnHazardFixedPerformed;
// 		//actions.Player.GetHazardResult.performed += OnGetHazardResultPerformed;

// 		actions.Enable();
// 	}

// 	private void OnDisable()
// 	{
// 		actions.UI.Escape.performed -= OnEscapePerformed;
// 		actions.Player.Interactions.performed -= OnInteractionPerformed;
// 		actions.Player.Numerics.performed -= OnNumericsPerformed;
		
// 		//actions.Player.HazardFixed.performed -= OnHazardFixedPerformed;
// 		//actions.Player.GetHazardResult.performed -= OnGetHazardResultPerformed;

// 		actions.Disable();
// 	}

// 	private void OnEscapePerformed(InputAction.CallbackContext ctx)
// 	{
// 		OnEscapeEvent?.Invoke();
// 	}

// 	private void OnInteractionPerformed(InputAction.CallbackContext ctx)
// 	{
// 		OnInteractionEvent?.Invoke();
// 	}

// 	private void OnNumericsPerformed(InputAction.CallbackContext ctx)
// 	{
// 		int bindingIndex = ctx.action.GetBindingIndexForControl(ctx.control);
// 		int number = bindingIndex + 1;
// 		OnNumericKeyPressed?.Invoke(number);
// 	}

// 	private void OnHazardFixedPerformed(InputAction.CallbackContext ctx)
// 	{
// 		OnHazardFixedKeyPressed?.Invoke();
// 	}

// 	private void OnGetHazardResultPerformed(InputAction.CallbackContext ctx)
// 	{
// 		OnGetHazardResultPressed?.Invoke();
// 	}
// }
