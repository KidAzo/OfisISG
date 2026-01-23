using System;
using UnityEngine;

namespace WoiUtils.SceneTransition
{
    public class SceneChanger : PersistentSingleton<SceneChanger>
	{
        public static Action BeforeOnSceneChanged;    
        
        public enum  Scenes
        {
            Initial = 0,
		}
       
        private void Start()
        {
            QualitySettings.vSyncCount = 0;
            //Application.targetFrameRate = 90;
            ChangeScene(Scenes.Initial);
        }

        public static void ChangeScene(Scenes scene)
        {
            BeforeOnSceneChanged?.Invoke();
            SceneTransitioner.Instance.LoadScene(scene.ToString(), SceneTransitioner.SceneTransitionMode.Circle);
        }
    }
}
