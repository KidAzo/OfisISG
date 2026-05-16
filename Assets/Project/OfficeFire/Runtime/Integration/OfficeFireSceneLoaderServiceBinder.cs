using UnityEngine;
using Woi.Settings;
using WOI.Modules.SDK;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Optional helper: registers the scene's <see cref="SceneLoader"/> on <see cref="ServiceLocator"/> as <see cref="ISceneLoaderService"/>
    /// so <see cref="OfficeFireModuleBootstrapper"/> (and other code) can resolve it. Use when your bootstrap scene has no other registration.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class OfficeFireSceneLoaderServiceBinder : MonoBehaviour
    {
        [SerializeField]
        private SceneLoader sceneLoader;

        private void Awake()
        {
            if (sceneLoader == null)
            {
                sceneLoader = GetComponent<SceneLoader>();
            }

            if (sceneLoader == null)
            {
                Debug.LogError(
                    "[OfficeFireSceneLoaderServiceBinder] No SceneLoader assigned and none found on this GameObject — cannot register ISceneLoaderService.",
                    this);
                return;
            }

            if (ServiceLocator.TryGet<ISceneLoaderService>(out ISceneLoaderService existing) && existing != null)
            {
                if (ReferenceEquals(existing, sceneLoader))
                {
                    Debug.Log(
                        "[OfficeFireSceneLoaderServiceBinder] ISceneLoaderService already registered (same SceneLoader instance).",
                        this);
                    return;
                }

                Debug.LogWarning(
                    "[OfficeFireSceneLoaderServiceBinder] ISceneLoaderService is already registered with a different instance — not overwriting.",
                    this);
                return;
            }

            ServiceLocator.Register<ISceneLoaderService>(sceneLoader);
            Debug.Log(
                "[OfficeFireSceneLoaderServiceBinder] Registered SceneLoader as ISceneLoaderService on ServiceLocator.",
                this);
        }
    }
}
