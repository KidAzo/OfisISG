using Newtonsoft.Json;

namespace Woi.VrBridge.Client.Auth
{
    /// <summary>Envelope for Firebase HTTPS callable functions: { "data": {...} }.</summary>
    sealed class CallableRequestEnvelope<T>
    {
        [JsonProperty("data")]
        public T Data { get; set; }
    }

    sealed class RedeemCallableRequestData
    {
        [JsonProperty("ticket")]
        public string Ticket { get; set; }

        [JsonProperty("authorizationId")]
        public string AuthorizationId { get; set; }

        [JsonProperty("sessionId")]
        public string SessionId { get; set; }

        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }

        [JsonProperty("moduleId")]
        public string ModuleId { get; set; }

        [JsonProperty("participantHash")]
        public string ParticipantHash { get; set; }
    }

    sealed class CallableResponseEnvelope<T>
    {
        [JsonProperty("result")]
        public T Result { get; set; }
    }

    sealed class RedeemCallableResult
    {
        [JsonProperty("customToken")]
        public string CustomToken { get; set; }

        [JsonProperty("firebaseUid")]
        public string FirebaseUid { get; set; }

        [JsonProperty("customerId")]
        public string CustomerId { get; set; }
    }

    sealed class SignInWithCustomTokenRequest
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("returnSecureToken")]
        public bool ReturnSecureToken { get; set; } = true;
    }

    sealed class SignInWithCustomTokenResponse
    {
        [JsonProperty("idToken")]
        public string IdToken { get; set; }

        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }

        [JsonProperty("expiresIn")]
        public string ExpiresIn { get; set; }

        [JsonProperty("localId")]
        public string LocalId { get; set; }
    }

    sealed class RefreshTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public string ExpiresIn { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("id_token")]
        public string IdToken { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }
    }
}
