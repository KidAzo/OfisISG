using HazardSystem;
using UnityEngine;
using Woi.Events;
using Reflex.Attributes;

namespace Woi.PopUpSystem
{
	public class Notifier : MonoBehaviour
	{
		[Inject] PopupManager popupManager;

		private void OnEnable()
		{
			EventBus.Subscribe<OnHazardFixed>(NotifyOnHazardFixed);
		}

		private void OnDisable()
		{
			EventBus.Unsubscribe<OnHazardFixed>(NotifyOnHazardFixed);
		}

		void NotifyOnHazardFixed(OnHazardFixed evt)
		{
			PopupData data = ScriptableObject.CreateInstance<PopupData>();
			
			data.title = evt.hazardTitle;
			data.isHazard = true;
			data.eventName = nameof(OnHazardFixed);
			data.message = "";
			data.triggerType = PopupTriggerType.OnEvent;
			data.autoClose = true;
			data.displayDuration = 3f;

			popupManager.EnqueuePopup(data);
		}
	}
}

