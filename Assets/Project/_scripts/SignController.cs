using Reflex.Attributes;
using UnityEngine;
using Woi.Localization;
using Woi.Porting;

public class SignController : MonoBehaviour
{
    [Inject] private PortingController portingController;
    [SerializeField] private GameObject trSigns;
    [SerializeField] private GameObject engSigns;



    void Start()
    {
        if(LanguageManager.CurrentLanguage == Language.English)
        {
            trSigns.SetActive(false);
            engSigns.SetActive(true);
        }
         else
        {
            trSigns.SetActive(true);
            engSigns.SetActive(false);
        }
    }
}

