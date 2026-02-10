using System.Collections.Generic;
using System.Linq;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

public readonly struct AsyncOperationHandleGroup
{
	public readonly List<AsyncOperationHandle<SceneInstance>> Handles;

	public float Progress => Handles.Count == 0 ? 0 : Handles.Average(h => h.PercentComplete);
	public bool IsDone => Handles.Count == 0 || Handles.All(o => o.IsDone);

	public AsyncOperationHandleGroup(int initalCapacity)
	{
		Handles = new List<AsyncOperationHandle<SceneInstance>>(initalCapacity);
	}
}
