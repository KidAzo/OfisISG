using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Woi.Settings;
using WoiUtils;
using WOI.Modules.SDK;
using WOI.Modules.SDK.Contracts;
using WOI.Modules.SDK.Data;

namespace Woi.Settings
{
    /// <summary>
    /// Hub entry point for the office-safety module. Loads the login scene group after
    /// <see cref="ISceneLoaderService"/> is registered (typically via FireServiceInstaller in Office_Boot).
    /// </summary>
    [Preserve]
    public sealed class OfficeFireBootstrapper : PersistentSingleton<OfficeFireBootstrapper>, IModuleBootstrap
    {
        [SerializeField]
        private string loginSceneGroupName = "OfficeFireModule_Login";

        [Tooltip("Same ScriptableEnumPortingVariable asset as SceneLoader / InputManager (Addressables key Managers/PortingVariable).")]
        [SerializeField]
        private ScriptableEnumPortingVariable portingSettings;

        private static int _initializeGate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _initializeGate = 0;
        }

        private void OnEnable()
        {
            ServiceLocator.Cleared += OnServiceLocatorCleared;
            ServiceLocator.Unregister<IModuleBootstrap>();
            ServiceLocator.Register<IModuleBootstrap>(this);
        }

        private void OnDisable()
        {
            ServiceLocator.Unregister<IModuleBootstrap>();
            ServiceLocator.Cleared -= OnServiceLocatorCleared;
        }

        protected override void Awake()
        {
            FirePlatformRuntime.TryInitialize(portingSettings);
            base.Awake();
        }

        private static void OnServiceLocatorCleared()
        {
            Interlocked.Exchange(ref _initializeGate, 0);
        }

        private void Start()
        {
            RunBootstrapWhenServicesReadyIfHubDidNot().Forget();
        }

        private async UniTaskVoid RunBootstrapWhenServicesReadyIfHubDidNot()
        {
            if (!await WaitForLiveSceneLoaderAsync(10f, destroyCancellationToken))
                return;

            await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken);

            await Initialize(new ModuleLaunchContext
            {
                ModuleId = "office-safety",
                TargetModule = null,
                Metadata = null,
            });
        }

        public async Task Initialize(ModuleLaunchContext context)
        {
            if (Interlocked.CompareExchange(ref _initializeGate, 1, 0) != 0)
                return;

            if (!await WaitForLiveSceneLoaderAsync(10f))
            {
                Debug.LogError("[OfficeFireBootstrapper] ISceneLoaderService is missing. Ensure FireServiceInstaller ran in Office_Boot.");
                Interlocked.Exchange(ref _initializeGate, 0);
                return;
            }

            if (!ServiceLocator.TryGet<ISceneLoaderService>(out var sceneLoader) || sceneLoader == null)
            {
                Debug.LogError("[OfficeFireBootstrapper] ISceneLoaderService is missing.");
                Interlocked.Exchange(ref _initializeGate, 0);
                return;
            }

            if (ShouldSkipHubSceneLoad(context))
            {
                Debug.Log(
                    "[OfficeFireBootstrapper] Hub launch: entry scene already loaded. Skipping login scene group load.");
                return;
            }

            await sceneLoader.LoadScene(loginSceneGroupName);
        }

        private bool ShouldSkipHubSceneLoad(ModuleLaunchContext context)
        {
            if (context?.TargetModule == null)
                return false;
            if (string.IsNullOrWhiteSpace(context.TargetModule.CatalogUrl))
                return false;
            if (string.IsNullOrWhiteSpace(context.TargetModule.EntrySceneKey))
                return false;

            var entryKey = context.TargetModule.EntrySceneKey.Trim();
            if (IsBootstrapEntryKey(entryKey))
                return false;

            if (string.Equals(entryKey, loginSceneGroupName, System.StringComparison.OrdinalIgnoreCase))
                return true;

            return true;
        }

        private static bool IsBootstrapEntryKey(string entryKey)
        {
            return entryKey.Equals("Office_Boot", System.StringComparison.OrdinalIgnoreCase)
                   || entryKey.IndexOf("Bootstrap", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static async UniTask<bool> WaitForLiveSceneLoaderAsync(float timeoutSeconds, CancellationToken cancellationToken = default)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (ServiceLocator.TryGet<ISceneLoaderService>(out var loader) && loader != null)
                    return true;

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            return ServiceLocator.TryGet<ISceneLoaderService>(out var finalLoader) && finalLoader != null;
        }
    }
}
