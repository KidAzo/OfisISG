using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Protocol;

namespace Woi.VrBridge.Client.Auth
{
    /// <summary>
    /// Safe fallback used when no <see cref="Configuration.FirebaseAuthConfig"/> is assigned.
    /// Every redeem attempt fails deterministically so session.authorize.command is rejected with a
    /// clear configuration error instead of throwing an unhandled exception.
    /// </summary>
    public sealed class NullVrCustomerAuthSession : IVrCustomerAuthSession
    {
        public bool IsAuthenticated => false;
        public string FirebaseUid => null;
        public string CustomerId => null;

        public UniTask RedeemAndSignInAsync(RedeemRequest request, CancellationToken cancellationToken)
        {
            throw new VrCustomerAuthException(ErrorCodes.AuthorizationNotConfigured, "No Firebase auth session is configured on this device.");
        }

        public UniTask<string> GetIdTokenAsync(CancellationToken cancellationToken)
        {
            throw new VrCustomerAuthException(ErrorCodes.AuthorizationNotConfigured, "No Firebase auth session is configured on this device.");
        }

        public UniTask SignOutCustomerAsync(CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }
    }
}
