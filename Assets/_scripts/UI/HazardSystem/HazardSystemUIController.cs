using Cysharp.Threading.Tasks;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Woi.Events;

public class HazardSystemUIController : MonoBehaviour
{
	[Header("Genel Panel")]

	public TextMeshProUGUI titleText;
	public TextMeshProUGUI playerNameText;
	public TextMeshProUGUI dateText;
	public TextMeshProUGUI durationText;
	public TextMeshProUGUI gradeLetterText;
	public TextMeshProUGUI gradeDescText;
	public TextMeshProUGUI totalScoreText;
	public Image totalScoreFillImage;

	[Header("KKD Listeleri")]
	public RectTransform correctEquipmentParent;
	public RectTransform wrongEquipmentParent;
	public RectTransform missingEquipmentParent;

	[Header("Tehlike Listeleri")]
	public RectTransform foundHazardsParent;
	public RectTransform missedHazardsParent;

	[Header("Item Prefabs - KKD")]
	public EquipmentListItemUI correctItemPrefab;  // YEŞİL
	public EquipmentListItemUI wrongItemPrefab;    // KIRMIZI
	public EquipmentListItemUI missingItemPrefab;  // TURUNCU

	[Header("Item Prefabs - Tehlike")]
	public EquipmentListItemUI foundHazardItemPrefab;   // Örn. yeşil/turkuaz
	public EquipmentListItemUI missedHazardItemPrefab;  // Örn. kırmızı

	[Header("Tehlike Sayacı & Progress")]
	public TextMeshProUGUI hazardsFoundText;   
	public TextMeshProUGUI totalHazardsText;  
	public TextMeshProUGUI hazardsFoundPercentText; 
	public Slider hazardsFoundProgressFill;

	[Header("Counters")]
	public TextMeshProUGUI totalFoundText;
	public TextMeshProUGUI totalMissingText;
	public TextMeshProUGUI totalExtraText;

	string activeSceneName;

    void OnEnable()
    {
        EventBus.Subscribe<OnSceneGroupLoaded>(GetActiveSceneName);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<OnSceneGroupLoaded>(GetActiveSceneName);
    }

	void GetActiveSceneName(OnSceneGroupLoaded evt)
	{
		//activeSceneName = evt.sceneName;
	}

    // ------- PUBLIC ENTRY POINT -------
    public void BuildReport(
		string playerName,
		TimeSpan duration,
		ICheckResultProvider hazardResult,
		DateTime reportDate)
	{
		// 1) Genel panel
		titleText.text = $"Tehlike Avı Raporu {activeSceneName}";
		playerNameText.text = playerName;
		dateText.text = reportDate.ToString("dd.MM.yyyy");
		durationText.text = $"{duration.Minutes:00}:{duration.Seconds:00}";

		totalScoreText.text = 0.ToString();

		gradeLetterText.text = "";
		gradeDescText.text = "";

		if (totalScoreFillImage != null)
			totalScoreFillImage.fillAmount = 100 / 100f;

		// 2) Listeleri sıfırla
		ClearChildren(correctEquipmentParent);
		ClearChildren(wrongEquipmentParent);
		ClearChildren(missingEquipmentParent);
		ClearChildren(foundHazardsParent);
		ClearChildren(missedHazardsParent);

		//totalFoundText.text = clothingResult.correct.Count.ToString();
		//totalExtraText.text = clothingResult.extra.Count.ToString();
		//totalMissingText.text = clothingResult.missing.Count.ToString();

		// 6) Tehlikeler: Bulunanlar
		foreach (var hz in hazardResult.foundedChecks)
		{
			CreateItem(foundHazardsParent, foundHazardItemPrefab, hz.TaskName);
		}

		// 7) Tehlikeler: Kaçanlar
		foreach (var hz in hazardResult.missedChecks)
		{
			CreateItem(missedHazardsParent, missedHazardItemPrefab, hz.TaskName);
		}

		HazardPercentage(hazardResult);

		RefreshLayouts().Forget();
	}

	private void HazardPercentage(ICheckResultProvider hazardResult)
	{
		int foundCount = hazardResult.foundedChecks.Count;
		int totalHazards = hazardResult.TotalHazards;

		hazardsFoundText.text = foundCount.ToString();
		totalHazardsText.text = totalHazards.ToString();

		float ratio = totalHazards == 0 ? 0f : (float)foundCount / totalHazards;

		hazardsFoundProgressFill.value = ratio;

		int percent = Mathf.RoundToInt(ratio * 100f);
		hazardsFoundPercentText.text = percent.ToString();
	}

	// ------- HELPERS -------
	void ClearChildren(RectTransform parent)
	{
		if (parent == null) return;

		for (int i = parent.childCount - 1; i >= 0; i--)
		{
			Destroy(parent.GetChild(i).gameObject);
		}
	}

	void CreateItem(RectTransform parent, EquipmentListItemUI prefab, string text, Sprite icon = null)
	{
		if (parent == null || prefab == null) return;

		var item = Instantiate(prefab, parent);
		item.Init(text, icon);
	}

	async UniTaskVoid RefreshLayouts()
	{
		await UniTask.Yield();
		Canvas.ForceUpdateCanvases(); 

		Rebuild(correctEquipmentParent);
		Rebuild(wrongEquipmentParent);
		Rebuild(missingEquipmentParent);
		Rebuild(foundHazardsParent);
		Rebuild(missedHazardsParent);
	}

	void Rebuild(RectTransform parent)
	{
		LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
	}
}

