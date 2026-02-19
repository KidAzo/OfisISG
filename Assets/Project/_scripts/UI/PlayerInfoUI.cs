using UnityEngine;
using TMPro;
using Woi.DataHandler;
using UnityEngine.UI;
using UnityEngine.Events;
using Obvious.Soap;

public class PlayerInfoUI : MonoBehaviour
{
    [SerializeField] private GameObject registrationPanel;

    [Header("UI Referansları")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerIDText;
    [SerializeField] private TextMeshProUGUI identificationText;
    [SerializeField] private Image headerImage;    
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image approvedImage;
    [SerializeField] private Image waitingImage;
    [SerializeField] private Color approvedColor = Color.green;
    [SerializeField] private Color waitingColor = Color.red;
    [SerializeField] private GameObject startGameButton;    
    [SerializeField] private Button startGameUIButton;
    [SerializeField] private UnityEvent onStartGame;
    [SerializeField] private ScriptableEventNoParam onSessionStarted;
    void Start()
    {
        registrationPanel.SetActive(true);
                
        WaitingState();

        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.OnSessionReady += ApprovedState;
        }
    }

    void OnDestroy()
    {
        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.OnSessionReady -= ApprovedState;
        }
    }

    /// <summary>
    /// Paneli gizle
    /// </summary>
    public void HidePanel()
    {
        if (registrationPanel != null)
            registrationPanel.SetActive(false);

        Debug.Log("[PlayerInfoUI] Panel gizlendi");
    }

    void WaitingState()
    {
            backgroundImage.color = waitingColor;

            headerImage.gameObject.SetActive(false);
            approvedImage.gameObject.SetActive(false);
            waitingImage.gameObject.SetActive(true);

            playerNameText.text = "XXXXX";
            playerIDText.text = "Staff ID: XXXXX";
            identificationText.text = "Identification in progress...";
            startGameButton.SetActive(false);
    }

    void ApprovedState(PlayerSession session)
    {
            backgroundImage.color = approvedColor;

            approvedImage.gameObject.SetActive(true);
            waitingImage.gameObject.SetActive(false);

            identificationText.text = "Identification Successful!";

            playerNameText.text = $"{session.PlayerName}";
            playerIDText.text = $"ID: {session.PlayerID}";
            headerImage.gameObject.SetActive(true);
            startGameButton.SetActive(true);
           
            startGameUIButton.onClick.AddListener(() => onStartGame?.Invoke());
            onSessionStarted?.Raise();
    }
}
