using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using WoiUtils;

namespace Woi.DataHandler
{
    public class WoiDiscoverySender : PersistentSingleton<WoiDiscoverySender>
    {
        [Header("Discovery")]
        [SerializeField] int discoveryPort = 7778;                 
        [SerializeField] float intervalSeconds = 1.0f;           
        [SerializeField] string discoverMessage = "WOI_DISCOVER";  

        [Header("Debug")]
        [SerializeField] bool log = true;

        UdpClient _client;
        IPEndPoint _broadcastEndPoint;
        float _t;

        protected override void Awake()
        {
            base.Awake();
            
            try
            {
                _client = new UdpClient();
                _client.EnableBroadcast = true;
                _broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

                if (log) Debug.Log($"[DiscoverySender] Ready. Broadcast -> 255.255.255.255:{discoveryPort}");
            }
            catch (Exception e)
            {
                Debug.LogError("[DiscoverySender] Init error: " + e);
            }
        }

        void OnDestroy()
        {
            try { _client?.Close(); } catch {}
            _client = null;
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_t < intervalSeconds) return;
            _t = 0f;

            SendDiscover();
        }

        void SendDiscover()
        {
            if (_client == null) return;

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(discoverMessage);
                _client.Send(data, data.Length, _broadcastEndPoint);

                if (log) Debug.Log("[DiscoverySender] Sent: WOI_DISCOVER");
            }
            catch (Exception e)
            {
                Debug.LogError("[DiscoverySender] Send error: " + e.Message);
            }
        }
    }
}
