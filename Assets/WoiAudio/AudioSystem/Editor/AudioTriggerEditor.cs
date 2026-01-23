#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WoiUtils.AudioSystem.Editor
{
    [CustomEditor(typeof(AudioTrigger))]
    public class AudioTriggerEditor : UnityEditor.Editor
    {
        SerializedProperty sound;

        SerializedProperty fireMode;
        SerializedProperty triggerCooldown;
        SerializedProperty blockSameFrame;

        SerializedProperty spatialMode;
        SerializedProperty positionSource;
        SerializedProperty followTarget;

        SerializedProperty volumeMul;
        SerializedProperty pitchMul;

        SerializedProperty audioSystem;

        // Optional clip override (only if you added these fields)
        SerializedProperty overrideClipIndex;
        SerializedProperty clipIndex;

        SerializedProperty onPlayed;
        SerializedProperty onBlocked;

        void OnEnable()
        {
            sound = serializedObject.FindProperty("sound");

            fireMode = serializedObject.FindProperty("fireMode");
            triggerCooldown = serializedObject.FindProperty("triggerCooldown");
            blockSameFrame = serializedObject.FindProperty("blockSameFrame");

            spatialMode = serializedObject.FindProperty("spatialMode");
            positionSource = serializedObject.FindProperty("positionSource");
            followTarget = serializedObject.FindProperty("followTarget");

            volumeMul = serializedObject.FindProperty("volumeMul");
            pitchMul = serializedObject.FindProperty("pitchMul");

            audioSystem = serializedObject.FindProperty("audioSystem");

            // These two must exist if you implemented clip override in AudioTrigger
            overrideClipIndex = serializedObject.FindProperty("overrideClipIndex");
            clipIndex = serializedObject.FindProperty("clipIndex");

            onPlayed = serializedObject.FindProperty("onPlayed");
            onBlocked = serializedObject.FindProperty("onBlocked");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var t = (AudioTrigger)target;

            DrawHeader(t);

            EditorGUILayout.Space(6);

            DrawTargetSection();

            EditorGUILayout.Space(6);

            DrawTriggerSection(t);

            EditorGUILayout.Space(6);

            DrawSpatialSection();

            EditorGUILayout.Space(6);

            DrawClipOverrideSection();

            EditorGUILayout.Space(6);

            DrawMultipliersSection();

            EditorGUILayout.Space(6);

            DrawAudioSystemSection(t);

            EditorGUILayout.Space(6);

            DrawEventsSection();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawHeader(AudioTrigger t)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Audio Trigger", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Zero-code bridge from scene events to AudioSystem.Play()", EditorStyles.miniLabel);

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = Application.isPlaying;
                    var originalColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.3f, 0.5f, 1f);
                    if (GUILayout.Button("Play", GUILayout.Height(24)))
                    {
                        t.Play();
                    }
                    GUI.backgroundColor = originalColor;
                    GUI.enabled = true;
                }

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to test the sound.", MessageType.Info);
                }
            }
        }

        void DrawTargetSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(sound);
                if (sound.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("Assign a SoundDefinition to play.", MessageType.Warning);
                }
            }
        }

        void DrawTriggerSection(AudioTrigger t)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Trigger", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(fireMode);
                EditorGUILayout.PropertyField(triggerCooldown);
                EditorGUILayout.PropertyField(blockSameFrame);

                var mode = (AudioTrigger.FireMode)fireMode.enumValueIndex;

                // Requirements hints
                if (mode == AudioTrigger.FireMode.OnTriggerEnter || mode == AudioTrigger.FireMode.OnTriggerExit)
                {
                    var col = t.GetComponent<Collider>();
                    if (col == null)
                        EditorGUILayout.HelpBox("OnTrigger requires a Collider component.", MessageType.Info);
                    else if (!col.isTrigger)
                        EditorGUILayout.HelpBox("Collider is not marked as Trigger. Enable 'Is Trigger' to receive trigger events.", MessageType.Info);
                }

                if (mode == AudioTrigger.FireMode.OnCollisionEnter || mode == AudioTrigger.FireMode.OnCollisionExit)
                {
                    var col = t.GetComponent<Collider>();
                    if (col == null)
                        EditorGUILayout.HelpBox("OnCollision requires a Collider component.", MessageType.Info);

                    // Collision requires Rigidbody on one of the objects (Unity rule)
                    var rb = t.GetComponent<Rigidbody>();
                    if (rb == null)
                        EditorGUILayout.HelpBox("Collision events usually require a Rigidbody on this object or the other object.", MessageType.Info);
                }

                if (mode == AudioTrigger.FireMode.Manual)
                {
                    EditorGUILayout.HelpBox("Manual mode: call AudioTrigger.Play() from a UI Button or UnityEvent.", MessageType.None);
                }
            }
        }

        void DrawSpatialSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Spatial", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(spatialMode);

                var sm = (AudioTrigger.SpatialMode)spatialMode.enumValueIndex;

                if (sm == AudioTrigger.SpatialMode.WorldPosition)
                    EditorGUILayout.PropertyField(positionSource);

                if (sm == AudioTrigger.SpatialMode.FollowTransform)
                    EditorGUILayout.PropertyField(followTarget);
            }
        }

        void DrawClipOverrideSection()
        {
            // If the project doesn't have these fields yet, just skip.
            if (overrideClipIndex == null || clipIndex == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Clip Override (Optional)", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(overrideClipIndex);

                if (overrideClipIndex.boolValue)
                {
                    EditorGUILayout.PropertyField(clipIndex, new GUIContent("Clip Index"));
                    if (clipIndex.intValue < 0) clipIndex.intValue = 0;
                    EditorGUILayout.HelpBox("Plays a specific clip index from the SoundDefinition (ignores selection mode for this call).", MessageType.None);
                }
            }
        }

        void DrawMultipliersSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Multipliers", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(volumeMul);
                EditorGUILayout.PropertyField(pitchMul);
            }
        }

        void DrawAudioSystemSection(AudioTrigger t)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Audio System", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(audioSystem);

                // In edit mode we can't reliably FindFirstObjectByType in all cases, but we can still hint.
                if (audioSystem.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "AudioSystem is not assigned. At runtime, this trigger will try to find an AudioSystem in the scene.",
                        MessageType.Info);
                }

                // Play mode test
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Test Fire (Play Mode)", GUILayout.Height(28)))
                    {
                        t.PlayWithNoCooldown();
                    }
                }

                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Enter Play Mode to use Test Fire.", MessageType.None);
            }
        }

        void DrawEventsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Events (Optional)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(onPlayed);
                EditorGUILayout.PropertyField(onBlocked);
            }
        }
    }
}
#endif
