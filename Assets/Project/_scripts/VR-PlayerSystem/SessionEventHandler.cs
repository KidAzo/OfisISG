using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Woi.DataHandler;

/// <summary>
/// UI mesajlarını yöneten script
/// SessionManager'dan gelen event'leri dinler ve ekranda gösterir
/// </summary>
public class SessionEventHandler : MonoBehaviour
{
    [Header("━━━━━━━ UI REFERANSLARI ━━━━━━━")]
    [Tooltip("Ana mesaj metni (örn: 'Oyuncu bekleniyor...')")]
    [SerializeField] private TextMeshProUGUI messageText;
    
    [Tooltip("Oyuncu adı metni (örn: 'Ahmet YILMAZ')")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    
    [Tooltip("Tüm panel (göster/gizle için)")]
    [SerializeField] private GameObject welcomePanel;
    
    [Header("━━━━━━━ MESAJ AYARLARI ━━━━━━━")]
    [SerializeField] private string waitingMessage = "Oyuncu kaydı bekleniyor...";
    [SerializeField] private string welcomeMessage = "Hoş Geldin";
    [SerializeField] private float welcomeDisplayTime = 3f; // Kaç saniye göster
    
    private bool isPlayerRegistered = false;

    // ========================================
    // UNITY LIFECYCLE
    // ========================================

    void Start()
    {
        // Event'leri dinlemeye başla
        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.OnSessionReady += OnPlayerRegistered;
            SessionManager.Instance.OnError += OnSessionError;
        }
        else
        {
            Debug.LogError("[UIController] SessionManager bulunamadı!");
        }

        ShowWaitingScreen();
    }

    void OnDestroy()
    {
        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.OnSessionReady -= OnPlayerRegistered;
            SessionManager.Instance.OnError -= OnSessionError;
        }
    }

    private void OnPlayerRegistered(PlayerSession session)
    {
        Debug.Log($"[UIController] Oyuncu kaydedildi: {session.FullName}");
        
        isPlayerRegistered = true;

        // Hoş geldin ekranını göster
        ShowWelcomeScreen(session);

        // Ses çal (varsa)
        PlayWelcomeSound();

        // X saniye sonra oyunu başlat
        StartCoroutine(StartGameAfterDelay(welcomeDisplayTime));
    }

    /// <summary>
    /// Hata olduğunda çağrılır
    /// </summary>
    private void OnSessionError(string error)
    {
        Debug.LogError($"[UIController] Session hatası: {error}");
        ShowErrorScreen(error);
    }

    // ========================================
    // EKRAN GÖSTERME METODLARI
    // ========================================

    /// <summary>
    /// Bekleme ekranını göster
    /// </summary>
    private void ShowWaitingScreen()
    {
        if (welcomePanel != null)
            welcomePanel.SetActive(true);

        if (messageText != null)
        {
            messageText.text = waitingMessage;
            messageText.color = Color.white;
        }

        if (playerNameText != null)
        {
            playerNameText.text = "";
            playerNameText.gameObject.SetActive(false);
        }

        Debug.Log("[UIController] Bekleme ekranı gösteriliyor");
    }

    /// <summary>
    /// Hoş geldin ekranını göster
    /// </summary>
    private void ShowWelcomeScreen(PlayerSession session)
    {
        if (welcomePanel != null)
            welcomePanel.SetActive(true);

        if (messageText != null)
        {
            messageText.text = welcomeMessage;
            messageText.color = new Color(0.3f, 0.8f, 0.3f); // Yeşil
        }

        if (playerNameText != null)
        {
            // Ad ve soyadı BÜYÜK harfle göster
            playerNameText.text = session.FullName.ToUpper();
            playerNameText.gameObject.SetActive(true);
            
            // Animasyon efekti (opsiyonel)
            StartCoroutine(AnimatePlayerName());
        }

        Debug.Log($"[UIController] Hoş geldin ekranı gösteriliyor: {session.FullName}");
    }

    /// <summary>
    /// Hata ekranını göster
    /// </summary>
    private void ShowErrorScreen(string error)
    {
        if (welcomePanel != null)
            welcomePanel.SetActive(true);

        if (messageText != null)
        {
            messageText.text = "⚠️ HATA";
            messageText.color = Color.red;
        }

        if (playerNameText != null)
        {
            playerNameText.text = error;
            playerNameText.color = Color.red;
            playerNameText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Ekranı gizle (oyun başlarken)
    /// </summary>
    public void HideScreen()
    {
        if (welcomePanel != null)
            welcomePanel.SetActive(false);

        Debug.Log("[UIController] Ekran gizlendi, oyun başlıyor");
    }

    // ========================================
    // OYUN BAŞLATMA
    // ========================================

    /// <summary>
    /// X saniye sonra oyunu başlat
    /// </summary>
    private IEnumerator StartGameAfterDelay(float delay)
    {
        // Geri sayım göster (opsiyonel)
        for (int i = (int)delay; i > 0; i--)
        {
            if (playerNameText != null)
            {
                playerNameText.text += $"\n\nOyun {i} saniye içinde başlıyor...";
            }
            yield return new WaitForSeconds(1f);
        }

        // Ekranı gizle
        HideScreen();

        // Oyunu başlat
        StartGame();
    }

    /// <summary>
    /// Oyunu başlatan metod
    /// BURAYA KENDİ OYUN BAŞLATMA KODUNUZU EKLEYIN!
    /// </summary>
    private void StartGame()
    {
        Debug.Log("🎮 OYUN BAŞLIYOR!");

        // Örnek: Oyun manager'ınızı çağırın
        // GameManager.Instance.StartGame();
        
        // Örnek: Scene değiştirin
        // SceneManager.LoadScene("GameScene");
        
        // Örnek: Oyuncu kontrollerini aktif edin
        // playerController.enabled = true;

        // ŞİMDİLİK: Sadece log
        Debug.Log($"Oyuncu: {SessionManager.Instance.CurrentSession.FullName}");
        Debug.Log($"ID: {SessionManager.Instance.CurrentSession.PlayerID}");
        Debug.Log($"Başlangıç: {SessionManager.Instance.CurrentSession.StartTime}");
    }

    // ========================================
    // ANIMASYON VE SES
    // ========================================

    /// <summary>
    /// İsim animasyonu (büyüyerek belir)
    /// </summary>
    private IEnumerator AnimatePlayerName()
    {
        if (playerNameText == null) yield break;

        float duration = 0.5f;
        float elapsed = 0f;

        playerNameText.transform.localScale = Vector3.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float scale = Mathf.Lerp(0f, 1f, elapsed / duration);
            playerNameText.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        playerNameText.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Hoş geldin sesi çal
    /// </summary>
    private void PlayWelcomeSound()
    {
    
    }

    // ========================================
    // INSPECTOR'DAN ÇAĞRILACAK TEST METODLARI
    // ========================================

    [ContextMenu("Test: Bekleme Ekranı")]
    private void TestWaitingScreen()
    {
        ShowWaitingScreen();
    }

    [ContextMenu("Test: Hoş Geldin Ekranı")]
    private void TestWelcomeScreen()
    {
        PlayerSession testSession = new PlayerSession
        {
            PlayerName = "Ahmet",
            PlayerID = 12345
        };
        ShowWelcomeScreen(testSession);
    }

    [ContextMenu("Test: Hata Ekranı")]
    private void TestErrorScreen()
    {
        ShowErrorScreen("Bağlantı hatası!");
    }
}