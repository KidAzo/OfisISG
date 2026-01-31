using UnityEngine;
using UnityEngine.Events;
using PrimeTween;

namespace Woi.HazardSystem
{
	public class ToggleObjectsOperation : IHazardOperation
	{
		private readonly GameObject[] _toEnable;
		private readonly GameObject[] _toDisable;

		public ToggleObjectsOperation(GameObject[] toEnable, GameObject[] toDisable)
		{
			_toEnable = toEnable;
			_toDisable = toDisable;
		}

		public void Execute()
		{
			if (_toEnable != null)
			{
				foreach (var go in _toEnable)
					if (go != null) go.SetActive(true);
			}

			if (_toDisable != null)
			{
				foreach (var go in _toDisable)
					if (go != null) go.SetActive(false);
			}
		}
	}

    public class RotationObjectsOperation : IHazardOperation
    {
		private readonly GameObject[] _rotationObjects;
		private readonly Vector3 _targetRotation;
		private readonly float _duration = 2.0f;

		public RotationObjectsOperation(GameObject[] rotationObjects, Vector3 targetRotation, float duration = 2.0f)
		{
			_rotationObjects = rotationObjects;
			_targetRotation = targetRotation;
			_duration = duration;
		}

        public void Execute()
        {
            if (_rotationObjects != null)
			{
				foreach (var go in _rotationObjects)
					Tween.Rotation(go.transform, _targetRotation, duration: _duration, Ease.InSine);
			}
        }
    }

    public class EventOperation : IHazardOperation
	{
		private readonly UnityEvent _event;

		public EventOperation(UnityEvent evt)
		{
			_event = evt;
		}

		public void Execute()
		{
			_event?.Invoke();
		}
	}
}