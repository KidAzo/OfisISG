using Woi.VrBridge.Client.Abstractions;

namespace Woi.VrBridge.Client.Storage
{
    /// <summary>
    /// Creates the secure store used to persist the Firebase refresh token obtained from
    /// redeeming a Bridge session.authorize.command ticket. Never PlayerPrefs.
    /// </summary>
    public static class FirebaseAuthTokenStoreFactory
    {
        const string EditorDevFileName = "dev-credentials-firebase.bin";

        public static IDeviceCredentialStore Create()
        {
#if UNITY_EDITOR
            return new EditorDevCredentialStore(fileName: EditorDevFileName);
#elif UNITY_ANDROID
            return new AndroidKeystoreFirebaseAuthStore();
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
