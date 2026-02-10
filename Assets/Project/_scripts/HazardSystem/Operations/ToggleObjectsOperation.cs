using UnityEngine;
using UnityEngine.Events;
using PrimeTween;

namespace Woi.HazardSystem
{
	public class ToggleObjectsOperation : IHazardOperation
	{
		private readonly GameObject[] _toEnable;
		private readonly GameObject[] _toDisable;
		private readonly bool _isTweenRequested;

		public ToggleObjectsOperation(GameObject[] toEnable, GameObject[] toDisable, bool isTweenRequested = true)
		{
			_toEnable = toEnable;
			_toDisable = toDisable;
			_isTweenRequested = isTweenRequested;
		}

		public void Execute()
		{
			HazardFeedback.SetScale(_toDisable, 0f, isTweenRequested: _isTweenRequested, Ease.OutElastic, () =>
			{
				SetActiveStates(_toDisable, false);
			});

			HazardFeedback.SetScale(_toEnable, 0f, isTweenRequested: false, Ease.OutBack, () =>
			{
				SetActiveStates(_toEnable, true);
			});

			HazardFeedback.SetScale(_toEnable, 1f, isTweenRequested: _isTweenRequested, Ease.InOutBack, () => {});
		}

		void SetActiveStates(GameObject[] go, bool active)
		{
			foreach (var obj in go)
				if (obj != null) obj.SetActive(active);
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