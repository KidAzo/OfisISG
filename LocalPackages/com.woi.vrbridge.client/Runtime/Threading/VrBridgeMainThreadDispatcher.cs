using System;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Threading
{
    public sealed class VrBridgeMainThreadDispatcher : MonoBehaviour, IMainThreadDispatcher
    {
        static VrBridgeMainThreadDispatcher _instance;
        readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public static VrBridgeMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(nameof(VrBridgeMainThreadDispatcher));
                    _instance = go.AddComponent<VrBridgeMainThreadDispatcher>();
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }
                }

                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (IsMainThread())
            {
                action();
                return;
            }

            _queue.Enqueue(action);
        }

        public async UniTask EnqueueAsync(Func<UniTask> action)
        {
            if (action == null)
            {
                return;
            }

            if (IsMainThread())
            {
                await action();
                return;
            }

            var tcs = new UniTaskCompletionSource();
            _queue.Enqueue(async () =>
            {
                try
                {
                    await action();
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            await tcs.Task;
        }

        static bool IsMainThread()
        {
            return System.Threading.Thread.CurrentThread.ManagedThreadId == 1;
        }
    }
}
