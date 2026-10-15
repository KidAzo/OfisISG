using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Log-only Quest evidence. Does not activate, deactivate, or retarget anything.
    /// </summary>
    public static class OfficeFireRuntimeDiagnostics
    {
        const string OfficeSceneName = "FireModule_Office";

        static bool _loginLogged;
        static bool _gameplayLogged;
        static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _loginLogged = false;
            _gameplayLogged = false;
            _hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallSceneHook()
        {
            if (_hooked)
                return;

            _hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                    OnSceneLoaded(scene, LoadSceneMode.Single);
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.IsValid() && scene.name != null &&
                scene.name.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log("[FireTrace] sceneLoaded name=" + scene.name + " mode=" + mode);
            }

            if (!IsOfficeScene(scene))
                return;

            LogGameplay();
        }

        public static void LogLogin()
        {
            if (_loginLogged)
                return;

            _loginLogged = true;
            string body = BuildBody(includeRenderingCount: false);
            Debug.Log("[FireLoginRuntime]\n" + body);
            Debug.Log("[FireLoginPlatformState]\n" + body);
            LogCameras("[FireLoginRuntime]");
            LogNamedObjects("[FireLoginRuntime]");
        }

        public static void LogLoginAborted(string reason)
        {
            Debug.Log("[FireLoginRuntime]\naborted=true\nreason=" + reason + "\n" + BuildBody(includeRenderingCount: false));
        }

        public static void LogGameplay()
        {
            if (_gameplayLogged)
                return;

            _gameplayLogged = true;
            string body = BuildBody(includeRenderingCount: true);
            Debug.Log("[FireGameplayRuntime]\n" + body);
            Debug.Log("[FirePlatformState]\n" + body);
            LogCameras("[FireGameplayRuntime]");
            LogNamedObjects("[FireGameplayRuntime]");
        }

        static bool IsOfficeScene(Scene scene)
        {
            return scene.IsValid()
                   && string.Equals(scene.name, OfficeSceneName, StringComparison.Ordinal);
        }

        static string BuildBody(bool includeRenderingCount)
        {
            bool isVr = FirePlatformRuntime.IsVR;
            AppMode appMode = FirePlatformRuntime.CurrentMode;
            string raw = ReadStaticString("WOI.Modules.SDK.Data.ModuleLaunchRuntime", "RawHubPlatformId");
            string normalized = ReadStaticString("WOI.Modules.SDK.Data.ModuleLaunchRuntime", "HubPlatformId");
            string hasHub = ReadStaticString("WOI.Modules.SDK.Data.ModuleLaunchRuntime", "HasHubPlatform");
            if (raw == "<missing-type>" || raw == "<missing-property>")
                raw = FirePlatformRuntime.RawHubPlatform;

            bool hubLaunch = string.Equals(hasHub, "True", StringComparison.OrdinalIgnoreCase)
                             || ReadStaticBool("StandalonePlatform", "HasHubOverride");

            var builder = new StringBuilder();
            builder.Append("scene=").Append(SceneManager.GetActiveScene().name).Append('\n');
            builder.Append("loadedScenes=").Append(LoadedSceneNames()).Append('\n');
            builder.Append("platform=").Append(isVr ? "VR" : "PC").Append('\n');
            builder.Append("appMode=").Append(appMode).Append('\n');
            builder.Append("hubLaunch=").Append(hubLaunch ? "true" : "false").Append('\n');
            builder.Append("preparedPlatform=").Append(raw).Append('\n');
            builder.Append("preparedPlatformNormalized=").Append(normalized).Append('\n');
            builder.Append("isAndroid=").Append(Application.platform == RuntimePlatform.Android ? "true" : "false").Append('\n');
            builder.Append("xrLoaderActive=").Append(XRSettings.enabled ? "true" : "false").Append('\n');
            builder.Append("xrDeviceActive=").Append(XRSettings.isDeviceActive ? "true" : "false").Append('\n');
            builder.Append("loadedDevice=").Append(string.IsNullOrEmpty(XRSettings.loadedDeviceName) ? "<none>" : XRSettings.loadedDeviceName).Append('\n');
            builder.Append("pcRootActive=").Append(AnyNamedActiveInHierarchy("PC-Player") ? "true" : "false").Append('\n');
            builder.Append("pcCameraEnabled=").Append(NamedCameraRendering("PlayerCamera") ? "true" : "false").Append('\n');
            builder.Append("xrRootActive=").Append(AnyNamedActiveInHierarchy("-------------XR------------") ? "true" : "false").Append('\n');
            builder.Append("xrCameraEnabled=").Append(NamedCameraRendering("Main Camera") ? "true" : "false");
            if (includeRenderingCount)
            {
                builder.Append('\n');
                builder.Append("activeRenderingCameraCount=").Append(CountRenderingCameras());
            }

            return builder.ToString();
        }

        static void LogCameras(string tag)
        {
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            int logged = 0;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || !camera.gameObject.scene.IsValid() || !camera.gameObject.scene.isLoaded)
                    continue;

                AudioListener listener = camera.GetComponent<AudioListener>();
                Debug.Log(
                    tag + "\n" +
                    "cameraPath=" + PathOf(camera.transform) + "\n" +
                    "scene=" + camera.gameObject.scene.name + "\n" +
                    "activeSelf=" + camera.gameObject.activeSelf.ToString().ToLowerInvariant() + "\n" +
                    "activeInHierarchy=" + camera.gameObject.activeInHierarchy.ToString().ToLowerInvariant() + "\n" +
                    "enabled=" + camera.enabled.ToString().ToLowerInvariant() + "\n" +
                    "tag=" + camera.gameObject.tag + "\n" +
                    "depth=" + camera.depth + "\n" +
                    "targetTexture=" + (camera.targetTexture == null ? "<none>" : camera.targetTexture.name) + "\n" +
                    "audioListenerEnabled=" + (listener == null ? "<none>" : listener.enabled.ToString().ToLowerInvariant()));
                logged++;
            }

            if (logged == 0)
                Debug.Log(tag + "\ncameraPath=<none>");
        }

        static void LogNamedObjects(string tag)
        {
            LogName(tag, "PC-Player");
            LogName(tag, "-------------XR------------");
            LogName(tag, "Main Camera");
            LogName(tag, "PlayerCamera");
            LogName(tag, "LoadingCamera");
            LogName(tag, "LoadingCameraPc");
            LogName(tag, "EventSystem");
            LogComponent(tag, "EventSystem");
            LogComponent(tag, "XRUIInputModule");
            LogComponent(tag, "InputSystemUIInputModule");
            LogHubLoginRig(tag);
        }

        static void LogName(string tag, string objectName)
        {
            bool found = false;
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null || !candidate.scene.IsValid() || !candidate.scene.isLoaded)
                    continue;
                if (!string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                    continue;

                found = true;
                Debug.Log(tag + "\n" + DescribeObject(candidate));
            }

            if (!found)
                Debug.Log(tag + "\nname=" + objectName + "\nstate=missing");
        }

        static void LogComponent(string tag, string typeName)
        {
            bool found = false;
            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.gameObject.scene.IsValid() || !behaviour.gameObject.scene.isLoaded)
                    continue;
                if (!string.Equals(behaviour.GetType().Name, typeName, StringComparison.Ordinal))
                    continue;

                found = true;
                Debug.Log(
                    tag + "\n" +
                    "component=" + typeName + "\n" +
                    DescribeObject(behaviour.gameObject) + "\n" +
                    "componentEnabled=" + behaviour.enabled.ToString().ToLowerInvariant());
            }

            if (!found)
                Debug.Log(tag + "\ncomponent=" + typeName + "\nstate=missing");
        }

        static void LogHubLoginRig(string tag)
        {
            bool found = false;
            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().Name != "HubLoginXrRig")
                    continue;
                if (!behaviour.gameObject.scene.IsValid() || !behaviour.gameObject.scene.isLoaded)
                    continue;

                found = true;
                LogSerializedRoot(tag, behaviour, "pcCameraRoot");
                LogSerializedRoot(tag, behaviour, "xrOriginRoot");
                LogSerializedRoot(tag, behaviour, "screenSpaceCanvasRoot");
            }

            if (!found)
                Debug.Log(tag + "\ncomponent=HubLoginXrRig\nstate=missing");
        }

        static void LogSerializedRoot(string tag, MonoBehaviour behaviour, string fieldName)
        {
            FieldInfo field = behaviour.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            GameObject root = field != null ? field.GetValue(behaviour) as GameObject : null;
            if (root == null)
            {
                Debug.Log(tag + "\nhubLoginField=" + fieldName + "\nstate=missing");
                return;
            }

            Debug.Log(tag + "\nhubLoginField=" + fieldName + "\n" + DescribeObject(root));
        }

        static string DescribeObject(GameObject candidate)
        {
            return
                "name=" + candidate.name + "\n" +
                "cameraPath=" + PathOf(candidate.transform) + "\n" +
                "scene=" + candidate.scene.name + "\n" +
                "activeSelf=" + candidate.activeSelf.ToString().ToLowerInvariant() + "\n" +
                "activeInHierarchy=" + candidate.activeInHierarchy.ToString().ToLowerInvariant();
        }

        static bool AnyNamedActiveInHierarchy(string objectName)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null || !candidate.scene.IsValid() || !candidate.scene.isLoaded)
                    continue;
                if (string.Equals(candidate.name, objectName, StringComparison.Ordinal) && candidate.activeInHierarchy)
                    return true;
            }

            return false;
        }

        static bool NamedCameraRendering(string objectName)
        {
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || !camera.gameObject.scene.IsValid() || !camera.gameObject.scene.isLoaded)
                    continue;
                if (!string.Equals(camera.gameObject.name, objectName, StringComparison.Ordinal))
                    continue;
                if (camera.enabled && camera.gameObject.activeInHierarchy)
                    return true;
            }

            return false;
        }

        static int CountRenderingCameras()
        {
            int count = 0;
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || !camera.gameObject.scene.IsValid())
                    continue;
                if (camera.enabled && camera.gameObject.activeInHierarchy && camera.targetTexture == null)
                    count++;
            }

            return count;
        }

        static string LoadedSceneNames()
        {
            var names = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                    names.Add(scene.name);
            }

            return names.Count == 0 ? "<none>" : string.Join(",", names);
        }

        static string PathOf(Transform transform)
        {
            var names = new List<string>();
            while (transform != null)
            {
                names.Add(transform.name);
                transform = transform.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        static string ReadStaticString(string typeName, string propertyName)
        {
            PropertyInfo property = FindProperty(typeName, propertyName);
            if (property == null)
                return typeName == "WOI.Modules.SDK.Data.ModuleLaunchRuntime" && FindType(typeName) == null
                    ? "<missing-type>"
                    : "<missing-property>";

            object value = property.GetValue(null);
            if (value == null)
                return "<null>";

            string text = value.ToString();
            return string.IsNullOrEmpty(text) ? "<empty>" : text;
        }

        static bool ReadStaticBool(string typeName, string propertyName)
        {
            PropertyInfo property = FindProperty(typeName, propertyName);
            if (property == null)
                return false;

            object value = property.GetValue(null);
            return value is bool flag && flag;
        }

        static PropertyInfo FindProperty(string typeName, string propertyName)
        {
            Type type = FindType(typeName);
            if (type == null)
                return null;

            return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        }

        static Type FindType(string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
