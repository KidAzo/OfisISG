using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.DataHandler
{
    /// <summary>
    /// Network thread'lerinden gelen işleri Unity main thread'inde çalıştırmak için.
    /// IMPORTANT: Instance mutlaka MAIN THREAD'de (Awake/Start) oluşturulmalı.
    /// </summary>
    public sealed class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        private static readonly object _lock = new object();

        public static bool HasInstance => _instance != null;

        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance != null) return _instance;

            var go = new GameObject(nameof(UnityMainThreadDispatcher));
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);

            Debug.Log("[Dispatcher] Created (main thread).");
            return _instance;
        }

        /// <summary>
        /// Queue'ya iş ekler. Thread-safe.
        /// </summary>
        public void Enqueue(Action action)
        {
            if (action == null) return;

            lock (_lock)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            // Bu frame’de çalıştırılacak işleri local listeye al (lock kısa sürsün)
            Action[] actionsToRun = null;

            lock (_lock)
            {
                if (_executionQueue.Count > 0)
                {
                    actionsToRun = _executionQueue.ToArray();
                    _executionQueue.Clear();
                }
            }

            if (actionsToRun == null) return;

            for (int i = 0; i < actionsToRun.Length; i++)
            {
                try
                {
                    actionsToRun[i]?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Dispatcher] Action threw exception: {ex}");
                }
            }
        }
    }
}
