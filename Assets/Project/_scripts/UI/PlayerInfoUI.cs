using UnityEngine;
using TMPro;
using Woi.DataHandler;

public class PlayerInfoUI : MonoBehaviour
{
    [Header("UI Referansları")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerIDText;
    [SerializeField] private GameObject infoPanel;
    
    [Header("Ayarlar")]
    [SerializeField] private float displayDuration = 5f; // Kaç saniye göster
    [SerializeField] private bool hideAfterDelay = true; // Otomatik gizlensin mi

    void Start()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.OnSessionReady += OnPlayerRegistered;
        }
        else
        {
            Debug.LogError("[PlayerInfoUI] SessionManager bulunamadı!");
        }
    }

    void OnDestroy()
    {
        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.OnSessionReady -= OnPlayerRegistered;
        }
    }

    private void OnPlayerRegistered(PlayerSession session)
    {
        Debug.Log($"[PlayerInfoUI] Oyuncu bilgileri gösteriliyor: {session.FullName}");

        // Paneli göster
        if (infoPanel != null)
            infoPanel.SetActive(true);

        // İsim ve soyadı göster
        if (playerNameText != null)
        {
            playerNameText.text = $"{session.PlayerName}";
        }

        // ID'yi göster
        if (playerIDText != null)
        {
            playerIDText.text = $"ID: {session.PlayerID}";
        }

        // Belirlenen süre sonra gizle
        if (hideAfterDelay)
        {
            Invoke(nameof(HidePanel), displayDuration);
        }
    }

    /// <summary>
    /// Paneli gizle
    /// </summary>
    public void HidePanel()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        Debug.Log("[PlayerInfoUI] Panel gizlendi");
    }

    /// <summary>
    /// Manuel olarak göster (isteğe bağlı)
    /// </summary>
    [ContextMenu("Test: Paneli Göster")]
    public void ShowCurrentPlayer()
    {
        if (SessionManager.Instance.CurrentSession != null)
        {
            OnPlayerRegistered(SessionManager.Instance.CurrentSession);
        }
        else
        {
            Debug.LogWarning("Henüz oyuncu kaydı yok!");
        }
    }
}
