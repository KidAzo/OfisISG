using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Connection
{
    /// <summary>
    /// The single <see cref="IBridgeModuleLauncher"/> handed to <see cref="VrBridgeClient"/>, dispatching by moduleId.
    /// The first registered launcher whose <see cref="IBridgeModuleLauncher.CanLaunch"/> accepts a module wins,
    /// so earlier registrations keep priority (for example a launcher that also claims "default").
    /// </summary>
    public sealed class BridgeModuleLauncherRouter : IBridgeModuleLauncher
    {
        readonly List<IBridgeModuleLauncher> _launchers = new List<IBridgeModuleLauncher>();
        readonly object _gate = new object();

        string _launchedSessionId;
        IBridgeModuleLauncher _launchedBy;

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _launchers.Count;
                }
            }
        }

        public void Register(IBridgeModuleLauncher launcher)
        {
            if (launcher == null)
            {
                throw new ArgumentNullException(nameof(launcher));
            }

            if (ReferenceEquals(launcher, this))
            {
                throw new ArgumentException("A router cannot route to itself.", nameof(launcher));
            }

            lock (_gate)
            {
                if (!_launchers.Contains(launcher))
                {
                    _launchers.Add(launcher);
                }
            }
        }

        public bool Unregister(IBridgeModuleLauncher launcher)
        {
            if (launcher == null)
            {
                return false;
            }

            lock (_gate)
            {
                if (ReferenceEquals(_launchedBy, launcher))
                {
                    _launchedBy = null;
                    _launchedSessionId = null;
                }

                return _launchers.Remove(launcher);
            }
        }

        public IBridgeModuleLauncher Resolve(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return null;
            }

            lock (_gate)
            {
                foreach (var launcher in _launchers)
                {
                    if (launcher.CanLaunch(moduleId))
                    {
                        return launcher;
                    }
                }
            }

            return null;
        }

        public bool CanLaunch(string moduleId) => Resolve(moduleId) != null;

        public UniTask LaunchSessionAsync(BridgeSessionContext context, CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var launcher = Resolve(context.ModuleId);
            if (launcher == null)
            {
                throw new InvalidOperationException($"No launcher is registered for moduleId '{context.ModuleId}'.");
            }

            lock (_gate)
            {
                _launchedSessionId = context.SessionId;
                _launchedBy = launcher;
            }

            return launcher.LaunchSessionAsync(context, cancellationToken);
        }

        public async UniTask CancelSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            IBridgeModuleLauncher owner = null;
            List<IBridgeModuleLauncher> all;
            lock (_gate)
            {
                if (_launchedBy != null
                    && string.Equals(_launchedSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
                {
                    owner = _launchedBy;
                    _launchedBy = null;
                    _launchedSessionId = null;
                }

                all = new List<IBridgeModuleLauncher>(_launchers);
            }

            if (owner != null)
            {
                await owner.CancelSessionAsync(sessionId, cancellationToken);
                return;
            }

            // Not launched through this router yet (e.g. cancelled before Start): every launcher ignores unknown ids.
            foreach (var launcher in all)
            {
                await launcher.CancelSessionAsync(sessionId, cancellationToken);
            }
        }
    }
}
