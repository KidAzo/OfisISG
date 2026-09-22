using UnityEngine;

namespace Woi.VrBridge.Client.Configuration
{
    /// <summary>
    /// Digitech Hub Firebase project settings used by <see cref="Abstractions.IVrCustomerAuthSession"/>
    /// to redeem Bridge one-time tickets via REST (no Firebase Auth SDK dependency).
    /// Editor defaults are intentionally empty; the shell installer / build pipeline assigns real values.
    /// </summary>
    [CreateAssetMenu(fileName = "FirebaseAuthConfig", menuName = "WOI/VR Bridge/Firebase Auth Config")]
    public sealed class FirebaseAuthConfig : ScriptableObject
    {
        [Header("Digitech Hub Firebase Project")]
        [Tooltip("Web API key for the Firebase project (Identity Toolkit / Secure Token REST calls).")]
        public string ApiKey = string.Empty;

        [Tooltip("Firebase project id, e.g. digitech-hub-388ab.")]
        public string ProjectId = string.Empty;

        [Tooltip("Cloud Functions region hosting redeemVrTrainingAuthorization.")]
        public string FunctionsRegion = "us-central1";

        [Tooltip("Callable Cloud Function name that redeems a Bridge one-time ticket for a Firebase custom token.")]
        public string RedeemFunctionName = "redeemVrTrainingAuthorization";

        [Tooltip("Callable Cloud Function name that stores a Hub module run result under the authenticated customer UID.")]
        public string SubmitModuleRunResultFunctionName = "submitModuleRunResult";

        [Header("Overrides (tests / self-hosted emulators only)")]
        public string FunctionsBaseUrlOverride = string.Empty;
        public string IdentityToolkitBaseUrlOverride = string.Empty;
        public string SecureTokenBaseUrlOverride = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey)
            && !string.IsNullOrWhiteSpace(ProjectId)
            && !string.IsNullOrWhiteSpace(FunctionsRegion);

        public string FunctionsBaseUrl()
        {
            return !string.IsNullOrWhiteSpace(FunctionsBaseUrlOverride)
                ? FunctionsBaseUrlOverride.TrimEnd('/')
                : $"https://{FunctionsRegion}-{ProjectId}.cloudfunctions.net";
        }

        public string RedeemCallableUrl() => $"{FunctionsBaseUrl()}/{RedeemFunctionName}";

        public string SubmitModuleRunResultCallableUrl() =>
            $"{FunctionsBaseUrl()}/{SubmitModuleRunResultFunctionName}";

        public string SignInWithCustomTokenUrl()
        {
            var baseUrl = !string.IsNullOrWhiteSpace(IdentityToolkitBaseUrlOverride)
                ? IdentityToolkitBaseUrlOverride.TrimEnd('/')
                : "https://identitytoolkit.googleapis.com/v1";
            return $"{baseUrl}/accounts:signInWithCustomToken?key={ApiKey}";
        }

        public string RefreshTokenUrl()
        {
            var baseUrl = !string.IsNullOrWhiteSpace(SecureTokenBaseUrlOverride)
                ? SecureTokenBaseUrlOverride.TrimEnd('/')
                : "https://securetoken.googleapis.com/v1";
            return $"{baseUrl}/token?key={ApiKey}";
        }
    }
}
