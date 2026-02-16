using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

namespace Woi.DataHandler
{
    /// <summary>
    /// VR Oyuncu Sistemi - Ana Yönetici
    /// 
    /// GÖREVLER:
    /// 1. UDP ile tablet'ten gelen oyuncu bilgilerini dinler
    /// 2. Gelen veriyi parse eder (Ad|Soyad|ID formatında)
    /// 3. PlayerSession oluşturur
    /// 4. Oyun bitince CSV'yi PC'ye HTTP POST ile gönderir
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        // Singleton pattern (sadece 1 instance olmalı)
        private static SessionManager _instance;
        public static SessionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("SessionManager");
                    _instance = go.AddComponent<SessionManager>();
                }
                return _instance;
            }
        }

        [Header("━━━━━━━ NETWORK AYARLARI ━━━━━━━")]
        [Tooltip("UDP dinleme portu (Tablet buraya gönderir)")]
        [SerializeField] private int udpListenPort = 7777;
        
        [Tooltip("PC sunucusunun adresi (CSV buraya gönderilir)")]
        [SerializeField] private string pcServerUrl = "http://192.168.1.50:8080";
        
        [Header("━━━━━━━ DEBUG AYARLARI ━━━━━━━")]
        [Tooltip("Console'a log yazsın mı?")]
        [SerializeField] private bool showDebugLogs = true;

        // Şu anki oyuncu oturumu
        public PlayerSession CurrentSession { get; private set; }
        
        // UDP dinleyici
        private UdpClient udpListener;
        private bool isListening = false;

        // Events (Diğer scriptler bunları dinleyebilir)
        public event Action<PlayerSession> OnSessionReady;  // Oyuncu kaydı geldiğinde
        public event Action<string> OnError;                 // Hata olduğunda

        // ========================================
        // UNITY LIFECYCLE METODLARI
        // ========================================

        void Awake()
        {
            // Singleton kontrolü
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject); // Scene değişse bile yok olma
                StartListening();
            }
            else if (_instance != this)
            {
                Destroy(gameObject); // İkinci instance oluşturulmaya çalışıldı, yok et
            }
        }

        void OnDestroy()
        {
            StopListening();
        }

        void OnApplicationQuit()
        {
            StopListening();
        }

        // ========================================
        // UDP DİNLEME SİSTEMİ
        // ========================================

        private void StartListening()
        {
            try
            {
                udpListener = new UdpClient(udpListenPort);
                udpListener.BeginReceive(OnDataReceived, null);
                isListening = true;
                
                Log($"✅ UDP Dinleme BAŞLATILDI - Port: {udpListenPort}");
                Log($"📡 Tablet'ten gelen oyuncu kayıtları bekleniyor...");
            }
            catch (Exception e)
            {
                LogError($"❌ UDP başlatma hatası: {e.Message}");
                OnError?.Invoke($"UDP başlatılamadı: {e.Message}");
            }
        }

        private void StopListening()
        {
            isListening = false;
            
            if (udpListener != null)
            {
                try
                {
                    udpListener.Close();
                    Log("🛑 UDP dinleme durduruldu");
                }
                catch { }
            }
        }

        // UDP'den veri geldiğinde çağrılır (FARKLI THREAD'DE ÇALIŞIR!)
        private void OnDataReceived(IAsyncResult result)
        {
            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpListener.EndReceive(result, ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);

                Log($"📩 Veri alındı: {message} (Gönderen: {remoteEndPoint.Address})");

                // Veriyi parse et: "Ad|Soyad|ID" formatında
                string[] parts = message.Split('|');
                
                if (parts.Length == 3)
                {
                    try
                    {
                        // Session oluştur
                        PlayerSession newSession = new PlayerSession
                        {
                            PlayerName = parts[0].Trim(),
                            PlayerID = int.Parse(parts[1].Trim()),
                            StartTime = DateTime.Now,
                            IsActive = true
                        };

                        Log($"✅ Session oluşturuldu: {newSession}");
                        
                        // Main thread'de event tetikle
                        // (Unity API'leri sadece main thread'de çalışır)
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            CurrentSession = newSession;
                            OnSessionReady?.Invoke(CurrentSession);
                        });
                    }
                    catch (Exception parseEx)
                    {
                        LogError($"❌ Veri parse hatası: {parseEx.Message}");
                    }
                }
                else
                {
                    LogError($"❌ Hatalı veri formatı! Beklenen: Ad|Soyad|ID, Gelen: {message}");
                }

                // Tekrar dinlemeye başla
                if (isListening)
                {
                    udpListener.BeginReceive(OnDataReceived, null);
                }
            }
            catch (ObjectDisposedException)
            {
                // UDP listener kapanmış, normal
            }
            catch (Exception e)
            {
                LogError($"❌ Veri alma hatası: {e.Message}");
                
                // Tekrar dinlemeye çalış
                if (isListening)
                {
                    try
                    {
                        udpListener.BeginReceive(OnDataReceived, null);
                    }
                    catch { }
                }
            }
        }

        // ========================================
        // CSV GÖNDERME SİSTEMİ
        // ========================================

        /// <summary>
        /// Oyun bitince CSV satırını PC'ye gönder
        /// </summary>
        /// <param name="csvLine">CSV formatında tek satır (noktalı virgülle ayrılmış)</param>
        public void SendResultToPC(string csvLine)
        {
            if (string.IsNullOrEmpty(csvLine))
            {
                LogError("❌ CSV satırı boş! Gönderilmedi.");
                return;
            }

            StartCoroutine(SendResultCoroutine(csvLine));
        }

        private IEnumerator SendResultCoroutine(string csvLine)
        {
            string url = $"{pcServerUrl}/save-result";
            
            Log($"📤 Sonuç gönderiliyor: {url}");
            Log($"📊 Veri: {csvLine.Substring(0, Math.Min(100, csvLine.Length))}...");

            // UnityWebRequest ile HTTP POST gönder
            using (UnityWebRequest request = UnityWebRequest.Post(url, csvLine, "text/plain"))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Log("✅ Sonuç PC'ye başarıyla kaydedildi!");
                }
                else
                {
                    LogError($"❌ Sonuç gönderme hatası: {request.error}");
                    LogError($"   URL: {url}");
                    LogError($"   PC sunucusu çalışıyor mu? IP adresi doğru mu?");
                    OnError?.Invoke($"CSV gönderilemedi: {request.error}");
                }
            }
        }

        // ========================================
        // YARDIMCI METODLAR
        // ========================================

        /// <summary>
        /// Session'ı sıfırla (oyun bittiğinde çağır)
        /// </summary>
        public void ClearSession()
        {
            if (CurrentSession != null)
            {
                CurrentSession.IsActive = false;
                Log($"🔄 Session kapatıldı: {CurrentSession}");
            }
            CurrentSession = null;
        }

        /// <summary>
        /// Test için manuel session oluştur (Editor'de test ederken kullanışlı)
        /// </summary>
        public void CreateTestSession(string name, string surname, int id)
        {
            CurrentSession = new PlayerSession
            {
                PlayerName = name,
                PlayerID = id,
                StartTime = DateTime.Now,
                IsActive = true
            };
            
            Log($"🧪 Test session oluşturuldu: {CurrentSession}");
            OnSessionReady?.Invoke(CurrentSession);
        }

        // Debug log metodları
        private void Log(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[SessionManager]</color> {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[SessionManager] {message}");
        }

        // ========================================
        // INSPECTOR'DAN ÇAĞRILACAK TEST METODLARI
        // ========================================

        [ContextMenu("Test: Örnek Session Oluştur")]
        private void TestCreateSession()
        {
            CreateTestSession("Test", "Kullanici", 999);
        }

        [ContextMenu("Test: Session'ı Temizle")]
        private void TestClearSession()
        {
            ClearSession();
        }

        [ContextMenu("Test: Örnek CSV Gönder")]
        private void TestSendCSV()
        {
            string testCSV = "Test;Kullanici;999;05:30;Task1;Task2;2;1;85 (İyi);2026-02-15 14:30:00";
            SendResultToPC(testCSV);
        }
    }
}