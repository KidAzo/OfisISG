using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Protocol
{
    public static class ProtocolJson
    {
        public static readonly JsonSerializerSettings Settings = CreateSettings();

        public static JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Formatting = Formatting.None
            };
        }

        public static ProtocolEnvelope<T> Create<T>(string type, T payload, string deviceId = null, string correlationId = null)
            where T : class
        {
            return new ProtocolEnvelope<T>
            {
                ProtocolVersion = ProtocolConstants.ProtocolVersion,
                Type = type,
                MessageId = Guid.NewGuid().ToString("N"),
                CorrelationId = correlationId,
                SentAtUtc = DateTimeOffset.UtcNow,
                DeviceId = deviceId,
                Payload = payload
            };
        }

        public static byte[] Serialize<T>(ProtocolEnvelope<T> envelope) where T : class
        {
            var json = JsonConvert.SerializeObject(envelope, Settings);
            return Encoding.UTF8.GetBytes(json);
        }

        public static string SerializeToString<T>(ProtocolEnvelope<T> envelope) where T : class
        {
            return JsonConvert.SerializeObject(envelope, Settings);
        }

        public static string SerializePayload(object payload)
        {
            return JsonConvert.SerializeObject(payload, Settings);
        }

        public static bool TryDeserializeEnvelope(string json, out ProtocolEnvelope envelope, out string error)
        {
            envelope = null;
            error = null;
            try
            {
                envelope = JsonConvert.DeserializeObject<ProtocolEnvelope>(json, Settings);
                if (envelope == null)
                {
                    error = "Envelope was null.";
                    return false;
                }

                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryDeserializeEnvelope(byte[] utf8, out ProtocolEnvelope envelope, out string error)
        {
            return TryDeserializeEnvelope(Encoding.UTF8.GetString(utf8), out envelope, out error);
        }

        public static T DeserializePayload<T>(Newtonsoft.Json.Linq.JToken token) where T : class
        {
            if (token == null)
            {
                return null;
            }

            return token.ToObject<T>(JsonSerializer.Create(Settings));
        }
    }
}
