namespace Systems.SceneManagement
{
    /// <summary>
    /// Set by AddressablesStartupCacheGuard so bootstrap code can wait until Addressables.InitializeAsync completes.
    /// Use <see cref="NotifyStartupBeginning"/> / <see cref="NotifyStartupComplete"/> from any assembly — properties are read-only from outside.
    /// </summary>
    public static class AddressablesInitializationState
    {
        /// <summary>True once startup guard finished init attempt (success or failure).</summary>
        public static bool StartupGuardFinished { get; private set; }

        /// <summary>True only if InitializeAsync succeeded.</summary>
        public static bool InitializationSucceeded { get; private set; }

        public static void NotifyStartupBeginning()
        {
            StartupGuardFinished = false;
            InitializationSucceeded = false;
        }

        /// <param name="initializationSucceeded">False after timeout or failed InitializeAsync.</param>
        public static void NotifyStartupComplete(bool initializationSucceeded)
        {
            InitializationSucceeded = initializationSucceeded;
            StartupGuardFinished = true;
        }

        internal static void ResetForTests()
        {
            StartupGuardFinished = false;
            InitializationSucceeded = false;
        }
    }
}
