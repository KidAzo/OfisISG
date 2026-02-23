using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using Woi.Localization;
using Woi.Porting;

public class ExitPanelController : MonoBehaviour
{
    [SerializeField] GameObject trPanel;
    [SerializeField] GameObject enPanel;
    [SerializeField] ScriptableEventNoParam preOnGameFinishEvent;  
    [SerializeField] ScriptableEventNoParam onGameFinishEvent;  

    void Start()
    {
       trPanel.SetActive(false);
       enPanel.SetActive(false);
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
        if(LanguageManager.CurrentLanguage == Language.Turkish)
        {
            trPanel.SetActive(trPanel.activeSelf ? false : true);
        }
        else if(LanguageManager.CurrentLanguage == Language.English)
        {
            enPanel.SetActive(enPanel.activeSelf ? false : true);
        }
    }

    public void Raise()
    {
        trPanel.SetActive(false);
        enPanel.SetActive(false);

        onGameFinishEvent.Raise();
    }
}
