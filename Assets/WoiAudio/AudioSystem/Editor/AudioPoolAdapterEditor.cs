#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WoiUtils.AudioSystem.Editor
{
    [CustomEditor(typeof(AudioPoolAdapter))]
    public class AudioPoolAdapterEditor : UnityEditor.Editor
    {
        SerializedProperty voicePrefab;
        SerializedProperty config;

        void OnEnable()
        {
            voicePrefab = serializedObject.FindProperty("voicePrefab");
            config = serializedObject.FindProperty("config");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool allAssigned = voicePrefab.objectReferenceValue != null && 
                               config.objectReferenceValue != null;

            if (allAssigned)
            {
                // Show compact view when all assigned
                EditorGUILayout.HelpBox("Audio Pool Adapter is configured.", MessageType.None);
                
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
                    
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField("Voice Prefab", voicePrefab.objectReferenceValue, typeof(AudioVoice), false);
                        EditorGUILayout.ObjectField("Config", config.objectReferenceValue, typeof(AudioSystemConfig), false);
                    }
                }

                EditorGUILayout.Space(4);

                if (GUILayout.Button("Edit Configuration", GUILayout.Height(22)))
                {
                    // Show fields for editing
                    EditorPrefs.SetBool("AudioPoolAdapter_ShowFields", true);
                }

                if (EditorPrefs.GetBool("AudioPoolAdapter_ShowFields", false))
                {
                    EditorGUILayout.Space(4);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.PropertyField(voicePrefab);
                        EditorGUILayout.PropertyField(config);

                        if (GUILayout.Button("Hide", GUILayout.Height(20)))
                        {
                            EditorPrefs.SetBool("AudioPoolAdapter_ShowFields", false);
                        }
                    }
                }
            }
            else
            {
                // Show fields when not fully configured
                EditorGUILayout.HelpBox("Please assign the required references.", MessageType.Warning);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(voicePrefab);
                    EditorGUILayout.PropertyField(config);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
