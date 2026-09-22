using NUnit.Framework;
using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class ConnectionStateTests
    {
        [Test]
        public void ConnectionStates_IncludeProtocolV2Set()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(VrBridgeConnectionState), VrBridgeConnectionState.PairingRequired));
            Assert.IsTrue(System.Enum.IsDefined(typeof(VrBridgeConnectionState), VrBridgeConnectionState.Degraded));
            Assert.IsTrue(System.Enum.IsDefined(typeof(VrBridgeConnectionState), VrBridgeConnectionState.Offline));
            Assert.IsTrue(System.Enum.IsDefined(typeof(VrBridgeConnectionState), VrBridgeConnectionState.AuthenticationFailed));
            Assert.IsTrue(System.Enum.IsDefined(typeof(VrBridgeConnectionState), VrBridgeConnectionState.ProtocolRejected));
            Assert.IsTrue(System.Enum.IsDefined(typeof(VrBridgeConnectionState), VrBridgeConnectionState.BridgeFound));
        }
    }
}
