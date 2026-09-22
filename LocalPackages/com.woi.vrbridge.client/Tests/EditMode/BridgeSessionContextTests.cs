using System;
using NUnit.Framework;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Protocol.Messages;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class BridgeSessionContextTests
    {
        [Test]
        public void BridgeSessionContext_ExposesParticipantFields()
        {
            var ctx = new BridgeSessionContext(
                "sess",
                "dev",
                "fire-training",
                "cmd",
                new SessionParticipantPayload { FullName = "Ada", PersonnelId = "P1" },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                DateTimeOffset.UtcNow,
                "conn",
                1);

            Assert.AreEqual("Ada", ctx.ParticipantFullName);
            Assert.AreEqual("P1", ctx.PersonnelId);
            Assert.AreEqual("fire-training", ctx.ModuleId);
            Assert.AreEqual("dev", ctx.DeviceId);
        }
    }
}
