using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using Woi.Localization;
using Woi.Porting;

public class ExitPanelController : MonoBehaviour
{
    [SerializeField] GameObject trPanel;
    [SerializeField] GameObject enPanel;
    [SerializeField] GameObject exitPanel;  
    [SerializeField] ScriptableEventNoParam preOnGameFinishEvent;  
    [SerializeField] ScriptableEventNoParam onGameFinishEvent;  

    void Start()
    {
       exitPanel.SetActive(false); 
    }

    void OnEnable()
    {
        preOnGameFinishEvent.OnRaised += Show;
    }

    void OnDisable()
    {
        preOnGameFinishEvent.OnRaised -= Show;
    }

    public void Show()
    {
        exitPanel.SetActive(exitPanel.activeSelf ? false : true);
       
        if(LanguageManager.CurrentLanguage == Language.Turkish)
        {
            trPanel.SetActive(trPanel.activeSelf ? false : true);
        }
        else if(LanguageManager.CurrentLanguage == Language.English)
        {
            enPanel.SetActive(enPanel.activeSelf ? false : true);
        }
    }

    public void Hide()
    {
        exitPanel.SetActive(false);
        trPanel.SetActive(false);
        enPanel.SetActive(false);
    }

    public void Raise()
    {
        onGameFinishEvent.Raise();

        exitPanel.SetActive(false);
    }
}
