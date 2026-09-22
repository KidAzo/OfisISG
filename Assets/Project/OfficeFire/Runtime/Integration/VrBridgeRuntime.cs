using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Connection;

namespace Woi.OfficeFire.VrBridge
{
    /// <summary>
    /// Application-level access to the single Protocol V2 Bridge client instance.
    /// </summary>
    public static class VrBridgeRuntime
    {
        public static IVrBridgeClient Client { get; private set; }
        public static VrBridgeClient ClientBehaviour { get; private set; }
        public static bool IsReady => Client != null;

        public static void Register(VrBridgeClient client)
        {
            ClientBehaviour = client;
            Client = client;
        }

        public static void Clear(VrBridgeClient client)
        {
            if (ClientBehaviour == client)
            {
                ClientBehaviour = null;
                Client = null;
            }
        }
    }
}
