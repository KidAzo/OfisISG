using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Storage
{
    public static class DeviceCredentialStoreFactory
    {
        public static IDeviceCredentialStore Create()
        {
#if UNITY_EDITOR
            return new EditorDevCredentialStore();
#elif UNITY_ANDROID
            return new AndroidKeystoreCredentialStore();
#else
            return new MemoryCredentialStore();
#endif
        }

        public static IDeviceCredentialStore CreateForTests()
        {
            return new MemoryCredentialStore();
        }
    }
}
