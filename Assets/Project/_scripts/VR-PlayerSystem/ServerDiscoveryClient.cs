using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Woi.DataHandler
{
    public class ServerDiscoveryClient : MonoBehaviour
    {
        [SerializeField] int discoveryPort = 7778;
        [SerializeField] float retryInterval = 1.0f;
        [SerializeField] int maxAttempts = 15;

        public event Action<string> OnServerDiscovered; // "http://ip:port"

        UdpClient _client;
        int _attemptsLeft;

        void OnEnable()
        {
            _attemptsLeft = maxAttempts;
            StartClient();
            InvokeRepeating(nameof(SendDiscover), 0f, retryInterval);
        }

        void OnDisable()
        {
            CancelInvoke(nameof(SendDiscover));
            StopClient();
        }

        void StartClient()
        {
            StopClient();

            // random local port, broadcast enabled
            _client = new UdpClient(0);
            _client.EnableBroadcast = true;

            // listen for replies
            _client.BeginReceive(OnReceive, null);

            Debug.Log("[Discovery] Client started.");
        }

        void StopClient()
        {
            try { _client?.Close(); } catch { }
            _client = null;
        }

        void SendDiscover()
        {
            if (_client == null) return;

            if (_attemptsLeft-- <= 0)
            {
                Debug.LogWarning("[Discovery] Server bulunamadı (attempt limit).");
                CancelInvoke(nameof(SendDiscover));
                return;
            }

            var msg = Encoding.UTF8.GetBytes("WOI_DISCOVER");
            var endPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

            try
            {
                _client.Send(msg, msg.Length, endPoint);
                Debug.Log("[Discovery] WOI_DISCOVER broadcast sent.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Discovery] Send error: {ex.Message}");
            }
        }

        void OnReceive(IAsyncResult ar)
        {
            if (_client == null) return;

            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;

            try
            {
                data = _client.EndReceive(ar, ref remote);
            }
            catch
            {
                return;
            }

            // keep listening
            try { _client.BeginReceive(OnReceive, null); } catch { }

            var text = Encoding.UTF8.GetString(data).Trim();
            if (!text.StartsWith("WOI_SERVER|")) return;

            var parts = text.Split('|');
            if (parts.Length != 3) return;

            var ip = parts[1].Trim();
            var port = parts[2].Trim();

            var url = $"http://{ip}:{port}";
            Debug.Log($"[Discovery] Server discovered: {url} (from {remote.Address})");

            CancelInvoke(nameof(SendDiscover));
            StopClient();

            OnServerDiscovered?.Invoke(url);
        }
    }
}

