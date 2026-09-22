using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Woi.VrBridge.Client.Protocol;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Connection
{
    public sealed class ResultOutboxEntry
    {
        [JsonProperty("resultId")]
        public string ResultId { get; set; }

        [JsonProperty("sessionId")]
        public string SessionId { get; set; }

        [JsonProperty("moduleId")]
        public string ModuleId { get; set; }

        [JsonProperty("body")]
        public ResultBodyPayload Body { get; set; }

        [JsonProperty("payloadSha256")]
        public string PayloadSha256 { get; set; }

        [JsonProperty("attempts")]
        public int Attempts { get; set; }

        [JsonProperty("queuedAtUtc")]
        public DateTimeOffset QueuedAtUtc { get; set; }
    }

    public sealed class ResultOutbox
    {
        readonly string _filePath;
        readonly int _maxRetries;
        readonly object _lock = new object();
        List<ResultOutboxEntry> _entries = new List<ResultOutboxEntry>();

        public ResultOutbox(int maxRetries, string rootDirectory = null)
        {
            _maxRetries = maxRetries;
            var root = rootDirectory ?? Path.Combine(Application.persistentDataPath, "WOI", "VRBridge");
            _filePath = Path.Combine(root, "result-outbox.json");
            Load();
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _entries.Count;
                }
            }
        }

        public IReadOnlyList<ResultOutboxEntry> Snapshot()
        {
            lock (_lock)
            {
                return _entries.ToList();
            }
        }

        public void Enqueue(string resultId, string sessionId, string moduleId, ResultBodyPayload body)
        {
            var bodyJson = ProtocolJson.SerializePayload(body);
            var hash = ComputeSha256Hex(bodyJson);

            lock (_lock)
            {
                var existing = _entries.FirstOrDefault(e =>
                    string.Equals(e.ResultId, resultId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (!string.Equals(existing.PayloadSha256, hash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(ErrorCodes.IdempotencyConflict);
                    }

                    return;
                }

                _entries.Add(new ResultOutboxEntry
                {
                    ResultId = resultId,
                    SessionId = sessionId,
                    ModuleId = moduleId,
                    Body = body,
                    PayloadSha256 = hash,
                    Attempts = 0,
                    QueuedAtUtc = DateTimeOffset.UtcNow
                });

                PersistLocked();
            }
        }

        public void MarkAttempt(string resultId)
        {
            lock (_lock)
            {
                var entry = _entries.FirstOrDefault(e =>
                    string.Equals(e.ResultId, resultId, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    entry.Attempts++;
                    PersistLocked();
                }
            }
        }

        public void Remove(string resultId)
        {
            lock (_lock)
            {
                _entries.RemoveAll(e => string.Equals(e.ResultId, resultId, StringComparison.OrdinalIgnoreCase));
                PersistLocked();
            }
        }

        /// <summary>
        /// Does not drop unacknowledged results. Retries continue until ACK or retention policy.
        /// </summary>
        public void PruneExceeded()
        {
            // Intentionally no-op for durability: offline results must survive reconnects.
        }

        public bool ShouldSkipSend(ResultOutboxEntry entry)
        {
            return entry != null && entry.Attempts > 0 && entry.Attempts % Math.Max(1, _maxRetries) == 0;
        }

        public static string ComputeSha256Hex(string json)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }

        void Load()
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_filePath, Encoding.UTF8);
                _entries = JsonConvert.DeserializeObject<List<ResultOutboxEntry>>(json) ?? new List<ResultOutboxEntry>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRBridge] Failed to load result outbox: {ex.Message}");
                _entries = new List<ResultOutboxEntry>();
            }
        }

        void PersistLocked()
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(_entries, Formatting.Indented);
            var temp = _filePath + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            File.Move(temp, _filePath);
        }

        public async UniTask PersistAsync()
        {
            await UniTask.RunOnThreadPool(() =>
            {
                lock (_lock)
                {
                    PersistLocked();
                }
            });
        }
    }
}
