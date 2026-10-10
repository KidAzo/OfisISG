using UnityEngine;
using Woi.OfficeFire.VrBridge;
using Woi.VrBridge.Client.Connection;

namespace Woi.WasteCollectionMode.VrBridge
{
    /// <summary>
    /// Registers the office-waste launcher on the shell's module router once the single Bridge client exists.
    /// The shell assembly cannot reference Waste, so registration happens from this side.
    /// </summary>
    public static class WasteBridgeInstaller
    {
        static BridgeModuleLauncherRouter _router;
        static WasteBridgeModuleLauncher _launcher;
        static WasteBridgeSessionAdapter _adapter;

        public static WasteBridgeModuleLauncher Launcher => _launcher;

        public static WasteBridgeSessionAdapter Adapter => _adapter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            VrBridgeRuntime.Registered -= OnBridgeRegistered;
            VrBridgeRuntime.Registered += OnBridgeRegistered;
            VrBridgeRuntime.Cleared -= Uninstall;
            VrBridgeRuntime.Cleared += Uninstall;

            if (VrBridgeRuntime.IsReady)
                OnBridgeRegistered();
        }

        static void OnBridgeRegistered()
        {
            BridgeModuleLauncherRouter router = VrBridgeRuntime.ModuleRouter;
            if (router == null || VrBridgeRuntime.Client == null || ReferenceEquals(router, _router))
                return;

            Uninstall();
            _adapter = new WasteBridgeSessionAdapter(VrBridgeRuntime.Client);
            _launcher = new WasteBridgeModuleLauncher(_adapter);
            router.Register(_launcher);
            _router = router;
            Debug.Log("[WasteBridge] office-waste launcher registered on the Bridge module router.");
        }

        static void Uninstall()
        {
            if (_router != null && _launcher != null)
                _router.Unregister(_launcher);

            if (_adapter != null)
            {
                _adapter.Reset();
                _adapter.Dispose();
            }

            _router = null;
            _launcher = null;
            _adapter = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _adapter?.Dispose();
            _router = null;
            _launcher = null;
            _adapter = null;
        }
    }
}
