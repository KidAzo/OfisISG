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
       
    }
}
