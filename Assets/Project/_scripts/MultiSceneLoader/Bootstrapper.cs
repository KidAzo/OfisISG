using UnityEngine;
using UnityEngine.SceneManagement;
using WoiUtils;

namespace Woi.Settings
{
	public class Bootstrapper : PersistentSingleton<Bootstrapper>
	{
		private const string BootstrapperSceneName = "Bootstrapper";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static async void Init()
		{
			await SceneManager.LoadSceneAsync(BootstrapperSceneName, LoadSceneMode.Single);
		}
	}
}




