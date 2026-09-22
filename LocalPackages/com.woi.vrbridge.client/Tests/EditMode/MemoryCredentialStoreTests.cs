using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Woi.VrBridge.Client.Storage;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class MemoryCredentialStoreTests
    {
        [Test]
        public async Task WriteReadDelete_RoundTrips()
        {
            var store = new MemoryCredentialStore();
            Assert.IsFalse(store.HasToken);

            await store.WriteTokenAsync("secret-token", CancellationToken.None);
            Assert.IsTrue(store.HasToken);

            var read = await store.ReadTokenAsync(CancellationToken.None);
            Assert.AreEqual("secret-token", read);

            await store.DeleteTokenAsync(CancellationToken.None);
            Assert.IsFalse(store.HasToken);
        }
    }
}
