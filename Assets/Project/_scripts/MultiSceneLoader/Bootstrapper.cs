using Reflex.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using WoiUtils;

namespace Woi.Settings
{
	public class Bootstrapper : PersistentSingleton<Bootstrapper>
	{
		private const string BootstrapperSceneName = "Bootstrapper";
		
		[Inject] ISceneLoaderService sceneLoaderService;
		[SerializeField] string sceneName = "LoginScreen";

		// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		// private static async void Init()
		// {
		// 	await SceneManager.LoadSceneAsync(BootstrapperSceneName, LoadSceneMode.Single);
		// }

        void Start()
        {
			Debug.Log("Bootstrapper started. Loading initial scene...");	
            sceneLoaderService.LoadScene(sceneName);
        }
    }
}




