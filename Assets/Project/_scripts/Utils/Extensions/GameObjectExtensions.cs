using UnityEngine;

public static class GameObjectExtensions 
{
    public static T GetOrAdd<T>(this GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }
        return component;
    }
    
    public static void SetActive(this GameObject gameObject) => gameObject.SetActive(true);
    public static void SetInactive(this GameObject gameObject) => gameObject.SetActive(false);
}

public static class MonobehaviourExtensions 
{
    public static void SetActive(this MonoBehaviour monoBehaviour) => monoBehaviour.gameObject.SetActive(true);
    public static void SetInactive(this MonoBehaviour monoBehaviour) => monoBehaviour.gameObject.SetActive(false);
}
