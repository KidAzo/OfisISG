using UnityEngine;

namespace NGS.SLO.Shared
{
    public static class UnityAPI
    {
        public static T[] FindObjectsOfType<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER

            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            
#else
            
            return Object.FindObjectsOfType<T>();
            
#endif
        }

        public static T FindObjectOfType<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER

            return Object.FindAnyObjectByType<T>();

#else

            return Object.FindObjectOfType<T>();
            
#endif
        }
    }
}
