using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Configuration;

namespace Woi.VrBridge.Client.Connection
{
    public sealed class ReconnectPolicy
    {
        readonly BridgeClientConfig _config;
        readonly Random _random = new Random();
        int _attempt;

        public ReconnectPolicy(BridgeClientConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public int Attempt => _attempt;

        public void Reset()
        {
            _attempt = 0;
        }

        public async UniTask DelayBeforeNextAttemptAsync(CancellationToken cancellationToken)
        {
            var delays = _config.ReconnectDelaysSeconds;
            var index = Math.Min(_attempt, delays.Length - 1);
            var baseDelay = delays[index];
            var jitter = _config.ReconnectJitterMaxSeconds > 0f
                ? (float)_random.NextDouble() * _config.ReconnectJitterMaxSeconds
                : 0f;

            _attempt++;
            await UniTask.Delay(TimeSpan.FromSeconds(baseDelay + jitter), cancellationToken: cancellationToken);
        }
    }
}
