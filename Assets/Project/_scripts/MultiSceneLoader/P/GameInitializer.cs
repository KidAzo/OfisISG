using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Woi.Events;
using Woi.UI;

public class GameInitializer : MonoBehaviour
{
    [SerializeField] string starterSceneName = "Office";

    bool loading;

    void Awake()
    {
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

        if (string.IsNullOrWhiteSpace(starterSceneName))
            starterSceneName = "Office";

        StartCoroutine(LoadStarterScene());
    }

    IEnumerator LoadStarterScene()
    {
        loading = true;
        var login = FindFirstObjectByType<LoginScreenController>(FindObjectsInactive.Include);
        var op = SceneManager.LoadSceneAsync(starterSceneName);

        if (op == null)
        {
            Debug.LogError($"[GameInitializer] Failed to load scene '{starterSceneName}'. Is it in Build Settings?");
            login?.RecoverFromFailedLoad();
            loading = false;
            yield break;
        }

        while (!op.isDone)
        {
            login?.SetLoadProgress(Mathf.Clamp01(op.progress / 0.9f));
            yield return null;
        }
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
