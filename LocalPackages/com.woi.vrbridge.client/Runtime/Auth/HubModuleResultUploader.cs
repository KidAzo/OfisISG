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

    /// Best-effort Hub <c>submitModuleRunResult</c> callable using the customer ID token

    /// obtained from Phase 2D ticket redemption. Failures must never block Bridge local results.

    /// </summary>

    public sealed class HubModuleResultUploader

    {

        readonly FirebaseAuthConfig _config;

        readonly IVrCustomerAuthSession _authSession;



        public HubModuleResultUploader(FirebaseAuthConfig config, IVrCustomerAuthSession authSession)

        {

            _config = config ?? throw new ArgumentNullException(nameof(config));

            _authSession = authSession ?? throw new ArgumentNullException(nameof(authSession));

        }



        public async UniTask<bool> TrySubmitAsync(HubModuleRunResultRequest request, CancellationToken cancellationToken)

        {

            if (request == null || !_config.IsConfigured || !_authSession.IsAuthenticated)

            {

                return false;

            }



            string idToken;

            try

            {

                idToken = await _authSession.GetIdTokenAsync(cancellationToken);

            }

            catch (Exception)

            {

                Debug.LogWarning("[VRBridge][HubResult] ID token unavailable; Hub upload skipped.");

                return false;

            }



            if (string.IsNullOrWhiteSpace(idToken))

            {

                return false;

            }



            var envelope = new CallableRequestEnvelope<HubModuleRunResultRequest>

            {

                Data = request

            };

            var json = JsonConvert.SerializeObject(envelope, ProtocolJson.Settings);



            using var uwr = new UnityWebRequest(_config.SubmitModuleRunResultCallableUrl(), UnityWebRequest.kHttpVerbPOST);

            var bodyBytes = Encoding.UTF8.GetBytes(json);

            uwr.uploadHandler = new UploadHandlerRaw(bodyBytes);

            uwr.downloadHandler = new DownloadHandlerBuffer();

            uwr.SetRequestHeader("Content-Type", "application/json");

            uwr.SetRequestHeader("Authorization", "Bearer " + idToken);



            try

            {

                await uwr.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

            }

            catch (Exception)

            {

                Debug.LogWarning("[VRBridge][HubResult] Network failure submitting module result.");

                return false;

            }



#if UNITY_2020_2_OR_NEWER

            var failed = uwr.result != UnityWebRequest.Result.Success;

#else

            var failed = uwr.isNetworkError || uwr.isHttpError;

#endif

            if (failed)

            {

                // Never log response body — may echo request fields.

                Debug.LogWarning($"[VRBridge][HubResult] submitModuleRunResult failed http={uwr.responseCode}");

                return false;

            }



            Debug.Log($"[VRBridge][HubResult] Stored Hub result sessionIdLength={request.SessionId?.Length ?? 0}");

            return true;

        }

    }



    public sealed class HubModuleRunResultRequest

    {

        [JsonProperty("moduleId")]

        public string ModuleId { get; set; }



        [JsonProperty("sessionId")]

        public string SessionId { get; set; }



        [JsonProperty("platform")]

        public string Platform { get; set; } = "vr";



        [JsonProperty("deviceId")]

        public string DeviceId { get; set; }



        [JsonProperty("moduleVersion")]

        public string ModuleVersion { get; set; }



        [JsonProperty("result")]

        public HubModuleRunResultBody Result { get; set; }

    }



    public sealed class HubModuleRunResultBody

    {

        [JsonProperty("traineeId")]

        public string TraineeId { get; set; }



        [JsonProperty("traineeName")]

        public string TraineeName { get; set; }



        [JsonProperty("resultStatus")]

        public string ResultStatus { get; set; } = "completed";



        [JsonProperty("score")]

        public double Score { get; set; }



        [JsonProperty("overallTrainingPassed")]

        public bool? OverallTrainingPassed { get; set; }



        [JsonProperty("rulesEvaluated")]

        public bool RulesEvaluated { get; set; }



        [JsonProperty("durationSeconds")]

        public double DurationSeconds { get; set; }



        [JsonProperty("criticalMistakes")]

        public string[] CriticalMistakes { get; set; } = Array.Empty<string>();



        [JsonProperty("fires")]

        public object[] Fires { get; set; } = Array.Empty<object>();



        [JsonProperty("clientCompletedAt")]

        public string ClientCompletedAt { get; set; }

    }

}


