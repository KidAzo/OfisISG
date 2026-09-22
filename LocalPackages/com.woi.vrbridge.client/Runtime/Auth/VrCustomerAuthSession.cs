using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Protocol;

namespace Woi.VrBridge.Client.Auth
{
    /// <summary>
    /// REST-only implementation of <see cref="IVrCustomerAuthSession"/> for the Digitech Hub Firebase
    /// project. Deliberately avoids the Firebase Auth SDK:
    ///   1) redeemVrTrainingAuthorization callable — exchanges a Bridge one-time ticket for a custom token.
    ///   2) Identity Toolkit signInWithCustomToken — exchanges the custom token for id/refresh tokens.
    ///   3) Secure Token refresh — renews the id token using the persisted refresh token.
    /// The ticket and any resulting tokens are never written to the log.
    /// </summary>
    public sealed class VrCustomerAuthSession : IVrCustomerAuthSession
    {
        static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

        readonly FirebaseAuthConfig _config;
        readonly IDeviceCredentialStore _refreshTokenStore;

        string _idToken;
        string _refreshToken;
        DateTimeOffset _idTokenExpiresAtUtc;
        string _firebaseUid;
        string _customerId;

        public VrCustomerAuthSession(FirebaseAuthConfig config, IDeviceCredentialStore refreshTokenStore)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _refreshTokenStore = refreshTokenStore ?? throw new ArgumentNullException(nameof(refreshTokenStore));
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(_firebaseUid)
                                        && (!string.IsNullOrEmpty(_idToken) || _refreshTokenStore.HasToken);

        public string FirebaseUid => _firebaseUid;
        public string CustomerId => _customerId;

        public async UniTask RedeemAndSignInAsync(RedeemRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!_config.IsConfigured)
            {
                throw new VrCustomerAuthException(ErrorCodes.AuthorizationNotConfigured, "Firebase auth is not configured on this device.");
            }

            RedeemCallableResult redeemResult;
            try
            {
                redeemResult = await CallRedeemFunctionAsync(request, cancellationToken);
            }
            catch (VrCustomerAuthException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new VrCustomerAuthException(ErrorCodes.AuthorizationRedeemFailed, "Ticket redemption failed.", ex);
            }

            if (redeemResult == null || string.IsNullOrWhiteSpace(redeemResult.CustomToken))
            {
                throw new VrCustomerAuthException(ErrorCodes.AuthorizationRedeemFailed, "Redeem response did not include a custom token.");
            }

            SignInWithCustomTokenResponse signInResult;
            try
            {
                signInResult = await CallSignInWithCustomTokenAsync(redeemResult.CustomToken, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new VrCustomerAuthException(ErrorCodes.AuthorizationRedeemFailed, "Custom token sign-in failed.", ex);
            }

            if (signInResult == null || string.IsNullOrWhiteSpace(signInResult.IdToken))
            {
                throw new VrCustomerAuthException(ErrorCodes.AuthorizationRedeemFailed, "Sign-in response did not include an id token.");
            }

            _idToken = signInResult.IdToken;
            _idTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(ParseSeconds(signInResult.ExpiresIn, 3600));
            _firebaseUid = !string.IsNullOrWhiteSpace(redeemResult.FirebaseUid) ? redeemResult.FirebaseUid : signInResult.LocalId;
            _customerId = !string.IsNullOrWhiteSpace(redeemResult.CustomerId) ? redeemResult.CustomerId : _firebaseUid;

            if (!string.IsNullOrWhiteSpace(signInResult.RefreshToken))
            {
                _refreshToken = signInResult.RefreshToken;
                await _refreshTokenStore.WriteTokenAsync(_refreshToken, cancellationToken);
            }

            Debug.Log($"[VRBridge][Auth] Customer session established uidLength={_firebaseUid?.Length ?? 0}");
        }

        public async UniTask<string> GetIdTokenAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_idToken) && DateTimeOffset.UtcNow < _idTokenExpiresAtUtc - RefreshSkew)
            {
                return _idToken;
            }

            await RefreshAsync(cancellationToken);
            return _idToken;
        }

        public async UniTask SignOutCustomerAsync(CancellationToken cancellationToken)
        {
            _idToken = null;
            _refreshToken = null;
            _idTokenExpiresAtUtc = default;
            _firebaseUid = null;
            _customerId = null;
            await _refreshTokenStore.DeleteTokenAsync(cancellationToken);
        }

        async UniTask RefreshAsync(CancellationToken cancellationToken)
        {
            var refreshToken = _refreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken) && _refreshTokenStore.HasToken)
            {
                refreshToken = await _refreshTokenStore.ReadTokenAsync(cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new VrCustomerAuthException(ErrorCodes.AuthorizationRedeemFailed, "No customer session to refresh.");
            }

            RefreshTokenResponse response;
            try
            {
                response = await CallRefreshTokenAsync(refreshToken, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new VrCustomerAuthException(ErrorCodes.AuthorizationRedeemFailed, "Token refresh failed.", ex);
            }

            if (response == null || string.IsNullOrWhiteSpace(response.IdToken))
            {
                throw new VrCustomerAuthException(ErrorCodes.AuthorizationRedeemFailed, "Refresh response did not include an id token.");
            }

            _idToken = response.IdToken;
            _idTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(ParseSeconds(response.ExpiresIn, 3600));
            if (!string.IsNullOrWhiteSpace(response.UserId))
            {
                _firebaseUid = response.UserId;
            }

            if (!string.IsNullOrWhiteSpace(response.RefreshToken))
            {
                _refreshToken = response.RefreshToken;
                await _refreshTokenStore.WriteTokenAsync(_refreshToken, cancellationToken);
            }
        }

        async UniTask<RedeemCallableResult> CallRedeemFunctionAsync(RedeemRequest request, CancellationToken cancellationToken)
        {
            var envelope = new CallableRequestEnvelope<RedeemCallableRequestData>
            {
                Data = new RedeemCallableRequestData
                {
                    Ticket = request.Ticket,
                    AuthorizationId = request.AuthorizationId,
                    SessionId = request.SessionId,
                    DeviceId = request.DeviceId,
                    ModuleId = request.ModuleId,
                    ParticipantHash = request.ParticipantHash
                }
            };

            var json = JsonConvert.SerializeObject(envelope, ProtocolJson.Settings);
            var responseJson = await PostJsonAsync(_config.RedeemCallableUrl(), json, cancellationToken, "redeem");
            var parsed = JsonConvert.DeserializeObject<CallableResponseEnvelope<RedeemCallableResult>>(responseJson, ProtocolJson.Settings);
            return parsed?.Result;
        }

        async UniTask<SignInWithCustomTokenResponse> CallSignInWithCustomTokenAsync(string customToken, CancellationToken cancellationToken)
        {
            var body = JsonConvert.SerializeObject(
                new SignInWithCustomTokenRequest { Token = customToken, ReturnSecureToken = true },
                ProtocolJson.Settings);
            var responseJson = await PostJsonAsync(_config.SignInWithCustomTokenUrl(), body, cancellationToken, "sign-in");
            return JsonConvert.DeserializeObject<SignInWithCustomTokenResponse>(responseJson, ProtocolJson.Settings);
        }

        async UniTask<RefreshTokenResponse> CallRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            var form = $"grant_type=refresh_token&refresh_token={UnityWebRequest.EscapeURL(refreshToken)}";
            using var uwr = new UnityWebRequest(_config.RefreshTokenUrl(), UnityWebRequest.kHttpVerbPOST);
            var bodyBytes = Encoding.UTF8.GetBytes(form);
            uwr.uploadHandler = new UploadHandlerRaw(bodyBytes);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            await uwr.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
            EnsureSuccess(uwr, "refresh");
            return JsonConvert.DeserializeObject<RefreshTokenResponse>(uwr.downloadHandler.text, ProtocolJson.Settings);
        }

        static async UniTask<string> PostJsonAsync(string url, string jsonBody, CancellationToken cancellationToken, string stage)
        {
            using var uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            uwr.uploadHandler = new UploadHandlerRaw(bodyBytes);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");

            await uwr.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
            EnsureSuccess(uwr, stage);
            return uwr.downloadHandler.text;
        }

        static void EnsureSuccess(UnityWebRequest request, string stage)
        {
#if UNITY_2020_2_OR_NEWER
            var failed = request.result != UnityWebRequest.Result.Success;
#else
            var failed = request.isNetworkError || request.isHttpError;
#endif
            if (failed)
            {
                // Deliberately omit response body from the exception message — it may echo request data.
                throw new VrCustomerAuthException(
                    ErrorCodes.AuthorizationRedeemFailed,
                    $"Firebase auth HTTP call failed at stage '{stage}' (code={request.responseCode}).");
            }
        }

        static double ParseSeconds(string value, double fallback)
        {
            return double.TryParse(value, out var seconds) ? seconds : fallback;
        }
    }
}
