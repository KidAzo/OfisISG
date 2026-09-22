namespace Woi.VrBridge.Client.Protocol
{
    /// <summary>
    /// Client-side and wire-aligned VRB error codes used by the Unity client.
    /// </summary>
    public static class ErrorCodes
    {
        public const string NoActiveNetworkAdapter = "VRB-NET-001";
        public const string RequiredPortUnavailable = "VRB-PORT-002";
        public const string FirewallOrNetworkPolicy = "VRB-FW-003";
        public const string DiscoveryTimeout = "VRB-DISC-004";
        public const string DiscoveryNonceMismatch = "VRB-DISC-005";
        public const string DiscoveryLoopbackRejected = "VRB-DISC-006";
        public const string DiscoveryInvalidOffer = "VRB-DISC-007";
        public const string AcknowledgementTimeout = "VRB-ACK-005";
        public const string DeviceAlreadyHasActiveSession = "VRB-SESSION-006";
        public const string InvalidResultPayload = "VRB-RESULT-007";
        public const string LocalDatabaseUnavailable = "VRB-STORAGE-008";
        public const string InvalidServiceConfiguration = "VRB-CONFIG-009";
        public const string SessionInvalidTransition = "VRB-SESSION-010";
        public const string DeviceNotEligible = "VRB-SESSION-011";
        public const string ParticipantValidationFailed = "VRB-SESSION-012";
        public const string DeviceNotFound = "VRB-DISC-013";
        public const string SessionNotFound = "VRB-SESSION-014";
        public const string SessionBusyRejected = "VRB-SESSION-015";
        public const string ProtocolInvalidMessage = "VRB-PROTO-011";
        public const string DeviceAuthenticationFailed = "VRB-AUTH-012";
        public const string DeviceConnectionFailed = "VRB-CONN-013";
        public const string HeartbeatTimedOut = "VRB-HB-014";
        public const string SessionCommandRejected = "VRB-CMD-015";
        public const string IdempotencyConflict = "VRB-IDEMP-016";
        public const string ResultSessionMismatch = "VRB-RESULT-017";
        public const string PairingCodeInvalid = "VRB-PAIR-018";
        public const string DeviceQueueCapacityExceeded = "VRB-QUEUE-019";
        public const string CredentialStoreUnavailable = "VRB-STOR-001";
        public const string CredentialStoreReadFailed = "VRB-STOR-002";
        public const string CredentialStoreWriteFailed = "VRB-STOR-003";
        public const string DeviceIdentityReadFailed = "VRB-STOR-004";
        public const string DeviceIdentityWriteFailed = "VRB-STOR-005";
        public const string WebSocketConnectFailed = "VRB-CONN-014";
        public const string WebSocketSendFailed = "VRB-CONN-015";
        public const string WebSocketReceiveFailed = "VRB-CONN-016";
        public const string PairingRequired = "VRB-AUTH-013";
        public const string PairingCancelled = "VRB-AUTH-014";
        public const string TransportNotConnected = "VRB-CONN-017";

        // Phase 2D — session.authorize.command / Firebase customer auth handoff.
        public const string SessionStartDeprecated = "VRB-CMD-020";
        public const string AuthorizationDeviceMismatch = "VRB-AUTHZ-021";
        public const string AuthorizationModuleUnsupported = "VRB-AUTHZ-022";
        public const string AuthorizationParticipantInvalid = "VRB-AUTHZ-023";
        public const string AuthorizationHashMismatch = "VRB-AUTHZ-024";
        public const string AuthorizationExpired = "VRB-AUTHZ-025";
        public const string AuthorizationConflict = "VRB-AUTHZ-026";
        public const string AuthorizationRedeemFailed = "VRB-AUTHZ-027";
        public const string AuthorizationInvalidCommand = "VRB-AUTHZ-028";
        public const string AuthorizationNotConfigured = "VRB-AUTHZ-029";
        public const string PlayerStartRejected = "VRB-AUTHZ-030";
    }
}
