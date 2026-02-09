using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Woi.Events;
using Woi.HazardSystem;

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

	[Header("Tehlike Listeleri")]
	public RectTransform missedContainer;
	public RectTransform foundedContainer;
	public RectTransform foundedHazardsParent;
	public RectTransform missedHazardsParent;
	public ScrollRect scrollRect;


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
		//EventBus.Subscribe<OnSceneGroupLoaded>(GetActiveSceneName);
	}

	void OnDisable()
	{
		//EventBus.Unsubscribe<OnSceneGroupLoaded>(GetActiveSceneName);
	}

	void GetActiveSceneName(OnSceneGroupLoaded evt)
	{
		//activeSceneName = evt.sceneName;
	}

	[Button]
	public void BuildReport(
		string playerName,
		TimeSpan duration,
		ICheckResultProvider hazardResult,
		DateTime reportDate)
	{
		titleText.text = $"Ofis Tehlike Avı Raporu";
		playerNameText.text = playerName;
		dateText.text = reportDate.ToString("dd.MM.yyyy");
		durationText.text = $"{duration.Minutes:00}:{duration.Seconds:00}";

		totalScoreText.text = hazardResult.Score.ToString();

		HazardScoreCalculator.GetGrade(0, out string letter, out string description);

		gradeLetterText.text = letter;
		gradeDescText.text = description;

		ClearChildren(foundedHazardsParent);
		ClearChildren(missedHazardsParent);

		foreach (var hz in hazardResult.foundedChecks)
		{
			CreateItem(foundedHazardsParent, foundHazardItemPrefab, hz.TaskName);
		}

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
		percent = Mathf.Max(percent, 0);
		hazardsFoundPercentText.text = $"%{percent}";

		if (totalScoreFillImage != null)
			totalScoreFillImage.fillAmount = ratio;
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


	async UniTask RefreshLayouts()
	{
		await UniTask.NextFrame();

		foundedHazardsParent.gameObject.SetActive(true);
		missedHazardsParent.gameObject.SetActive(true);	
		// Grid reset (çok kritik)
		ToggleGrid(foundedHazardsParent, false);
		ToggleGrid(missedHazardsParent, false);

		await UniTask.NextFrame();

		ToggleGrid(foundedHazardsParent, true);
		ToggleGrid(missedHazardsParent, true);

		// Ölçülerin oturması için 2 adım
		await UniTask.WaitForEndOfFrame();
		await UniTask.NextFrame();

		Canvas.ForceUpdateCanvases();

		// Rebuild (ikisi de)
		LayoutRebuilder.ForceRebuildLayoutImmediate(foundedHazardsParent);
		LayoutRebuilder.ForceRebuildLayoutImmediate(missedHazardsParent);

		Canvas.ForceUpdateCanvases();

		await UniTask.NextFrame();
		missedContainer.gameObject.SetActive(false);	
	}

	static void ToggleGrid(RectTransform rt, bool enabled)
	{
		if (!rt) return;
		var grid = rt.GetComponent<GridLayoutGroup>();
		if (grid) grid.enabled = enabled;
	}
}

