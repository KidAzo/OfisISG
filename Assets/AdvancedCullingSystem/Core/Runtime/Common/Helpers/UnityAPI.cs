using System.Runtime.CompilerServices;
using UnityEngine;

namespace NGS.AdvancedCullingSystem
{
    public static class UnityAPI
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] FindObjectsOfType<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER

            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            
#else
            
            return Object.FindObjectsOfType<T>();
            
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FindObjectOfType<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER

            return Object.FindAnyObjectByType<T>();

#else

            return Object.FindObjectOfType<T>();
            
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RaycastCommand NewRaycastCommand(Vector3 origin, Vector3 direction, float distance = 5000, int layerMask = -1)
        {
#if UNITY_2022_2_OR_NEWER

            return new RaycastCommand(origin, direction, new QueryParameters(layerMask), distance);

#else
            
            return new RaycastCommand(origin, direction, distance, layerMask);

#endif
        }
    }
}
