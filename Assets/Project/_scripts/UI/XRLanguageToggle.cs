using Reflex.Attributes;
using TMPro;
using UnityEngine;
using Woi.Localization;

public class XRLanguageToggle : MonoBehaviour
{
    [SerializeField] GameObject trLanguageIndicator;
    [SerializeField] GameObject engLanguageIndicator;
    [SerializeField] TextMeshProUGUI trlanguageText;
    [SerializeField] TextMeshProUGUI enlanguageText;
    [SerializeField] Color selectionColor = Color.green;
    [SerializeField] Color defaultColor = Color.white;
    [Inject] IGameManager gameManager;

    void Start()
    {
        SwitchLanguage((int)LanguageManager.CurrentLanguage);    
    }

    public void SwitchLanguage(int language)
    {
         if (language == 0)
        {
            trLanguageIndicator.SetActive(true);
            engLanguageIndicator.SetActive(false);
            trlanguageText.color = selectionColor;
            enlanguageText.color = defaultColor;
            
            gameManager.SetLanguage(Language.Turkish);
        }
        else
        {
            trLanguageIndicator.SetActive(false);
            engLanguageIndicator.SetActive(true);
            trlanguageText.color = defaultColor;
            enlanguageText.color = selectionColor;

            gameManager.SetLanguage(Language.English);
        }   
    }
}
