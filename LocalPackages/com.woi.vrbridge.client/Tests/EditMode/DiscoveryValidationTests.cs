using System;
using NUnit.Framework;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Protocol;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class DiscoveryValidationTests
    {
        [Test]
        public void ManualOffer_RejectsLoopback_WhenNotAllowed()
        {
            Assert.Throws<InvalidOperationException>(() =>
                BridgeDiscoveryClient.CreateManualOffer("ws://127.0.0.1:17881/ws/device", allowLoopback: false));
        }

        [Test]
        public void ManualOffer_AllowsLoopback_ForEditorFallback()
        {
            var offer = BridgeDiscoveryClient.CreateManualOffer("ws://127.0.0.1:17881/ws/device", allowLoopback: true);
            Assert.AreEqual("ws://127.0.0.1:17881/ws/device", offer.DeviceGatewayUrl);
        }

        [Test]
        public void ManualOffer_RejectsWrongPath()
        {
            Assert.Throws<InvalidOperationException>(() =>
                BridgeDiscoveryClient.CreateManualOffer("ws://192.168.1.10:17881/wrong", allowLoopback: false));
        }

        [Test]
        public void GatewayDefaults_MatchProtocolV2()
        {
            Assert.AreEqual(17778, ProtocolConstants.DiscoveryPort);
            Assert.AreEqual(17881, ProtocolConstants.GatewayPort);
            Assert.AreEqual("/ws/device", ProtocolConstants.GatewayPath);
            Assert.AreEqual(2, ProtocolConstants.ProtocolVersion);
        }
    }
}
