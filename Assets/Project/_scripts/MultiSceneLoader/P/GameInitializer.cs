using UnityEngine;
using Woi.Settings;
using Reflex.Attributes;

public class GameInitializer : MonoBehaviour
{
	[SerializeField] string starterSceneName;

    [Inject] SceneLoader sceneLoader;
	
	async void Start()
	{
		await sceneLoader.LoadScene(starterSceneName);
	}
}
