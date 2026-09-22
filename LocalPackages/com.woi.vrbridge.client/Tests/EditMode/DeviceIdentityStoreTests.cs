using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Woi.VrBridge.Client.Storage;

namespace Woi.VrBridge.Client.Tests.EditMode
{
    public sealed class DeviceIdentityStoreTests
    {
        string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "woi-vrbridge-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Test]
        public async Task GetOrCreateDeviceId_IsStable()
        {
            var store = new DeviceIdentityStore(_tempRoot);
            var id1 = await store.GetOrCreateDeviceIdAsync();
            var id2 = await store.GetOrCreateDeviceIdAsync();
            Assert.AreEqual(id1, id2);
            Assert.IsTrue(File.Exists(store.FilePath));
        }

        [Test]
        public async Task Reset_GeneratesNewId()
        {
            var store = new DeviceIdentityStore(_tempRoot);
            var id1 = await store.GetOrCreateDeviceIdAsync();
            await store.ResetAsync();
            var id2 = await store.GetOrCreateDeviceIdAsync();
            Assert.AreNotEqual(id1, id2);
        }
    }
}
