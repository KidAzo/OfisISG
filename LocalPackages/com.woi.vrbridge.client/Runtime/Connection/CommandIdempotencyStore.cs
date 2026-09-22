using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Woi.VrBridge.Client.Connection
{
    public sealed class CommandIdempotencyEntry
    {
        [JsonProperty("commandMessageId")]
        public string CommandMessageId { get; set; }

        [JsonProperty("sessionId")]
        public string SessionId { get; set; }

        [JsonProperty("payloadHash")]
        public string PayloadHash { get; set; }

        [JsonProperty("recordedAtUtc")]
        public DateTimeOffset RecordedAtUtc { get; set; }
    }

    public sealed class CommandIdempotencyStore
    {
        readonly string _filePath;
        readonly int _maxEntries;
        readonly object _lock = new object();
        List<CommandIdempotencyEntry> _entries = new List<CommandIdempotencyEntry>();

        public CommandIdempotencyStore(int maxEntries, string rootDirectory = null)
        {
            _maxEntries = maxEntries;
            var root = rootDirectory ?? Path.Combine(Application.persistentDataPath, "WOI", "VRBridge");
            _filePath = Path.Combine(root, "command-idempotency.json");
            Load();
        }

        public bool TryGet(string commandMessageId, out CommandIdempotencyEntry entry)
        {
            lock (_lock)
            {
                entry = _entries.FirstOrDefault(e =>
                    string.Equals(e.CommandMessageId, commandMessageId, StringComparison.OrdinalIgnoreCase));
                return entry != null;
            }
        }

        public bool TryGetBySession(string sessionId, out CommandIdempotencyEntry entry)
        {
            lock (_lock)
            {
                entry = _entries.LastOrDefault(e =>
                    string.Equals(e.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
                return entry != null;
            }
        }

        public void Record(string commandMessageId, string sessionId, string payloadHash)
        {
            lock (_lock)
            {
                _entries.RemoveAll(e =>
                    string.Equals(e.CommandMessageId, commandMessageId, StringComparison.OrdinalIgnoreCase));

                _entries.Add(new CommandIdempotencyEntry
                {
                    CommandMessageId = commandMessageId,
                    SessionId = sessionId,
                    PayloadHash = payloadHash,
                    RecordedAtUtc = DateTimeOffset.UtcNow
                });

                while (_entries.Count > _maxEntries)
                {
                    _entries.RemoveAt(0);
                }

                PersistLocked();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                PersistLocked();
            }
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
                _entries = JsonConvert.DeserializeObject<List<CommandIdempotencyEntry>>(json) ?? new List<CommandIdempotencyEntry>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRBridge] Failed to load idempotency store: {ex.Message}");
                _entries = new List<CommandIdempotencyEntry>();
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
