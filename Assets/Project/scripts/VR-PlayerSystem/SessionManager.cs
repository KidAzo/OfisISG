using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using FireExtinguisher.Core;
using WoiUtils;
using Woi.Events;
using Obvious.Soap;


namespace Woi.DataHandler
{
    public class SessionManager : PersistentSingleton<SessionManager>
    {
        [Header("━━━━━━━ NETWORK AYARLARI ━━━━━━━")]
        [SerializeField] private int udpListenPort = 7777;
        [SerializeField] private string pcServerUrl = "http://192.168.1.50:8080";

        [Header("━━━━━━━ DEBUG AYARLARI ━━━━━━━")]
        [SerializeField] private bool showDebugLogs = true;
        [Tooltip("When true, starts a local test session if no UDP session arrives (Editor and builds).")]
        [FormerlySerializedAs("autoStartTestSessionInEditor")]
        [SerializeField] private bool autoStartTestSession = true;
        [FormerlySerializedAs("editorTestSessionDelaySeconds")]
        [SerializeField, Min(0f)] private float testSessionDelaySeconds = 3f;
        [SerializeField] private ScriptableEventNoParam onSessionStarted;
        [SerializeField] private ServerDiscoveryClient discovery;
        [SerializeField] private bool useManualUrl = false;
        [SerializeField] private string manualServerUrl = "http://192.168.1.32:8080";

        public PlayerSession CurrentSession { get; private set; }

        private UdpClient udpListener;
        private bool isListening;
        private Coroutine testSessionRoutine;

        public event Action<PlayerSession> OnSessionReady;
        public event Action<string> OnError;

        /// <summary>
        /// Fired when UDP or auto test session is committed (same moment as <see cref="OnSessionReady"/> / onSessionStarted).
        /// </summary>
        public static event Action<PlayerSession> SessionBecameReady;

        protected override void Awake()
        {
            base.Awake();
            
            UnityMainThreadDispatcher.Instance();
        }

        void Start()
        {
            StartListening();

            if (autoStartTestSession)
                ScheduleAutoTestSession();

            if (useManualUrl)
            {
                pcServerUrl = manualServerUrl;
                Log($"🔧 Manuel URL kullanılıyor: {pcServerUrl}");
                return;
            }

            if (discovery != null)
            {
                discovery.OnServerDiscovered += SetPcServerUrl;
                Log("📡 Discovery client bulundu, server arama başladı...");
            }
            else
            {
                LogError("❌ ServerDiscoveryClient sahnede yok!");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                StopListening();
        }

        private void OnApplicationQuit()
        {
            StopListening();
        }

        public void SetPcServerUrl(string url)
        {
            pcServerUrl = url;
            Log($"🔧 PC Server URL set: {pcServerUrl}");
        }

        private void StartListening()
        {
            StopListening(); // ✅ önce varsa kapat

            try
            {
                udpListener = new UdpClient();
                udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, udpListenPort));

                isListening = true;
                udpListener.BeginReceive(OnDataReceived, null);

                Log($"✅ UDP Dinleme BAŞLATILDI - Port: {udpListenPort}");
                Log("📡 Beklenen format: Name|ID");
            }
            catch (Exception e)
            {
                LogError($"❌ UDP başlatma hatası: {e}");
                OnError?.Invoke($"UDP başlatılamadı: {e.Message}");
            }
        }

        [ContextMenu("🧪 Test Session Oluştur")]
        private void TestSession()
        {
            CreateTestSession("Test Oyuncu", 9999);
        }

        [ContextMenu("📤 Test CSV Gönder")]
        private void TestCsvSend()
        {
            if (CurrentSession == null)
            {
                LogError("❌ Önce Test Session oluştur!");
                return;
            }

            // 26 sütunlu tam format: İsim;KullanıcıID;[6 scene x 4 col];Tarih
            string hazard = "\"🟢 Bulunanlar:\n• Test Tehlike\n\n🔴 Bulunamayanlar:\n• Kaçırılan\"";
            string kkd = "\"🟢 Doğru Giyilenler:\n• Baret\n\n🔴 Giyilmeyen/Yanlış:\n• Eldiven\"";

            // Kırıcı dolduruldu, diğer 5 sahne boş
            string csvLine = $"Test Oyuncu;9999;" +
                            $"{hazard};{kkd};02:30;85 (İyi);" + // Kırıcı
                            $";;;;" +                            // Ön Karıştırıcı
                            $";;;;" +                            // VRM
                            $";;;;" +                            // Ön Isıtıcı
                            $";;;;" +                            // Bilyalı Degirmen
                            $";;;;" +                            // Paketleme
                            $"2024-01-01 12:00:00";

            SendResultToPC(csvLine);
        }

        private void StopListening()
        {
            isListening = false;

            if (udpListener == null) return;

            try { udpListener.Close(); } catch { }
            try { udpListener.Dispose(); } catch { }

            udpListener = null;
            Log("🛑 UDP dinleme durduruldu");
        }

        // ⚠️ Background thread
        private void OnDataReceived(IAsyncResult result)
        {
            if (!isListening || udpListener == null) return;

            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                byte[] data = udpListener.EndReceive(result, ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);

                Log($"📩 Veri alındı: {message} (Gönderen: {remoteEndPoint.Address})");

                string[] parts = message.Split('|');
                if (parts.Length != 2)
                {
                    LogError($"❌ Hatalı format! Beklenen Name|ID, Gelen: {message}");
                    return;
                }

                string name = (parts[0] ?? "").Trim();
                string idStr = (parts[1] ?? "").Trim();

                if (string.IsNullOrEmpty(name))
                {
                    LogError("❌ Name boş geldi.");
                    return;
                }

                if (!int.TryParse(idStr, out int id))
                {
                    LogError($"❌ ID parse edilemedi: {idStr}");
                    return;
                }

                var newSession = new PlayerSession
                {
                    PlayerName = name,
                    PlayerID = id,
                    StartTime = DateTime.Now,
                    IsActive = true
                };

                Log($"✅ Session oluşturuldu: {newSession}");

                // ✅ Main thread’e geç
                var dispatcher = UnityMainThreadDispatcher.Instance(); // artık main thread’de create edildiği için güvenli
                dispatcher.Enqueue(() =>
                {
                    if (CurrentSession != null) CurrentSession.IsActive = false;

                    CurrentSession = newSession;

                    Debug.Log($"[SessionManager] MAIN THREAD: CurrentSession set -> {CurrentSession}");

                    RaiseSessionStarted(CurrentSession);

                    Debug.Log($"[SessionManager] OnSessionReady fired -> {CurrentSession}");
                });
            }
            catch (ObjectDisposedException)
            {
                // socket kapandı
            }
            catch (Exception e)
            {
                LogError($"❌ Veri alma hatası: {e}");
            }
            finally
            {
                // ✅ Her koşulda tekrar dinle
                if (isListening && udpListener != null)
                {
                    try { udpListener.BeginReceive(OnDataReceived, null); }
                    catch { /* ignore */ }
                }
            }
        }

        // ================== CSV SEND ==================

        public void SendResultToPC(string csvLine)
        {
            if (string.IsNullOrWhiteSpace(csvLine))
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

            byte[] bodyRaw = Encoding.UTF8.GetBytes(csvLine);

            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "text/plain; charset=utf-8");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Log("✅ Sonuç PC'ye başarıyla kaydedildi!");
            }
            else
            {
                LogError($"❌ Sonuç gönderme hatası: {request.error}");
                LogError($"   Response: {request.downloadHandler?.text}");
                OnError?.Invoke($"CSV gönderilemedi: {request.error}");
            }
        }

        // ================== HELPERS ==================

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
        /// Clears the active session and schedules auto test session again when enabled.
        /// </summary>
        public void PrepareForRestart()
        {
            PrepareForOverlaySceneReload();
        }

        /// <summary>
        /// Called when a gameplay overlay scene (e.g. FireModule_Office) loads again.
        /// Schedules a fresh test session when <see cref="autoStartTestSession"/> is enabled.
        /// </summary>
        public void PrepareForOverlaySceneReload()
        {
            ClearSession();

            if (autoStartTestSession)
                ScheduleAutoTestSession();
        }

        /// <summary>
        /// Re-fires <see cref="OnSessionReady"/> for an active session (e.g. after overlay scene reload).
        /// </summary>
        public void ReNotifySessionReady()
        {
            if (CurrentSession == null || !CurrentSession.IsActive)
                return;

            Log($"🔁 Session re-notified: {CurrentSession}");
            RaiseSessionStarted(CurrentSession);
        }

        private void ScheduleAutoTestSession()
        {
            if (!autoStartTestSession)
                return;

            if (testSessionRoutine != null)
                StopCoroutine(testSessionRoutine);

            testSessionRoutine = StartCoroutine(AutoStartTestSessionRoutine());
        }

        public void CreateTestSession(string name, int id)
        {
            CurrentSession = new PlayerSession
            {
                PlayerName = name,
                PlayerID = id,
                StartTime = DateTime.Now,
                IsActive = true
            };

            Log($"🧪 Test session oluşturuldu: {CurrentSession}");
            RaiseSessionStarted(CurrentSession);
        }

        private void RaiseSessionStarted(PlayerSession session)
        {
            if (session == null || !session.IsActive)
                return;

            OnSessionReady?.Invoke(session);
            SessionBecameReady?.Invoke(session);

            EventBus.Raise(new OnLogged
            {
                UserName = session.PlayerName,
                UserId = session.PlayerID.ToString(),
                SelectedClasses = new List<FireClass>(),
                TargetScene = string.Empty,
            });

            onSessionStarted?.Raise();
        }

        private IEnumerator AutoStartTestSessionRoutine()
        {
            if (testSessionDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(testSessionDelaySeconds);

            testSessionRoutine = null;

            if (CurrentSession != null && CurrentSession.IsActive)
                yield break;

            CreateTestSession("Test Oyuncu", 9999);
        }

        private void Log(string message)
        {
            if (showDebugLogs)
                Debug.Log($"<color=cyan>[SessionManager]</color> {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[SessionManager] {message}");
        }
    }
}
