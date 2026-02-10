using System;
using System.Collections.Generic;
using System.Linq;
using Eflatun.SceneReference;

namespace Woi.Settings
{
	[Serializable]
	public class SceneGroup 
	{
		public string GroupName = "New Scene Group";
		public List<SceneData> Scenes;
		public SceneSettings sceneSettings;
		public string FindSceneNameByType(SceneType sceneType)
		{
			return Scenes.FirstOrDefault(scene => scene.SceneType == sceneType)?.Reference.Name;
		}
	}

	[Serializable]
	public class SceneData
	{
		public SceneReference Reference;
		public string Name => Reference.Name;
		public SceneType SceneType;
	}

	public enum SceneType { ActiveScene, GameplayScene, EnvironmentScene, UI , KKD}

	public class SceneSettings
	{
		
	}
}

