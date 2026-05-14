#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace WoiUtils.AudioSystem.Editor
{
    [CustomEditor(typeof(SoundDefinition))]
    public class SoundDefinitionEditor : UnityEditor.Editor
    {
        SerializedProperty selectionMode;
        SerializedProperty clips;
        SerializedProperty noImmediateRepeat;

        SerializedProperty mixerGroup;

        SerializedProperty scheduleMode;
        SerializedProperty queueScope;

        SerializedProperty instanceMode;
        SerializedProperty reTriggerMode;

        SerializedProperty cooldown;
        SerializedProperty delayMode;
        SerializedProperty delay;
        SerializedProperty delayRange;

        SerializedProperty loop;
        SerializedProperty priority;
        SerializedProperty volume;
        SerializedProperty pitch;

        SerializedProperty spatialBlend;
        SerializedProperty minDistance;
        SerializedProperty maxDistance;
        SerializedProperty rolloffMode;

        SerializedProperty mute;
        SerializedProperty bypassEffects;
        SerializedProperty bypassListenerEffects;
        SerializedProperty bypassReverbZones;
        SerializedProperty panStereo;
        SerializedProperty reverbZoneMix;
        SerializedProperty dopplerLevel;
        SerializedProperty spread;
        SerializedProperty ignoreListenerVolume;
        SerializedProperty ignoreListenerPause;
        SerializedProperty protectedFromSteal;
        SerializedProperty suppressDuplicatesWhileQueued;
        SerializedProperty category;
        SerializedProperty useCustomCategory;
        SerializedProperty customCategoryKey;

        ReorderableList clipsList;

        int previewIndex = 0;

        void OnEnable()
        {
            selectionMode = serializedObject.FindProperty("selectionMode");
            clips = serializedObject.FindProperty("clips");
            noImmediateRepeat = serializedObject.FindProperty("noImmediateRepeat");

            category = serializedObject.FindProperty("category");
            mixerGroup = serializedObject.FindProperty("mixerGroup");

            scheduleMode = serializedObject.FindProperty("scheduleMode");
            queueScope = serializedObject.FindProperty("queueScope");

            instanceMode = serializedObject.FindProperty("instanceMode");
            reTriggerMode = serializedObject.FindProperty("reTriggerMode");

            cooldown = serializedObject.FindProperty("cooldown");
            delayMode = serializedObject.FindProperty("delayMode");
            delay = serializedObject.FindProperty("delay");
            delayRange = serializedObject.FindProperty("delayRange");

            loop = serializedObject.FindProperty("loop");
            priority = serializedObject.FindProperty("priority");
            volume = serializedObject.FindProperty("volume");
            pitch = serializedObject.FindProperty("pitch");

            spatialBlend = serializedObject.FindProperty("spatialBlend");
            minDistance = serializedObject.FindProperty("minDistance");
            maxDistance = serializedObject.FindProperty("maxDistance");
            rolloffMode = serializedObject.FindProperty("rolloffMode");

            mute = serializedObject.FindProperty("mute");
            bypassEffects = serializedObject.FindProperty("bypassEffects");
            bypassListenerEffects = serializedObject.FindProperty("bypassListenerEffects");
            bypassReverbZones = serializedObject.FindProperty("bypassReverbZones");
            panStereo = serializedObject.FindProperty("panStereo");
            reverbZoneMix = serializedObject.FindProperty("reverbZoneMix");
            dopplerLevel = serializedObject.FindProperty("dopplerLevel");
            spread = serializedObject.FindProperty("spread");
            ignoreListenerVolume = serializedObject.FindProperty("ignoreListenerVolume");
            ignoreListenerPause = serializedObject.FindProperty("ignoreListenerPause");
            protectedFromSteal = serializedObject.FindProperty("protectedFromSteal");
            suppressDuplicatesWhileQueued = serializedObject.FindProperty("suppressDuplicatesWhileQueued");

            category = serializedObject.FindProperty("category");
            useCustomCategory = serializedObject.FindProperty("useCustomCategory");
            customCategoryKey = serializedObject.FindProperty("customCategoryKey");

            BuildClipsList();
        }

void BuildClipsList()
{
    clipsList = new ReorderableList(serializedObject, clips, true, true, true, true);

    clipsList.drawHeaderCallback = rect =>
    {
        EditorGUI.LabelField(rect, "Clips (index / weight / delay)");
    };

    // ✅ Real dynamic height per element
    clipsList.elementHeightCallback = (index) =>
    {
        var element    = clips.GetArrayElementAtIndex(index);
        var clipProp   = element.FindPropertyRelative("clip");
        var weightProp = element.FindPropertyRelative("weight");
        var delayProp  = element.FindPropertyRelative("delay");

        float pad = 4f;
        float h =
            pad +
            EditorGUI.GetPropertyHeight(clipProp, true) + pad +
            EditorGUI.GetPropertyHeight(weightProp, true) + pad +
            EditorGUI.GetPropertyHeight(delayProp, true) + pad;

        return h;
    };

    clipsList.drawElementCallback = (rect, index, active, focused) =>
    {
        var element    = clips.GetArrayElementAtIndex(index);
        var clipProp   = element.FindPropertyRelative("clip");
        var weightProp = element.FindPropertyRelative("weight");
        var delayProp  = element.FindPropertyRelative("delay");

        float pad = 4f;

        rect.y += pad;
        rect.height -= pad;

        // --- Clip ---
        float h0 = EditorGUI.GetPropertyHeight(clipProp, true);
        var r0 = new Rect(rect.x, rect.y, rect.width, h0);
        EditorGUI.PropertyField(r0, clipProp, new GUIContent($"[{index:00}] Clip"), true);

        // --- Weight ---
        rect.y += h0 + pad;
        float h1 = EditorGUI.GetPropertyHeight(weightProp, true);
        var r1 = new Rect(rect.x, rect.y, rect.width, h1);

        bool weightEnabled = IsRandomMode();
        using (new EditorGUI.DisabledScope(!weightEnabled))
        {
            EditorGUI.PropertyField(r1, weightProp, new GUIContent("Weight"), true);
        }

        // --- Delay ---
        rect.y += h1 + pad;
        float h2 = EditorGUI.GetPropertyHeight(delayProp, true);
        var r2 = new Rect(rect.x, rect.y, rect.width, h2);
        var prevColor = GUI.color;
        GUI.color = Color.cyan;
        EditorGUI.PropertyField(r2, delayProp, new GUIContent("Delay (sec)"), true);
        GUI.color = prevColor;

        // clamps
        if (weightProp.floatValue < 0f) weightProp.floatValue = 0f;
        if (delayProp.floatValue < 0f)  delayProp.floatValue  = 0f;
    };
}



        bool IsRandomMode()
        {
            // ClipSelectionMode enum must exist in your runtime.
            // We use enum index safely.
            // Expected: Single, Sequence, RandomWeighted
            var mode = (ClipSelectionMode)selectionMode.enumValueIndex;
            return mode == ClipSelectionMode.RandomWeighted;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var s = (SoundDefinition)target;
            DrawWarnings(s);
            ApplyAutoFixes(s);

            EditorGUILayout.Space(6);

            DrawClipsBox(s);

            EditorGUILayout.Space(6);

            DrawRoutingBox();

            EditorGUILayout.Space(6);

            DrawSchedulingBox();

            EditorGUILayout.Space(6);

            DrawInstanceBox();

            EditorGUILayout.Space(6);

            DrawTimingBox(s);

            EditorGUILayout.Space(6);

            DrawPlaybackBox();

            EditorGUILayout.Space(6);

            Draw3DBox(s);

            EditorGUILayout.Space(6);

            DrawAdvancedBox();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawWarnings(SoundDefinition s)
        {
            if (s.clips == null || s.clips.Count == 0)
                EditorGUILayout.HelpBox("No clips assigned. This sound will not play (quiet).", MessageType.Warning);

            if (s.cooldown < 0f)
                EditorGUILayout.HelpBox("Cooldown is negative. It should be >= 0.", MessageType.Warning);

            if (s.delay < 0f)
                EditorGUILayout.HelpBox("Delay is negative. It should be >= 0.", MessageType.Warning);

            if (s.delayMode == DelayMode.RandomRange && s.delayRange.y < s.delayRange.x)
                EditorGUILayout.HelpBox("Delay Range is inverted (Y < X).", MessageType.Warning);

            if (s.maxDistance < s.minDistance)
                EditorGUILayout.HelpBox("Max Distance is smaller than Min Distance.", MessageType.Warning);

            if (s.spatialBlend > 0.99f && s.maxDistance <= 10f)
            {
                EditorGUILayout.HelpBox(
                    "Spatial Blend is full 3D and Max Distance is very small — the listener must be within a few world units or this sound is effectively silent.",
                    MessageType.Info);
            }

            if (s.loop && s.scheduleMode == ScheduleMode.Queue)
                EditorGUILayout.HelpBox("Loop + Queue: queued loop sounds will never finish unless stopped. Ensure this is intended.", MessageType.Info);

            if (!IsRandomMode() && s.noImmediateRepeat)
                EditorGUILayout.HelpBox("No Immediate Repeat only applies to Random selection mode.", MessageType.None);


            for (int i = 0; i < s.clips.Count; i++)
            {
                if (s.clips[i].clip == null)
                {
                    EditorGUILayout.HelpBox($"Clip [{i}] is missing (null). It will be ignored and may cause quiet plays.", MessageType.Warning);
                    break;
                }
            }

            if (Mathf.Approximately(s.pitch, 0f))
            {
                EditorGUILayout.HelpBox(
                    "Pitch is set to 0. This will result in silence.",
                    MessageType.Warning);
            }

            if (s.useCustomCategory && string.IsNullOrWhiteSpace(s.customCategoryKey))
                EditorGUILayout.HelpBox("Custom Category is enabled but key is empty.", MessageType.Warning);
        }

        void ApplyAutoFixes(SoundDefinition s)
        {
            bool changed = false;

            if (s.cooldown < 0f) { s.cooldown = 0f; changed = true; }
            if (s.delay < 0f)    { s.delay = 0f; changed = true; }

            if (s.delayMode == DelayMode.RandomRange && s.delayRange.y < s.delayRange.x)
            {
                (s.delayRange.x, s.delayRange.y) = (s.delayRange.y, s.delayRange.x);
                changed = true;
            }

            if (s.maxDistance < s.minDistance)
            {
                s.maxDistance = s.minDistance;
                changed = true;
            }

            if (s.clips != null)
            {
                foreach (var c in s.clips)
                {
                    if (c.weight < 0f)
                    {
                        c.weight = 0f;
                        changed = true;
                    }
                }
            }

            if (changed)
                EditorUtility.SetDirty(s);
        }



        void DrawClipsBox(SoundDefinition s)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(selectionMode);

                // Random-only option
                if (IsRandomMode())
                    EditorGUILayout.PropertyField(noImmediateRepeat, new GUIContent("No Immediate Repeat"));

                clipsList.DoLayoutList();

                DrawPreviewControls(s);
            }
        }

    void DrawSchedulingBox()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Scheduling", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(scheduleMode);

            // 🔴 Warning: Queue All + Immediate
            var sm = (ScheduleMode)scheduleMode.enumValueIndex;
            var sel = (ClipSelectionMode)selectionMode.enumValueIndex;

            if (sel == ClipSelectionMode.QueueAll && sm == ScheduleMode.Immediate)
            {
                // Option A: classic red "Error" helpbox
                EditorGUILayout.HelpBox(
                    "Queue All is selected, but Scheduling Mode is Immediate.\n" +
                    "Queue All requires sequential scheduling. Consider switching Scheduling Mode to Queue.",
                    MessageType.Error
                );

                // Option B (extra): make it even more red (optional)
                /*
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        "Queue All + Immediate may behave unexpectedly. Switch Scheduling Mode to Queue.",
                        EditorStyles.wordWrappedLabel);
                }
                GUI.backgroundColor = prev;
                */
            }

            // Queue scope only meaningful when schedule is Queue
            if (sm == ScheduleMode.Queue)
                EditorGUILayout.PropertyField(queueScope, new GUIContent("Queue Scope"));
        }
    }


        void DrawInstanceBox()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Instance", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(instanceMode);

                var im = (InstanceMode)instanceMode.enumValueIndex;
                if (im == InstanceMode.SingleGlobal)
                    EditorGUILayout.PropertyField(reTriggerMode);
                else if (im == InstanceMode.SinglePerCategory)
                    EditorGUILayout.HelpBox(
                        "When this plays, any other active voice with the same Audio Category (or same Custom Category Key) is stopped — including voices from other Sound Definition assets.",
                        MessageType.Info);
                else
                {
                    EditorGUILayout.HelpBox("Re-trigger Mode is only used for SingleGlobal sounds.", MessageType.None);
                    if (loop.boolValue)
                    {
                        EditorGUILayout.HelpBox(
                            "Loop + Multiple: each Play() starts another infinite loop until you stop that voice or the pool recycles it — " +
                            "stacked layers often sound like one smear, not separate hits. For overlapping one-shots (UI, impacts), turn Loop off. " +
                            "For continuous ambience, prefer one loop you start/stop from code instead of spamming Play().",
                            MessageType.Info);
                    }
                }

                EditorGUILayout.PropertyField(protectedFromSteal);
            }
        }


        void DrawTimingBox(SoundDefinition s)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(cooldown);

                EditorGUILayout.PropertyField(delayMode);
                var dm = (DelayMode)delayMode.enumValueIndex;

                if (dm == DelayMode.Fixed)
                    EditorGUILayout.PropertyField(delay);

                if (dm == DelayMode.RandomRange)
                    EditorGUILayout.PropertyField(delayRange);
            }
        }

        void DrawPlaybackBox()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(loop);

                EditorGUILayout.PropertyField(priority);
                EditorGUILayout.PropertyField(volume);
                EditorGUILayout.PropertyField(pitch);
            }
        }

        void Draw3DBox(SoundDefinition s)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("3D", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(spatialBlend);

                // If 2D, hide distance options by default (but still editable if you want)
                bool is3D = s.spatialBlend > 0.001f;
                using (new EditorGUI.DisabledScope(!is3D))
                {
                    EditorGUILayout.PropertyField(minDistance);
                    EditorGUILayout.PropertyField(maxDistance);
                    EditorGUILayout.PropertyField(rolloffMode);
                }

                if (!is3D)
                    EditorGUILayout.HelpBox("Spatial Blend is 0 (2D). Distance/Rolloff settings are not used.", MessageType.None);
            }
        }

        void DrawAdvancedBox()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Advanced (Optional)", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(mute);
                EditorGUILayout.PropertyField(bypassEffects);
                EditorGUILayout.PropertyField(bypassListenerEffects);
                EditorGUILayout.PropertyField(bypassReverbZones);

                EditorGUILayout.PropertyField(panStereo);
                EditorGUILayout.PropertyField(reverbZoneMix);
                EditorGUILayout.PropertyField(dopplerLevel);
                EditorGUILayout.PropertyField(spread);

                EditorGUILayout.PropertyField(ignoreListenerVolume);
                EditorGUILayout.PropertyField(ignoreListenerPause);
                EditorGUILayout.PropertyField(suppressDuplicatesWhileQueued);
            }
        }

        void DrawPreviewControls(SoundDefinition s)
        {
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Preview (Play Mode)", EditorStyles.boldLabel);

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to preview via the real AudioSystem pipeline.", MessageType.None);
                    return;
                }

                var sys = Object.FindFirstObjectByType<AudioSystem>();
                if (sys == null)
                {
                    EditorGUILayout.HelpBox("No AudioSystem found in the scene.", MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preview (Selected)", GUILayout.Height(24)))
                    {
                        int idx = Mathf.Max(0, clipsList.index);
                        sys.Play(s, PlayContext.WithClipIndex(idx, ignoreCooldowns: true));
                    }

                    if (GUILayout.Button("Stop All Instances", GUILayout.Height(24)))
                    {
                        sys.StopAllInstances(s);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    previewIndex = EditorGUILayout.IntField(new GUIContent("Clip Index"), previewIndex);
                    if (previewIndex < 0) previewIndex = 0;

                    using (new EditorGUI.DisabledScope(s.clips == null || previewIndex >= (s.clips?.Count ?? 0)))
                    {
                        if (GUILayout.Button("Preview Index", GUILayout.Height(22)))
                        {
                            sys.Play(s, PlayContext.WithClipIndex(previewIndex));
                        }
                    }
                }

                if (s.clips != null && s.clips.Count > 0 && previewIndex >= s.clips.Count)
                {
                    EditorGUILayout.HelpBox($"Index out of range. This sound has {s.clips.Count} clips.", MessageType.Info);
                }
            }
        }

        void DrawRoutingBox()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Category & Routing", EditorStyles.boldLabel);

                // Lock enum when custom category is enabled
                using (new EditorGUI.DisabledScope(useCustomCategory.boolValue))
                {
                    EditorGUILayout.PropertyField(category, new GUIContent("Category"));
                }

                EditorGUILayout.PropertyField(
                    useCustomCategory,
                    new GUIContent("Use Custom Category")
                );

                if (useCustomCategory.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        customCategoryKey,
                        new GUIContent("Custom Category Key")
                    );

                    // minor cleanup
                    customCategoryKey.stringValue =
                        customCategoryKey.stringValue?.Trim();
                }

                EditorGUILayout.PropertyField(mixerGroup);

                if (useCustomCategory.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        $"Using custom category key: \"{customCategoryKey.stringValue}\"",
                        MessageType.Info);
                }
            }
        }

    }
}
#endif
