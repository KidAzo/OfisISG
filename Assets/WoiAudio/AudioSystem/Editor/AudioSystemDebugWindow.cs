using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;


namespace WoiUtils.AudioSystem.Editor
{
    /// <summary>
    /// Editor window that displays currently active sounds and queued sounds in Play Mode.
    /// Useful for debugging and monitoring the AudioSystem at runtime.
    /// </summary>
    public class AudioSystemDebugWindow : EditorWindow
    {
        // ======== Configuration ========
        // Default to 0.1s. Serialized to ensure it persists.
        [SerializeField] private float refreshInterval = 0.1f; 
        private const string WINDOW_TITLE = "Audio System Debug";

        // ======== State ========
        private Vector2 activeScrollPos;
        private Vector2 queueScrollPos;
        private double lastRepaintTime;
        
        // Cached snapshots (refreshed each repaint)
        private List<ActiveVoiceSnapshot> activeVoices = new();
        private List<QueuedSoundSnapshot> queuedSounds = new();
        
        // Multi-AudioSystem support
        private AudioSystem[] allAudioSystems;
        private AudioSystem selectedAudioSystem;
        private int selectedIndex = 0;

        // ======== Styles (lazy initialized) ========
        private GUIStyle headerStyle;
        private GUIStyle playingStyle;
        private GUIStyle stoppedStyle;
        private GUIStyle inactiveStyle;
        private GUIStyle runningStyle;
        private bool stylesInitialized;

        // ======== Menu Item ========
        [MenuItem("Tools/Woi Audio/Audio System Debug Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<AudioSystemDebugWindow>();
            window.titleContent = new GUIContent(WINDOW_TITLE);
            window.minSize = new Vector2(350, 400);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += EditorTick;

            wantsMouseMove = false; // Disable unnecessary mouse events
            autoRepaintOnSceneChange = true;
            RefreshData();                    
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= EditorTick;
        }

        private void OnFocus()
        {
            if (EditorApplication.isPlaying)
            {
                RefreshData();
                Repaint();
            }
        }

        private void OnBecameVisible()
        {
            if (EditorApplication.isPlaying)
            {
                RefreshData();
                Repaint();
            }
        }

        private void EditorTick()
        {
            if (!EditorApplication.isPlaying) return;

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - lastRepaintTime < refreshInterval)
                return;

            lastRepaintTime = currentTime;
            RefreshData();
            
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Clear cached references when exiting play mode
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                allAudioSystems = null;
                selectedAudioSystem = null;
                activeVoices.Clear();
                queuedSounds.Clear();
            }
            Repaint();
        }

        // Standard EditorWindow update loop (called ~30+ times per second when looking at window)
        private void Update()
        {
            if (!EditorApplication.isPlaying) return;

            // Ensure we don't repaint too often, but keep it responsive
            if (EditorApplication.timeSinceStartup - lastRepaintTime >= refreshInterval)
            {
                lastRepaintTime = EditorApplication.timeSinceStartup;
                RefreshData();
                Repaint();
            }
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 5)
            };

            playingStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.3f, 0.8f, 0.3f) } // Green
            };

            stoppedStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.8f, 0.3f, 0.3f) } // Red
            };
            
            inactiveStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }, // Gray
                fontStyle = FontStyle.Italic
            };

            runningStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.3f, 0.6f, 1f) } // Blue
            };

            stylesInitialized = true;
        }

        /// <summary>
        /// Finds all AudioSystems in the scene. 
        /// In Play Mode, it tries to recover references if lost.
        /// </summary>
        private void RefreshAudioSystems()
        {
            if (EditorApplication.isPlaying)
            {
                // Find ALL systems (including DontDestroyOnLoad)
                allAudioSystems = Object.FindObjectsByType<AudioSystem>(FindObjectsSortMode.None);
                
                // If nothing selected or selection invalid, pick first
                if (selectedAudioSystem == null || !selectedAudioSystem)
                {
                    if (allAudioSystems.Length > 0)
                    {
                        selectedAudioSystem = allAudioSystems[0];
                        selectedIndex = 0;
                    }
                }
            }
            else
            {
                allAudioSystems = null;
                selectedAudioSystem = null;
            }
        }

        /// <summary>
        /// Refreshes the snapshot data from the selected AudioSystem.
        /// </summary>
        private void RefreshData()
        {
            RefreshAudioSystems();

            activeVoices.Clear();
            queuedSounds.Clear();

            if (selectedAudioSystem == null) return;

            activeVoices.AddRange(selectedAudioSystem.GetActiveVoicesSnapshot());
            queuedSounds.AddRange(selectedAudioSystem.GetQueuedSoundsSnapshot());
        }

        private void OnGUI()
        {
            InitializeStyles();

            // ======== Not in Play Mode ========
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see active sounds and queues.", MessageType.Info);
                return;
            }

            // ======== AudioSystem Selection ========
            RefreshAudioSystems();
            
            if (allAudioSystems == null || allAudioSystems.Length == 0)
            {
                EditorGUILayout.HelpBox("No AudioSystem found in the scene.", MessageType.Warning);
                if (GUILayout.Button("Refresh")) RefreshData();
                return;
            }
            
            // If we have multiple systems, show a dropdown
            if (allAudioSystems.Length > 1)
            {
                string[] names = new string[allAudioSystems.Length];
                for (int i = 0; i < allAudioSystems.Length; i++)
                    names[i] = $"{allAudioSystems[i].name} (ID: {allAudioSystems[i].GetInstanceID()})";
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target System:", GUILayout.Width(100));
                
                int newIndex = EditorGUILayout.Popup(selectedIndex, names);
                if (newIndex != selectedIndex)
                {
                    selectedIndex = newIndex;
                    selectedAudioSystem = allAudioSystems[selectedIndex];
                    RefreshData();
                }
                EditorGUILayout.EndHorizontal();
            }

            // ======== Header Info ========
            var sys = selectedAudioSystem;
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField($"Active: {activeVoices.Count}", GUILayout.Width(100));
            EditorGUILayout.LabelField($"Queued: {GetTotalQueuedCount()}", GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshData();
            }
            
            // Refresh Rate Control
            EditorGUILayout.LabelField("Rate:", GUILayout.Width(35));
            refreshInterval = EditorGUILayout.DelayedFloatField(refreshInterval, GUILayout.Width(35));
            if (refreshInterval < 0.01f) refreshInterval = 0.01f;
            
            if (GUILayout.Button("Stop All", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                if(sys) sys.StopAll();
                RefreshData();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // ======== Active Sounds Section ========
            DrawActiveSoundsSection();

            EditorGUILayout.Space(10);

            // ======== Queued Sounds Section ========
            DrawQueuedSoundsSection(sys);
        }

        /// <summary>
        /// Draws the Active Sounds section.
        /// </summary>
        private void DrawActiveSoundsSection()
        {
            // Debug info header
            EditorGUILayout.LabelField($"🔊 Active Sounds (List count: {activeVoices.Count})", headerStyle);

            if (activeVoices.Count == 0)
            {
                EditorGUILayout.HelpBox("No active sounds found in snapshot.", MessageType.None);
                return;
            }

            activeScrollPos = EditorGUILayout.BeginScrollView(activeScrollPos, GUILayout.MaxHeight(200));

            for (int i = 0; i < activeVoices.Count; i++)
            {
                var voice = activeVoices[i];
                
                // Use a slightly different background for invalid entries
                GUIStyle style = EditorStyles.helpBox;
                if (voice.DebugStatus != "OK") 
                    EditorGUILayout.BeginVertical("box"); // Different look
                else
                    EditorGUILayout.BeginVertical(style);
                
                // Row 1: Name + Status
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{i}] {voice.SoundName}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                
                // Show explicit status string from our debug snapshot
                if (voice.DebugStatus != "OK")
                {
                    EditorGUILayout.LabelField(voice.DebugStatus, inactiveStyle, GUILayout.Width(100));
                }
                else
                {
                    var statusStyle = voice.IsPlaying ? playingStyle : stoppedStyle;
                    var statusText = voice.IsPlaying ? "▶ Playing" : "■ Stopped";
                    EditorGUILayout.LabelField(statusText, statusStyle, GUILayout.Width(70));
                }
                EditorGUILayout.EndHorizontal();

                // Only show details if it's actually valid
                if (voice.DebugStatus == "OK")
                {
                    // Row 2: Clip Info
                    EditorGUILayout.LabelField($"Clip: {voice.ClipName}", EditorStyles.miniLabel);

                    // Row 3: Flags
                    EditorGUILayout.BeginHorizontal();
                    
                    if (voice.IsLooping)
                        EditorGUILayout.LabelField("🔁 Loop", EditorStyles.miniLabel, GUILayout.Width(50));
                    
                    if (voice.SpatialBlend > 0.5f)
                        EditorGUILayout.LabelField("🎧 3D", EditorStyles.miniLabel, GUILayout.Width(35));
                    else
                        EditorGUILayout.LabelField("📢 2D", EditorStyles.miniLabel, GUILayout.Width(35));

                    if (voice.HasFollowTarget)
                        EditorGUILayout.LabelField("📍 Follow", EditorStyles.miniLabel, GUILayout.Width(50));

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws the Queued Sounds section.
        /// </summary>
        private void DrawQueuedSoundsSection(AudioSystem audioSystem)
        {
            EditorGUILayout.LabelField("📋 Queued Sounds", headerStyle);

            if (queuedSounds.Count == 0)
            {
                EditorGUILayout.HelpBox("No queued sounds.", MessageType.None);
                return;
            }

            queueScrollPos = EditorGUILayout.BeginScrollView(queueScrollPos, GUILayout.MaxHeight(200));

            foreach (var queuedSound in queuedSounds)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Row 1: Name + Count + Status
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(queuedSound.SoundName, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                
                EditorGUILayout.LabelField($"[{queuedSound.QueuedCount}]", GUILayout.Width(30));
                
                if (queuedSound.IsRunning)
                    EditorGUILayout.LabelField("⏳ Running", runningStyle, GUILayout.Width(70));
                else
                    EditorGUILayout.LabelField("⏸ Waiting", EditorStyles.miniLabel, GUILayout.Width(70));
                    
                EditorGUILayout.EndHorizontal();

                // Row 2: Controls
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Skip One", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    if (queuedSound.Sound != null)
                    {
                        audioSystem.SkipQueueOne(queuedSound.Sound);
                        // Immediate refresh to feel responsive
                        RefreshData(); 
                    }
                }
                
                if (GUILayout.Button("Clear Queue", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    if (queuedSound.Sound != null)
                    {
                        audioSystem.ClearQueue(queuedSound.Sound);
                        RefreshData();
                    }
                }
                
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        private int GetTotalQueuedCount()
        {
            int total = 0;
            foreach (var qs in queuedSounds)
                total += qs.QueuedCount;
            return total;
        }
    }
}