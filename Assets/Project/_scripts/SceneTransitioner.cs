using Reflex.Attributes;
using UnityEngine;
using Woi.Settings;

namespace Woi.SceneTransition
{
    public class SceneTransitioner : MonoBehaviour
    {
        [Inject] ISceneLoaderService sceneTransitionService;
        [SerializeField] string sceneName; 

        public void StartTransition()
        {
            TransitionToScene();
        }
        
        async void TransitionToScene()
        {
            await sceneTransitionService.LoadScene(sceneName);
        } 
    }
}
