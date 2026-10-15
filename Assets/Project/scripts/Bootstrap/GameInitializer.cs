using System.Collections;
using UnityEngine;
using Woi.Events;
using Woi.HazardSystem.Compat;
using Woi.Settings;
using Woi.UI;

public class GameInitializer : MonoBehaviour
{
    [SerializeField] string starterSceneName = "Office";

    bool loading;

    void Awake()
    {
        HazardHubLaunch.ApplyEditorStandaloneFallback();
        EnsurePersistentGameManager();
    }

    void OnEnable()
    {
        EventBus.Register<HazardHuntLogged>(OnHazardHuntLogged);
    }

    void OnDisable()
    {
        EventBus.Deregister<HazardHuntLogged>(OnHazardHuntLogged);
    }

    void OnHazardHuntLogged(HazardHuntLogged evt)
    {
        if (loading)
            return;

        HazardPlatformUiDriver.MarkLoginCompleted();

        if (string.IsNullOrWhiteSpace(starterSceneName))
            starterSceneName = "Office";

        StartCoroutine(LoadStarterScene());
    }

    IEnumerator LoadStarterScene()
    {
        loading = true;
        var login = FindFirstObjectByType<LoginScreenController>(FindObjectsInactive.Include);
        var request = new HazardOfficeLoadRequest();
        System.Action<float> report = progress => login?.SetLoadProgress(progress);
        if (HazardHubLaunch.IsHubLaunch)
            yield return HazardOfficeStandaloneLoader.LoadAddressable(report, request);
        else
            yield return HazardOfficeStandaloneLoader.Load(starterSceneName, report, request);

        if (request.Succeeded)
        {
            if (HazardHubLaunch.IsHubLaunch)
                HazardHubLaunch.ApplyPlatformToLoadedPortingAssets();
            HazardHuntPlayerRigController.ApplyLoaded();
            yield break;
        }

        Debug.LogError("[GameInitializer] Hazard Office did not load. " + request.Error);
        login?.RecoverFromFailedLoad();
        loading = false;
    }

    static void EnsurePersistentGameManager()
    {
        var existing = FindFirstObjectByType<GameManager>();
        if (existing != null)
        {
            DontDestroyOnLoad(existing.gameObject);
            return;
        }

        var prefab = Resources.Load<GameObject>("GameManager");
        if (prefab != null)
        {
            Instantiate(prefab);
            return;
        }

        var host = new GameObject("GameManager");
        host.AddComponent<GameManager>();
    }
}
