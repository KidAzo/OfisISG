using UnityEngine;
using Woi.Settings;
using Reflex.Attributes;
using Woi.Events;

public class GameInitializer : MonoBehaviour
{
	[SerializeField] string starterSceneName;
    [Inject] SceneLoader sceneLoader;
	
    void OnEnable()
    {
        EventBus.Subscribe<OnLogged>(OnLogged);
    }

	void OnDisable()
	{
		EventBus.Unsubscribe<OnLogged>(OnLogged);
	}

	async void OnLogged(OnLogged evt)
	{
		await sceneLoader.LoadScene(starterSceneName);
	}
}
