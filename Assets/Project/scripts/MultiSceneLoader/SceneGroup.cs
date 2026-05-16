using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
			return Scenes.FirstOrDefault(scene => scene.SceneType == sceneType)?.SceneName;
		}
	}

	[Serializable]
	public class SceneData
	{
		[Tooltip("Unity build scene name (must be in Build Settings). Used when Addressable Key is empty.")]
		public string SceneName;

		[Tooltip("When set, this entry loads via Addressables using this key instead of SceneManager.")]
		public string AddressableKey;

		public SceneType SceneType;

		/// <summary>Used for duplicate detection: Addressable key when set, otherwise build scene name.</summary>
		public string Name => string.IsNullOrEmpty(AddressableKey) ? SceneName : AddressableKey;
	}

	public enum SceneType { ActiveScene, GameplayScene, EnvironmentScene, UI, KKD }

	public class SceneSettings
	{
	}
}
