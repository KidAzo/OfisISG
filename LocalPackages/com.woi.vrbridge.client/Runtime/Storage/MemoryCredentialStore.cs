using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Storage
{
    public sealed class MemoryCredentialStore : IDeviceCredentialStore
    {
        string _token;

        public bool HasToken => !string.IsNullOrEmpty(_token);

        public UniTask<string> ReadTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(_token);
        }

        public UniTask WriteTokenAsync(string token, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _token = token;
            return UniTask.CompletedTask;
        }

        public UniTask DeleteTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _token = null;
            return UniTask.CompletedTask;
        }
    }
}
