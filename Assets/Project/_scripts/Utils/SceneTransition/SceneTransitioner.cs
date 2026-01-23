using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using WoiUtils;

namespace WoiUtils.SceneTransition
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class SceneTransitioner : PersistentSingleton<SceneTransitioner>
    {
        private Canvas TransitionCanvas;
        [SerializeField]
        private List<Transition> Transitions = new();
        
        private AsyncOperation LoadLevelOperation;
        private SceneTransitionSO ActiveTransition;
        private bool isLoading = false;
        public static Action OnSceneLoaded;
        public static Action OnSceneChanging;
        
        protected override void Awake()
        {
            base.Awake();
            SceneManager.activeSceneChanged += HandleSceneChange;
            
            TransitionCanvas = GetComponent<Canvas>();
            TransitionCanvas.enabled = false;
        }
        
        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= HandleSceneChange;
        }

        public void LoadScene(string Scene, 
            SceneTransitionMode TransitionMode = SceneTransitionMode.None, 
            LoadSceneMode Mode = LoadSceneMode.Single)
        {
            if (isLoading) return;

            isLoading = true;
            
            LoadLevelOperation = SceneManager.LoadSceneAsync(Scene);
            
            Transition transition = Transitions.Find(
                (transition) => transition.Mode == TransitionMode
            );
            if (transition != null)
            {
                LoadLevelOperation.allowSceneActivation = false;
                TransitionCanvas.enabled = true;
                ActiveTransition = transition.AnimationSO;
                Exit().Forget();
            }
            else
            {
                Debug.LogWarning($"No transition found for" +
                    $" TransitionMode {TransitionMode}!" +
                    $" Maybe you are misssing a configuration?");
                isLoading = false;
            }
        }

        private async UniTaskVoid Exit()
        {
            OnSceneChanging?.Invoke();
            await ActiveTransition.Exit(TransitionCanvas);
            LoadLevelOperation.allowSceneActivation = true;
        }

        private async UniTaskVoid Enter()
        {
            await ActiveTransition.Enter(TransitionCanvas);
            TransitionCanvas.enabled = false;
            LoadLevelOperation = null;
            ActiveTransition = null;
            isLoading = false;
            OnSceneLoaded?.Invoke();
        }

        private void HandleSceneChange(Scene OldScene, Scene NewScene)
        {
            if (ActiveTransition != null)
            {
                Enter().Forget();   
            }
        }

        [System.Serializable]
        public class Transition
        {
            public SceneTransitionMode Mode;
            public SceneTransitionSO AnimationSO;
        }
        
        public enum SceneTransitionMode
        {
            None,
            Fade,
            Circle
        }
    }
}
