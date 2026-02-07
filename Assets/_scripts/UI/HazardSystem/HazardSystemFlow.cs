using HazardSystem;
using System;
using UnityEngine;
using UnityEngine.Events;
using Woi.Events;
using WoiUtils.AudioSystem;

public class HazardSystemFlow : MonoBehaviour
{
// //	[Inject] SoundManager soundManager;
// 	public HazardSystemUIController reportUI;
// 	//public RequiredClothingSet requiredClothingSet;
// 	//public ClothingValidator clothingValidator;
// 	public LevelTimer levelTimer;
// 	public UnityEvent eventUnity;

// 	private void OnEnable()
// 	{
// 		EventBus.Subscribe<OnHazardModeFinished>(EndHazardHunt);	
// 	}

// 	private void OnDisable()
// 	{
// 		EventBus.Unsubscribe<OnHazardModeFinished>(EndHazardHunt);
// 	}

// 	public void EndHazardHunt(OnHazardModeFinished evt)
// 	{
// 		soundManager.StopAll();
// 		EventBus.Publish(new OnHazardResult(true));

// 		// 1) Clothing karşılaştırması
// 		var clothingEquipped = ClothingRuntimeState.Equipped;
// 		// 2) Tehlike result’unu sen kendi sisteminden dolduracaksın
// 		var hazardResult = HazardManager.Instance.BuildHazardCheckResult();
// 		// Örnek:
// 		// hazardResult.foundHazards.Add("Reflektörlü Yelek Yok");
// 		// hazardResult.missedHazards.Add("Kırık Merdiven Korkuluğu");

// 		Debug.Log($"Hazard Result - Found: {hazardResult.foundedChecks.Count}, Missed: {hazardResult.missedChecks.Count}");

// 		// 3) Süre / oyuncu bilgisi
// 		string playerName = "XXXX"; // PlayFab vs'den de gelebilir
// 		TimeSpan duration = levelTimer.GetDuration();
// 		DateTime reportDate = DateTime.Now;

// 		reportUI.gameObject.SetActive(true);
		
// 		var clothingResult = clothingValidator.LastResult;

// 		// 4) UI’yi kur
// 		reportUI.BuildReport(playerName, duration, clothingResult, hazardResult, reportDate);
// 		eventUnity?.Invoke();	
// 	}
}

