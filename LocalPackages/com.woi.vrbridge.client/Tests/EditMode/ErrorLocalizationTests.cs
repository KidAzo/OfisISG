using NUnit.Framework;
using Woi.VrBridge.Client.Diagnostics;
using Woi.VrBridge.Client.Protocol;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class ErrorLocalizationTests
    {
        [Test]
        public void Localize_ReturnsTurkishForKnownCode()
        {
            var message = BridgeDiagnosticCatalog.Localize(ErrorCodes.DiscoveryTimeout, "tr");
            StringAssert.Contains("Bridge bulunamadı", message);
        }

        [Test]
        public void Localize_FallsBackToEnglish()
        {
            var message = BridgeDiagnosticCatalog.Localize(ErrorCodes.DiscoveryTimeout, "en");
            StringAssert.Contains("bridge not found", message.ToLowerInvariant());
        }
    }
}
