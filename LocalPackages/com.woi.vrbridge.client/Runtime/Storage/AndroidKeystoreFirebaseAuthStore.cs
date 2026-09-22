#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Storage
{
    /// <summary>
    /// Android Keystore backed storage for the Firebase refresh token obtained after redeeming a
    /// Bridge session.authorize.command ticket. Mirrors <see cref="AndroidKeystoreCredentialStore"/>
    /// but uses a dedicated alias so device pairing tokens and customer auth tokens never collide.
    /// The refresh token is the only Firebase credential persisted to disk — id tokens stay in memory.
    /// </summary>
    public sealed class AndroidKeystoreFirebaseAuthStore : IDeviceCredentialStore
    {
        const string JavaClassName = "com.woi.vrbridge.security.WoiCredentialStore";
        const string Alias = "woi_vrbridge_firebase_refresh";

        bool _hasTokenChecked;
        bool _hasToken;

        public bool HasToken
        {
            get
            {
                if (!_hasTokenChecked)
                {
                    _hasToken = CallBool("has", Alias);
                    _hasTokenChecked = true;
                }

                return _hasToken;
            }
        }

        public async UniTask<string> ReadTokenAsync(System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.SwitchToMainThread();
            var token = CallString("read", Alias);
            _hasToken = !string.IsNullOrEmpty(token);
            _hasTokenChecked = true;
            return token;
        }

        public async UniTask WriteTokenAsync(string token, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token cannot be empty.", nameof(token));
            }

            await UniTask.SwitchToMainThread();
            CallVoid("store", Alias, token);
            _hasToken = true;
            _hasTokenChecked = true;
        }

        public async UniTask DeleteTokenAsync(System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.SwitchToMainThread();
            CallVoid("delete", Alias);
            _hasToken = false;
            _hasTokenChecked = true;
        }

        static AndroidJavaObject GetStore()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            return new AndroidJavaClass(JavaClassName).CallStatic<AndroidJavaObject>("getInstance", activity);
        }

        static string CallString(string method, string alias)
        {
            try
            {
                using var store = GetStore();
                return store.Call<string>(method, alias);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VRBridge] Android keystore (firebase) read failed: {ex.Message}");
                return null;
            }
        }

        static bool CallBool(string method, string alias)
        {
            try
            {
                using var store = GetStore();
                return store.Call<bool>(method, alias);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VRBridge] Android keystore (firebase) probe failed: {ex.Message}");
                return false;
            }
        }

        static void CallVoid(string method, string alias, string value = null)
        {
            try
            {
                using var store = GetStore();
                if (value == null)
                {
                    store.Call(method, alias);
                }
                else
                {
                    store.Call(method, alias, value);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VRBridge] Android keystore (firebase) operation failed: {ex.Message}");
                throw;
            }
        }
    }
}
#endif
