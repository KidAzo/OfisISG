using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Protocol;

namespace Woi.VrBridge.Client.Tests.Support
{
    /// <summary>Deterministic fake for <see cref="IVrCustomerAuthSession"/> used by EditMode tests.</summary>
    public sealed class FakeVrCustomerAuthSession : IVrCustomerAuthSession
    {
        public int RedeemCallCount { get; private set; }
        public bool ShouldFail { get; set; }
        public string FailureErrorCode { get; set; } = ErrorCodes.AuthorizationRedeemFailed;
        public RedeemRequest LastRequest { get; private set; }

        public bool IsAuthenticated { get; private set; }
        public string FirebaseUid { get; private set; } = "fake-firebase-uid";
        public string CustomerId { get; private set; } = "fake-customer-id";

        public UniTask RedeemAndSignInAsync(RedeemRequest request, CancellationToken cancellationToken)
        {
            RedeemCallCount++;
            LastRequest = request;
            if (ShouldFail)
            {
                throw new VrCustomerAuthException(FailureErrorCode, "Simulated redeem failure.");
            }

            IsAuthenticated = true;
            return UniTask.CompletedTask;
        }

        public UniTask<string> GetIdTokenAsync(CancellationToken cancellationToken)
        {
            return UniTask.FromResult("fake-id-token");
        }

        public UniTask SignOutCustomerAsync(CancellationToken cancellationToken)
        {
            IsAuthenticated = false;
            return UniTask.CompletedTask;
        }
    }
}
