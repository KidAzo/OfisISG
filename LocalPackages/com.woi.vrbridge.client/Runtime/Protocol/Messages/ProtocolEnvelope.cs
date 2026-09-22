using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Woi.VrBridge.Client.Protocol.Messages
{
    public sealed class ProtocolEnvelope
    {
        [JsonProperty("protocolVersion")]
        public int ProtocolVersion { get; set; } = ProtocolConstants.ProtocolVersion;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("messageId")]
        public string MessageId { get; set; } = string.Empty;

        [JsonProperty("correlationId")]
        public string CorrelationId { get; set; }

        [JsonProperty("sentAtUtc")]
        public DateTimeOffset SentAtUtc { get; set; }

        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }

        [JsonProperty("payload")]
        public JToken Payload { get; set; }
    }

    public sealed class ProtocolEnvelope<TPayload> where TPayload : class
    {
        [JsonProperty("protocolVersion")]
        public int ProtocolVersion { get; set; } = ProtocolConstants.ProtocolVersion;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("messageId")]
        public string MessageId { get; set; } = string.Empty;

        [JsonProperty("correlationId")]
        public string CorrelationId { get; set; }

        [JsonProperty("sentAtUtc")]
        public DateTimeOffset SentAtUtc { get; set; }

        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }

        [JsonProperty("payload")]
        public TPayload Payload { get; set; }
    }
}
