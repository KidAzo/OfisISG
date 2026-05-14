using System.Collections.Generic;
using UnityEngine;

namespace WoiUtils.Pooling
{
	namespace Utils.Pooling
	{
		public static class PoolingRootManager
		{
			internal static GameObject root;
			public static readonly Dictionary<string, Transform> SubParents = new();

			public static Transform Root
			{
				get
				{
					if (root == null)
					{
						root = new GameObject("[Pooling_Root]");
						GameObject.DontDestroyOnLoad(root);
					}
					return root.transform;
				}
			}

			public static Transform GetSubParent(string name)
			{
				if (SubParents.TryGetValue(name, out var t))
					return t;

				var go = new GameObject(name);
				go.transform.SetParent(Root);
				SubParents[name] = go.transform;
				return go.transform;
			}
		}
	}

}
