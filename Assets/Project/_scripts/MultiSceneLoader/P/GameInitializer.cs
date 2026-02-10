using UnityEngine;
using Woi.Settings;
using Reflex.Attributes;

public class GameInitializer : MonoBehaviour
{
	[SerializeField] string starterSceneName;

    [Inject] private SceneLoader sceneLoader;
	
	private async void Start()
	{
		await sceneLoader.LoadScene(starterSceneName);
	}
}
