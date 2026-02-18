using Reflex.Attributes;
using UnityEngine;
using Woi.Porting;
using Woi.Settings;

namespace Woi.SceneTransition
{
    public class SceneTransitioner : MonoBehaviour
    {
        [Inject] ISceneLoaderService sceneTransitionService;
        [Inject] IPortingService  portingService;
        [SerializeField] string pcSceneName;
        [SerializeField] string xrSceneName;
        string currentSceneName;

        void Start()
        {
            currentSceneName = portingService.CurrentMode == AppMode.XR ? xrSceneName : pcSceneName;
        }

        public void StartTransition()
        {
            TransitionToScene();
        }
        
        async void TransitionToScene()
        {
            await sceneTransitionService.LoadScene(currentSceneName);
        } 
    }
}
