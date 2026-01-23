using UnityEngine;
using UnityEngine.Events;

namespace HazardSystem
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