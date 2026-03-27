using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using WoiUtils;
using Woi.Events;
using Obvious.Soap;
using Woi.Porting;
using Sirenix.OdinInspector;

namespace Woi.DataHandler
{
    public class SessionManager : PersistentSingleton<SessionManager>
    {
        [Header("━━━━━━━ NETWORK AYARLARI ━━━━━━━")]
        [SerializeField] private int udpListenPort = 7777;
        [SerializeField] private string pcServerUrl = "http://192.168.1.50:8080";

        [Header("━━━━━━━ DEBUG AYARLARI ━━━━━━━")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private ScriptableEventNoParam onSessionStarted;   
        [SerializeField] private ServerDiscoveryClient discovery;
        [SerializeField] private IPortingService portingService;

        public PlayerSession CurrentSession { get; private set; }

        private UdpClient udpListener;
        private bool isListening;

        public event Action<PlayerSession> OnSessionReady;
        public event Action<string> OnError;

        protected override void Awake()
        {
            base.Awake(); 
            UnityMainThreadDispatcher.Instance();

            StartListening();
        }

        void Start()
        {
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

        private void OnEnable() => StartListening();
        private void OnDisable() => StopListening();


        private void StopListening()
        {
            isListening = false;

            if (udpListener == null) return;

            try { udpListener.Close(); } catch {}
            try { udpListener.Dispose(); } catch {}

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

                    OnSessionReady?.Invoke(CurrentSession);

                    EventBus.Publish(new OnLogged(CurrentSession.PlayerName, CurrentSession.PlayerID, 0)); 

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

        [Button]
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
            OnSessionReady?.Invoke(CurrentSession);
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
