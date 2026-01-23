#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WoiUtils.AudioSystem.Editor
{
    public class AudioSetupWizard : EditorWindow
    {
        bool dontDestroyOnLoad = true;

        int defaultCapacity = 10;
        int maxPoolSize = 100;

        AudioVoice voicePrefab; // optional: user can drag a prefab

        [MenuItem("Tools/Woi Audio/Setup Audio System")]
        public static void Open()
        {
            var w = GetWindow<AudioSetupWizard>("Setup Woi Audio");
            w.minSize = new Vector2(420, 260);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Woi Audio - Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Creates/Configures AudioSystem in the current scene (one-click).", EditorStyles.miniLabel);

            EditorGUILayout.Space(10);

            dontDestroyOnLoad = EditorGUILayout.ToggleLeft("DontDestroyOnLoad AudioSystem", dontDestroyOnLoad);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Pool Settings", EditorStyles.boldLabel);
            defaultCapacity = EditorGUILayout.IntField("Default Capacity", defaultCapacity);
            maxPoolSize = EditorGUILayout.IntField("Max Pool Size", maxPoolSize);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("AudioVoice Prefab (Optional)", EditorStyles.boldLabel);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                voicePrefab = (AudioVoice)EditorGUILayout.ObjectField(
                    new GUIContent("Voice Prefab", "If empty, will search for existing or create new."),
                    voicePrefab, typeof(AudioVoice), false);

                if (voicePrefab == null)
                {
                    if (GUILayout.Button("Find", GUILayout.Width(50)))
                    {
                        voicePrefab = FindExistingVoicePrefab();
                        if (voicePrefab != null)
                        {
                            EditorGUIUtility.PingObject(voicePrefab);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("AudioVoice Prefab", "No existing AudioVoice prefab found.\nOne will be created during setup.", "OK");
                        }
                    }
                }
            }

            if (voicePrefab != null)
            {
                EditorGUILayout.HelpBox($"Using: {AssetDatabase.GetAssetPath(voicePrefab)}", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Will auto-find or create AudioVoice prefab during setup.", MessageType.Info);
            }

            EditorGUILayout.Space(12);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Setup In Scene", GUILayout.Height(30)))
                {
                    Setup();
                }

                if (GUILayout.Button("Ping Existing", GUILayout.Height(30)))
                {
                    var sys = FindFirstObjectByType<AudioSystem>();
                    if (sys != null)
                    {
                        EditorGUIUtility.PingObject(sys.gameObject);
                        Selection.activeObject = sys.gameObject;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Woi Audio", "No AudioSystem found in the scene.", "OK");
                    }
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "After setup, add AudioTrigger components to your scene objects and create SoundDefinitions via the creator tool.",
                MessageType.Info);
        }

        void Setup()
        {
            // Sanity
            if (defaultCapacity < 0) defaultCapacity = 0;
            if (maxPoolSize < 1) maxPoolSize = 1;
            if (maxPoolSize < defaultCapacity) maxPoolSize = defaultCapacity;

            var sys = FindFirstObjectByType<AudioSystem>();
            var adapter = FindFirstObjectByType<AudioPoolAdapter>();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            // Create root if missing
            if (sys == null)
            {
                var go = new GameObject("AudioSystem");
                Undo.RegisterCreatedObjectUndo(go, "Create AudioSystem");

                sys = go.AddComponent<AudioSystem>();

                if (dontDestroyOnLoad)
                {
                    // Mark for runtime: you can implement this flag in AudioSystem if you prefer.
                    // For now we just set a name hint.
                    go.name = "AudioSystem (DDOL)";
                }
            }

            // Ensure adapter on same GO (recommended)
            if (adapter == null)
            {
                adapter = sys.gameObject.GetComponent<AudioPoolAdapter>();
                if (adapter == null)
                {
                    Undo.AddComponent<AudioPoolAdapter>(sys.gameObject);
                    adapter = sys.gameObject.GetComponent<AudioPoolAdapter>();
                }
            }
            else if (adapter.gameObject != sys.gameObject)
            {
                // Prefer to keep them together to avoid confusion.
                Debug.LogWarning("AudioPoolAdapter exists on a different GameObject. Consider moving it under AudioSystem.");
            }

            // Create voice prefab if none provided
            if (voicePrefab == null)
            {
                // First try to find existing prefab
                voicePrefab = FindExistingVoicePrefab();
                
                // If still null, create a new one
                if (voicePrefab == null)
                {
                    voicePrefab = CreateVoicePrefabIfMissing();
                }
            }

            // Apply settings into adapter via SerializedObject so private fields are supported
            var so = new SerializedObject(adapter);
            SetIfExists(so, "voicePrefab", voicePrefab);
            SetIfExists(so, "defaultCapacity", defaultCapacity);
            SetIfExists(so, "maxPoolSize", maxPoolSize);
            // collectionCheck/maxSoundInstances exists in your adapter but optional
            so.ApplyModifiedPropertiesWithoutUndo();

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);

            EditorGUIUtility.PingObject(sys.gameObject);
            Selection.activeObject = sys.gameObject;

            EditorUtility.DisplayDialog("Woi Audio", "Setup complete.\nAudioSystem is ready in this scene.", "OK");
        }

        AudioVoice CreateVoicePrefabIfMissing()
        {
            // Create a temporary GO, add AudioVoice + AudioSource, save as prefab in Assets/WoiAudio/Runtime/Resources or similar.
            // Minimal approach: save into Assets/WoiAudio/Runtime/Generated.
            const string folder = "Assets/WoiAudio/AudioSystem/Runtime/Prefabs/Generated";
            if (!AssetDatabase.IsValidFolder("Assets/WoiAudio")) AssetDatabase.CreateFolder("Assets", "WoiAudio");
            if (!AssetDatabase.IsValidFolder("Assets/WoiAudio/AudioSystem")) AssetDatabase.CreateFolder("Assets/WoiAudio", "AudioSystem");
            if (!AssetDatabase.IsValidFolder("Assets/WoiAudio/AudioSystem/Runtime")) AssetDatabase.CreateFolder("Assets/WoiAudio/AudioSystem", "Runtime");
            if (!AssetDatabase.IsValidFolder("Assets/WoiAudio/AudioSystem/Runtime/Prefabs")) AssetDatabase.CreateFolder("Assets/WoiAudio/AudioSystem/Runtime", "Prefabs");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/WoiAudio/AudioSystem/Runtime/Prefabs", "Generated");

            var temp = new GameObject("AudioVoice_Prefab");
            temp.AddComponent<AudioSource>();
            var v = temp.AddComponent<AudioVoice>();

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/AudioVoice.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);

            DestroyImmediate(temp);

            var voice = prefab.GetComponent<AudioVoice>();
            EditorGUIUtility.PingObject(prefab);
            return voice;
        }

        static void SetIfExists(SerializedObject so, string propName, Object value)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.objectReferenceValue = value;
        }

        static void SetIfExists(SerializedObject so, string propName, int value)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.intValue = value;
        }

        /// <summary>
        /// Searches for an existing AudioVoice prefab in the project.
        /// </summary>
        AudioVoice FindExistingVoicePrefab()
        {
            // Search in common locations first
            string[] searchFolders = new[]
            {
                "Assets/WoiAudio/AudioSystem/Runtime/Prefabs/Generated",
                "Assets/WoiAudio/AudioSystem/Runtime/Prefabs",
                "Assets/WoiAudio/Runtime/Prefabs",
                "Assets/WoiAudio",
                "Assets"
            };

            foreach (var folder in searchFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        var voice = prefab.GetComponent<AudioVoice>();
                        if (voice != null)
                        {
                            Debug.Log($"[AudioSetupWizard] Found existing AudioVoice prefab: {path}");
                            return voice;
                        }
                    }
                }
            }

            return null;
        }
    }
}
#endif
