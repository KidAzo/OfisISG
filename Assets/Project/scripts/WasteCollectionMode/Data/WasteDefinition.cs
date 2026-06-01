using UnityEngine;
using Woi.WasteCollectionMode;

[CreateAssetMenu(fileName = "WasteDefinition", menuName = "Waste Collection/Waste Definition")]
public class WasteDefinition : ScriptableObject
{
    public Waste waste;

    [Tooltip("TR/EN sound played when this waste is selected.")]
    [SerializeField] private LocalizedWasteSound selectSound;

    [Header("Explanation (after bin selection)")]
    [Tooltip("TR/EN explanation voice played after the correct/wrong sound. The popup stays open until it finishes.")]
    [SerializeField] private LocalizedWasteSound explanationSound;

    [TextArea(2, 5)]
    [Tooltip("Turkish explanation text shown in the popup.")]
    [SerializeField] private string explanationTurkish;

    [TextArea(2, 5)]
    [Tooltip("English explanation text shown in the popup.")]
    [SerializeField] private string explanationEnglish;

    public string Name => waste != null ? waste.name : string.Empty;

    public WasteType Type => waste != null ? waste.type : default;

    public LocalizedWasteSound SelectSound => selectSound;

    public LocalizedWasteSound ExplanationSound => explanationSound;

    public string ExplanationText =>
        WasteCollectionLocalization.IsEnglish
            ? (string.IsNullOrWhiteSpace(explanationEnglish) ? explanationTurkish : explanationEnglish)
            : explanationTurkish;

    public bool HasExplanation =>
        explanationSound != null
        || !string.IsNullOrWhiteSpace(explanationTurkish)
        || !string.IsNullOrWhiteSpace(explanationEnglish);
}
