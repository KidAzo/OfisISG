using UnityEngine;
using UnityEngine.SceneManagement;

namespace Woi.OfficeFire
{
    public static class OfficeFireExtinguisherHudReference
    {
        public const string HudObjectName = "EstinguisherHUD";
        public const string HudObjectLegacyName = "Estinguisher-HUD";
        public const string UiRootObjectName = "05_UI";

        public static GameObject Resolve(GameObject cached)
        {
            if (cached != null)
            {
                return cached;
            }

            GameObject uiRoot = GameObject.Find(UiRootObjectName);
            if (uiRoot != null)
            {
                Transform hudTransform = uiRoot.transform.Find(HudObjectName);
                if (hudTransform != null)
                {
                    return hudTransform.gameObject;
                }

                hudTransform = uiRoot.transform.Find(HudObjectLegacyName);
                if (hudTransform != null)
                {
                    return hudTransform.gameObject;
                }
            }

            GameObject found = GameObject.Find(HudObjectName);
            if (found != null)
            {
                return found;
            }

            found = GameObject.Find(HudObjectLegacyName);
            if (found != null)
            {
                return found;
            }

            return FindInLoadedScenes(HudObjectName) ?? FindInLoadedScenes(HudObjectLegacyName);
        }

        private static GameObject FindInLoadedScenes(string objectName)
        {
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject candidate = allObjects[i];
                if (candidate == null || candidate.name != objectName)
                {
                    continue;
                }

                Scene scene = candidate.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }
    }
}
