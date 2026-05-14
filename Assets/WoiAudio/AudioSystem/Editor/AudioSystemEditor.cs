#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WoiUtils.AudioSystem.Editor
{
    [CustomEditor(typeof(AudioSystem))]
    public class AudioSystemEditor : UnityEditor.Editor
    {
        SerializedProperty configProp;

        void OnEnable()
        {
            configProp = serializedObject.FindProperty("config");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var sys = (AudioSystem)target;

            DrawHeader(sys);
            EditorGUILayout.Space(6);

            DrawConfigField();
            EditorGUILayout.Space(6);

            DrawRuntimeButtons(sys);

            serializedObject.ApplyModifiedProperties();
        }

         void DrawHeader(AudioSystem sys)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Audio System", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Toggle("In Play Mode", Application.isPlaying);
                    EditorGUILayout.Toggle("Is Shutting Down", AudioSystem.IsShuttingDown);
                }
            }
        }


        void DrawConfigField()
        {
            // Hide when config is assigned (only show when null)
            if (configProp.objectReferenceValue == null)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(configProp, new GUIContent("Config"));
                    EditorGUILayout.HelpBox("Assign an AudioSystemConfig to enable the system.", MessageType.Info);
                }
            }
            else
            {
                // Optionally show the reference in a small info line:
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Config assigned (hidden)", EditorStyles.boldLabel);

                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("Config", configProp.objectReferenceValue, typeof(AudioSystemConfig), false);

                    EditorGUILayout.HelpBox("Config field is hidden when assigned (as requested).", MessageType.None);
                }
            }
        }

        void DrawRuntimeButtons(AudioSystem sys)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to use runtime controls.", MessageType.None);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUILayout.Button("Stop All");
                        GUILayout.Button("Stop All Instances (Selected Sound)");
                    }
                    return;
                }

                // Stop All
                if (GUILayout.Button("Stop All", GUILayout.Height(28)))
                {
                    sys.StopAll();
                }

                EditorGUILayout.Space(4);

                // Stop All Instances requires a SoundDefinition ref
                using (new EditorGUILayout.HorizontalScope())
                {
                    _selectedSound = (SoundDefinition)EditorGUILayout.ObjectField(
                        _selectedSound,
                        typeof(SoundDefinition),
                        false);

                    using (new EditorGUI.DisabledScope(_selectedSound == null))
                    {
                        if (GUILayout.Button("Stop All Instances", GUILayout.Height(24)))
                        {
                            sys.StopAllInstances(_selectedSound);
                        }
                    }
                }

                if (_selectedSound == null)
                    EditorGUILayout.HelpBox("Assign a SoundDefinition to stop only its instances.", MessageType.None);
            }
        }

        // ---- internal state (editor-only) ----
        SoundDefinition _selectedSound;

        // Active voice count: AudioSystem private fields, so we can only approximate without reflection.
        // If you want exact count, I can also show reflection-based count.
        int GetActiveVoicesCountSafe(AudioSystem sys)
        {
            // cheapest: can't access private activeOrder without reflection.
            return -1;
        }
    }
}
#endif
