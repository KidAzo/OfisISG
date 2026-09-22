using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Woi.Events;
using Woi.HazardSystem;
using Woi.Localization;

public class HazardSystemUIController : MonoBehaviour
{
	[Header("Genel Panel")]

	public TextMeshProUGUI titleText;
	public TextMeshProUGUI playerNameText;
	public TextMeshProUGUI playerNamePrefixText;
	public TextMeshProUGUI dateText;
	public TextMeshProUGUI datePrefixText;
	[FormerlySerializedAs("durationText")]
	public TextMeshProUGUI durationTextEng;
	public GameObject durationTextEngObj;
	public TextMeshProUGUI durationTextTr;
	public GameObject durationTextTrObj;
	public TextMeshProUGUI durationPrefixText;
	public TextMeshProUGUI gradeLetterText;
	public TextMeshProUGUI gradeDescText;
	public TextMeshProUGUI totalScoreText;
	public TextMeshProUGUI generalPerformanceText;
	public TextMeshProUGUI totalScoreTextPrefix;
	public TextMeshProUGUI progressText;

	[Header("Middle Tabs")]

	public TextMeshProUGUI middleTitle;
	public TextMeshProUGUI middleSubTitle;
	public TextMeshProUGUI foundedButtonTextC;
	public TextMeshProUGUI foundedButtonText;
	public TextMeshProUGUI missedButtonTextC;
	public TextMeshProUGUI missedButtonText;
	

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
	const string LoginSceneName = "LoginScreen";
	bool _returningToLogin;

	void OnEnable()
	{
		//EventBus.Subscribe<OnSceneGroupLoaded>(GetActiveSceneName);
		WireReturnButtons();
	}

	void OnDisable()
	{
		//EventBus.Unsubscribe<OnSceneGroupLoaded>(GetActiveSceneName);
	}

	void GetActiveSceneName(OnSceneGroupLoaded evt)
	{
		activeSceneName = SceneManager.GetActiveScene().name;
	}

	void WireReturnButtons()
	{
		bool found = false;
		var transforms = GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < transforms.Length; i++)
		{
			if (!IsExistingReturnVisual(transforms[i]))
				continue;

			BindReturnButton(transforms[i].gameObject);
			found = true;
		}

		if (!found)
			CreateReturnButton();
	}

	static bool IsExistingReturnVisual(Transform t)
	{
		if (t == null)
			return false;
		if (t.name == "Quit")
			return true;

		var image = t.GetComponent<Image>();
		var rt = t as RectTransform;
		if (image == null || rt == null)
			return false;
		if (rt.anchorMin.x < 0.95f || rt.anchorMin.y < 0.95f)
			return false;
		if (Mathf.Abs(rt.sizeDelta.x - 64f) > 8f || Mathf.Abs(rt.sizeDelta.y - 64f) > 8f)
			return false;

		var color = image.color;
		return color.r > 0.85f && color.g < 0.5f && color.b < 0.65f && color.a > 0.5f;
	}

	void BindReturnButton(GameObject go)
	{
		var button = go.GetComponent<Button>();
		if (button == null)
			button = go.AddComponent<Button>();

		button.interactable = true;
		button.onClick.RemoveListener(ReturnToLoginScreen);
		button.onClick.AddListener(ReturnToLoginScreen);
	}

	void CreateReturnButton()
	{
		var canvas = GetComponentInChildren<Canvas>(true);
		if (canvas == null)
			return;

		var go = new GameObject("Quit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
		go.transform.SetParent(canvas.transform, false);

		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(1f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = new Vector2(-72f, -50f);
		rt.sizeDelta = new Vector2(64f, 64f);

		var image = go.GetComponent<Image>();
		image.color = new Color(1f, 0.39215684f, 0.55838746f, 1f);
		image.raycastTarget = true;

		BindReturnButton(go);
	}

	public void ReturnToLoginScreen()
	{
		if (_returningToLogin)
			return;

		_returningToLogin = true;
		SceneManager.LoadSceneAsync(LoginSceneName, LoadSceneMode.Single);
	}

	[Button]
	public void BuildReport(
		string playerName,
		TimeSpan duration,
		ICheckResultProvider hazardResult,
		DateTime reportDate)
	{
		missedButtonText.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Gozden Kacanlar" : "Missed Hazards";
		missedButtonTextC.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Gozden Kacanlar" : "Missed Hazards";
		foundedButtonText.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Bulunanlar" : "Founded Hazards";
		foundedButtonTextC.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Bulunanlar" : "Founded Hazards";
		middleTitle.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Ofis Tehlike Raporu" : "Office Hazard Report";
		middleSubTitle.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Tespit edilen riskler" : "Identified Risks";
		
		titleText.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Ofis Tehlike Avı Raporu" : "Office Hazard Hunt Report";
		generalPerformanceText.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Genel Performans" : "General Performance";
		totalScoreTextPrefix.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Toplam Puan:" : "Total Score:";	
		progressText.text = LanguageManager.CurrentLanguage == Language.Turkish ? "İlerleme" : "Progress";
		
		playerNamePrefixText.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Oyuncu:" : "Player:";	
		playerNameText.text = playerName;

		datePrefixText.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Tarih:" : "Date:";
		dateText.text = reportDate.ToString("dd.MM.yyyy");

		if (durationTextEngObj != null)
			durationTextEngObj.SetActive(LanguageManager.CurrentLanguage != Language.Turkish);
		if (durationTextTrObj != null)
			durationTextTrObj.SetActive(LanguageManager.CurrentLanguage == Language.Turkish);
		
		if (durationPrefixText != null)
			durationPrefixText.text = LanguageManager.CurrentLanguage == Language.Turkish ? "Süre:" : "Duration:";
		string durationValue = $"{duration.Minutes:00}:{duration.Seconds:00}";
		if (durationTextEng != null)
			durationTextEng.text = durationValue;
		if (durationTextTr != null)
			durationTextTr.text = durationValue;

		if (totalScoreText != null)
			totalScoreText.text = hazardResult.Score.ToString();

		HazardScoreCalculator.GetGrade(hazardResult.Score, LanguageManager.CurrentLanguage, out string letter, out string description);

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

		if (hazardsFoundProgressFill != null)
			hazardsFoundProgressFill.value = ratio;

		int percent = Mathf.RoundToInt(ratio * 100f);
		percent = Mathf.Max(percent, 0);
		if (hazardsFoundPercentText != null)
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

