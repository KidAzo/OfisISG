using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using Woi.VrBridge.Client.Protocol;

namespace Woi.VrBridge.Client.Storage
{
    public sealed class DeviceIdentityRecord
    {
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }

        [JsonProperty("createdAtUtc")]
        public DateTimeOffset CreatedAtUtc { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("lastServiceId")]
        public string LastServiceId { get; set; }

        [JsonProperty("lastGatewayUrl")]
        public string LastGatewayUrl { get; set; }
    }

    public sealed class DeviceIdentityStore
    {
        readonly string _filePath;
        DeviceIdentityCorruptionException _corruption;

        public DeviceIdentityStore(string rootDirectory = null)
        {
            var root = rootDirectory ?? Path.Combine(Application.persistentDataPath, "WOI", "VRBridge");
            _filePath = Path.Combine(root, "device.json");
        }

        public string FilePath => _filePath;
        public bool HasCorruption => _corruption != null;
        public DeviceIdentityCorruptionException Corruption => _corruption;

        public async UniTask<string> GetOrCreateDeviceIdAsync()
        {
            if (_corruption != null)
            {
                throw _corruption;
            }

            var record = await ReadAsync();
            if (record != null && !string.IsNullOrWhiteSpace(record.DeviceId))
            {
                return record.DeviceId;
            }

            if (File.Exists(_filePath) && record == null)
            {
                _corruption = new DeviceIdentityCorruptionException(ErrorCodes.DeviceIdentityReadFailed);
                throw _corruption;
            }

            record = new DeviceIdentityRecord
            {
                DeviceId = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            await WriteAsync(record);
            return record.DeviceId;
        }

        public async UniTask<DeviceIdentityRecord> GetRecordAsync()
        {
            return await ReadAsync();
        }

        public async UniTask UpdateEndpointMetadataAsync(string displayName, string serviceId, string gatewayUrl)
        {
            var record = await ReadAsync() ?? new DeviceIdentityRecord
            {
                DeviceId = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            if (string.IsNullOrWhiteSpace(record.DeviceId))
            {
                throw new DeviceIdentityCorruptionException(ErrorCodes.DeviceIdentityReadFailed);
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                record.DisplayName = displayName;
            }

            record.LastServiceId = serviceId;
            record.LastGatewayUrl = gatewayUrl;
            await WriteAsync(record);
        }

        public async UniTask ResetAsync()
        {
            _corruption = null;
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            await UniTask.CompletedTask;
        }

        async UniTask<DeviceIdentityRecord> ReadAsync()
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(_filePath, Encoding.UTF8);
                var record = JsonConvert.DeserializeObject<DeviceIdentityRecord>(json);
                if (record == null || string.IsNullOrWhiteSpace(record.DeviceId))
                {
                    _corruption = new DeviceIdentityCorruptionException(ErrorCodes.DeviceIdentityReadFailed);
                    return null;
                }

                return record;
            }
            catch (DeviceIdentityCorruptionException)
            {
                throw;
            }
            catch (Exception)
            {
                _corruption = new DeviceIdentityCorruptionException(ErrorCodes.DeviceIdentityReadFailed);
                return null;
            }
        }

        async UniTask WriteAsync(DeviceIdentityRecord record)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(record, Formatting.Indented);
            var tempPath = _filePath + ".tmp";

            try
            {
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            var written = File.ReadAllText(tempPath, Encoding.UTF8);
            var validated = JsonConvert.DeserializeObject<DeviceIdentityRecord>(written);
            if (validated == null || string.IsNullOrWhiteSpace(validated.DeviceId))
            {
                throw new DeviceIdentityCorruptionException(ErrorCodes.DeviceIdentityWriteFailed);
            }

            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }

            File.Move(tempPath, _filePath);
            await UniTask.CompletedTask;
        }
            catch (DeviceIdentityCorruptionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DeviceIdentityCorruptionException(ErrorCodes.DeviceIdentityWriteFailed, ex);
            }
        }
    }

    public sealed class DeviceIdentityCorruptionException : Exception
    {
        public string ErrorCode { get; }

        public DeviceIdentityCorruptionException(string errorCode, Exception inner = null)
            : base(errorCode, inner)
        {
            ErrorCode = errorCode;
        }
    }
}
