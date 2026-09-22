using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Storage
{
    /// <summary>
    /// DEV ONLY credential store for Unity Editor. Never use in production builds.
    /// Stores encrypted token outside Assets at persistentDataPath/WOI/VRBridge/dev-credentials.bin
    /// </summary>
    public sealed class EditorDevCredentialStore : IDeviceCredentialStore
    {
        readonly string _filePath;
        string _cachedToken;

        public EditorDevCredentialStore(string rootDirectory = null, string fileName = "dev-credentials.bin")
        {
            var root = rootDirectory ?? Path.Combine(Application.persistentDataPath, "WOI", "VRBridge");
            _filePath = Path.Combine(root, fileName);
        }

        public bool HasToken
        {
            get
            {
                EnsureLoaded();
                return !string.IsNullOrEmpty(_cachedToken);
            }
        }

        public async UniTask<string> ReadTokenAsync(System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureLoaded();
            await UniTask.CompletedTask;
            return _cachedToken;
        }

        public async UniTask WriteTokenAsync(string token, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token cannot be empty.", nameof(token));
            }

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var encrypted = Encrypt(token);
            var tempPath = _filePath + ".tmp";
            await UniTask.RunOnThreadPool(() =>
            {
                File.WriteAllBytes(tempPath, encrypted);
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }

                File.Move(tempPath, _filePath);
            }, cancellationToken: cancellationToken);

            _cachedToken = token;
        }

        public async UniTask DeleteTokenAsync(System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _cachedToken = null;
            if (File.Exists(_filePath))
            {
                await UniTask.RunOnThreadPool(() => File.Delete(_filePath), cancellationToken: cancellationToken);
            }
        }

        void EnsureLoaded()
        {
            if (_cachedToken != null || !File.Exists(_filePath))
            {
                return;
            }

            try
            {
                var bytes = File.ReadAllBytes(_filePath);
                _cachedToken = Decrypt(bytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRBridge][DEV] Failed to read dev credentials: {ex.Message}");
                _cachedToken = null;
            }
        }

        static byte[] Encrypt(string plaintext)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plaintext);
            var key = DeriveKey();
            var iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            var result = new byte[iv.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, result, iv.Length, cipher.Length);
            return result;
        }

        static string Decrypt(byte[] payload)
        {
            if (payload == null || payload.Length <= 16)
            {
                return null;
            }

            var iv = new byte[16];
            var cipher = new byte[payload.Length - 16];
            Buffer.BlockCopy(payload, 0, iv, 0, 16);
            Buffer.BlockCopy(payload, 16, cipher, 0, cipher.Length);

            using var aes = Aes.Create();
            aes.Key = DeriveKey();
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }

        static byte[] DeriveKey()
        {
            var salt = Encoding.UTF8.GetBytes("WOI.VRBridge.EditorDev|" + SystemInfo.deviceUniqueIdentifier);
            using var sha = SHA256.Create();
            return sha.ComputeHash(salt);
        }
    }
}
