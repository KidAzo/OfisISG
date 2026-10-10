using System;
using UnityEngine;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Connection;

namespace Woi.OfficeFire.VrBridge
{
    /// <summary>
    /// Application-level access to the single Protocol V2 Bridge client instance and its module router.
    /// Module assemblies that cannot reference this assembly's launchers register theirs on <see cref="ModuleRouter"/>.
    /// </summary>
    public static class VrBridgeRuntime
    {
        public static IVrBridgeClient Client { get; private set; }
        public static VrBridgeClient ClientBehaviour { get; private set; }
        public static BridgeModuleLauncherRouter ModuleRouter { get; private set; }
        public static bool IsReady => Client != null;

        /// <summary>Raised after <see cref="Register"/>. Late subscribers should also check <see cref="IsReady"/>.</summary>
        public static event Action Registered;

        public static event Action Cleared;

        public static void Register(VrBridgeClient client, BridgeModuleLauncherRouter router = null)
        {
            ClientBehaviour = client;
            Client = client;
            ModuleRouter = router;
            Registered?.Invoke();
        }

        public static void Clear(VrBridgeClient client)
        {
            if (ClientBehaviour == client)
            {
                ClientBehaviour = null;
                Client = null;
                ModuleRouter = null;
                Cleared?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ClientBehaviour = null;
            Client = null;
            ModuleRouter = null;
            Registered = null;
            Cleared = null;
        }
    }
}
