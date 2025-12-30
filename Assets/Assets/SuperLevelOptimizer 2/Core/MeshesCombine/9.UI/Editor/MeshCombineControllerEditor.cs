using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using NGS.SLO.Shared;

namespace NGS.SLO.MeshesCombine
{
    [CustomEditor(typeof(MeshCombineController))]
    public class MeshCombineControllerEditor : Editor
    {
        private new MeshCombineController target
        {
            get
            {
                return base.target as MeshCombineController;
            }
        }
        private Section[] _sections;


        private void OnEnable()
        {
            _sections = new Section[]
            {
                new ObjectsSelectionSection(this),
                new CombineOptionsSection(this),
                new SourcesManagementSection(this),
                new CombinedObjectsOutputSection(this)
            };
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Meshes Combine", SLOGUI.TitleLabelStyle);
            SLOGUI.DrawSeparatorLine();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginScrollView(Vector2.zero, false, false, GUIStyle.none, GUIStyle.none, EditorStyles.inspectorDefaultMargins);

            EditorGUILayout.Space();

            for (int i = 0; i < _sections.Length; i++)
            {
                Section section = _sections[i];

                if (section.ShouldDraw())
                {
                    section.OnInspectorGUI();
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Combine"))
                target.CombineMeshes();

            EditorGUILayout.Space();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }


        private abstract class Section
        {
            protected string Label { get; set; }
            protected MeshCombineControllerEditor Parent { get; private set; }
            protected MeshCombineController Target
            {
                get
                {
                    return Parent.target;
                }
            }

            private AnimBool _foldout;


            public Section(MeshCombineControllerEditor parent)
            {
                Label = "Default Label";
                Parent = parent;

                _foldout = new AnimBool(false);
                _foldout.valueChanged.AddListener(parent.Repaint);
            }

            public virtual bool ShouldDraw()
            {
                return true;
            }

            public void OnInspectorGUI()
            {
                bool foldout = EditorGUILayout.Foldout(_foldout.target, Label, true, SLOGUI.FoldoutGUIStyle);

                if (_foldout.target != foldout)
                    OnFoldoutChanged(foldout);

                _foldout.target = foldout;

                if (EditorGUILayout.BeginFadeGroup(_foldout.faded))
                {
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                    DrawContent();
                }

                EditorGUILayout.EndFadeGroup();

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }


            protected virtual void OnFoldoutChanged(bool foldout)
            {
                
            }

            protected abstract void DrawContent();
        }

        private class ObjectsSelectionSection : Section
        {
            public ObjectsSelectionSection(MeshCombineControllerEditor parent)
                : base(parent)
            {
                Label = "Selection Tool";
            }

            protected override void DrawContent()
            {
                Target.DrawSelectedObjects = EditorGUILayout.Toggle("Draw Selected", Target.DrawSelectedObjects);

                EditorGUILayout.Space();

                Target.IncludeOnlyStatic = EditorGUILayout.Toggle("Include Only Static", Target.IncludeOnlyStatic);

                EditorGUILayout.Space();

                Target.IncludeMeshRenderers = EditorGUILayout.Toggle("Include MeshRenderers", Target.IncludeMeshRenderers);
                Target.IncludeSkinnedMeshRenderers = EditorGUILayout.Toggle("Include SkinnedMeshRenderers", Target.IncludeSkinnedMeshRenderers);
                Target.IncludeLODGroups = EditorGUILayout.Toggle("Include LODGroups", Target.IncludeLODGroups);

                EditorGUILayout.Space();
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Assigned Renderers: " + Target.SelectedRenderers.Count);
                EditorGUILayout.LabelField("Assigned LODGroups: " + Target.SelectedLODGroups.Count);

                EditorGUILayout.Space();
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();


                EditorGUILayout.BeginVertical();

                if (GUILayout.Button("Add Automatic"))
                    Target.AddObjectsAutomatic();

                if (GUILayout.Button("Remove All"))
                    Target.RemoveAllObjects();

                EditorGUILayout.EndVertical();


                EditorGUILayout.BeginVertical();

                if (GUILayout.Button("Add Selected"))
                    Target.AddSelectedObjects();

                if (GUILayout.Button("Remove Selected"))
                    Target.RemoveSelectedObjects();

                EditorGUILayout.EndVertical();


                EditorGUILayout.EndHorizontal();
            }
        }

        private class CombineOptionsSection : Section
        {
            public CombineOptionsSection(MeshCombineControllerEditor parent)
                : base(parent)
            {
                Label = "Combine Options";
            }

            protected override void DrawContent()
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.Space();

                Target.SplitInGroups = EditorGUILayout.Toggle("Split In Groups", Target.SplitInGroups);

                if (Target.SplitInGroups)
                {
                    Target.DrawGroups = EditorGUILayout.Toggle("Draw Groups", Target.DrawGroups);
                    Target.CellSize = EditorGUILayout.FloatField("Cell Size", Target.CellSize);
                }

                EditorGUILayout.Space();

                Target.Limit65kVertices = EditorGUILayout.Toggle("Limit 65k Vertices", Target.Limit65kVertices);
                Target.UseLightweightBuffers = EditorGUILayout.Toggle("Lightweight Buffers", Target.UseLightweightBuffers);
                Target.RebuildUV2 = EditorGUILayout.Toggle("Rebuild UV2", Target.RebuildUV2);

                EditorGUILayout.Space();

                Target.SaveAssets = EditorGUILayout.Toggle("Save Assets", Target.SaveAssets);

                if (Target.SaveAssets)
                {
                    EditorGUILayout.BeginHorizontal();

                    Target.DataOutputPath = EditorGUILayout.TextField("Output Path", Target.DataOutputPath);

                    if (GUILayout.Button("Browse"))
                    {
                        EditorApplication.delayCall += () =>
                        {
                            string path = OpenFolderPanel();

                            if (!string.IsNullOrEmpty(path))
                                Target.DataOutputPath = path;
                        };
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();

                if (Target.SplitInGroups)
                {
                    if (GUILayout.Button("Update Groups"))
                        Target.CreateBinaryTree();
                }

                if (!EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(Target);
            }

            private string OpenFolderPanel()
            {
                string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets/", "");

                return path;
            }
        }

        private class SourcesManagementSection : Section
        {
            private bool _sourcesEnabled;


            public SourcesManagementSection(MeshCombineControllerEditor parent)
                : base(parent)
            {
                Label = "Sources Management";
            }

            protected override void OnFoldoutChanged(bool foldout)
            {
                base.OnFoldoutChanged(foldout);

                Target.RefreshSceneSourcesInfo();
            }

            protected override void DrawContent()
            {
                EditorGUILayout.Space();

                if (Target.CombinedSourcesCount > 0)
                    Target.DrawSources = EditorGUILayout.Toggle("Draw Sources", Target.DrawSources);

                EditorGUILayout.Space();

                EditorGUILayout.LabelField($"Combined Sources: {Target.CombinedSourcesCount}");
                EditorGUILayout.LabelField($"Combine Errors: {Target.CombineErrorsCount}");

                EditorGUILayout.Space();

                if (Target.CombinedSourcesCount > 0)
                {
                    string buttonText = _sourcesEnabled ? "Disable Sources/Enable Combined" : "Enable Sources/Disable Combined";

                    if (GUILayout.Button(buttonText))
                    {
                        Target.SetSourcesEnable(_sourcesEnabled, !_sourcesEnabled);

                        _sourcesEnabled = !_sourcesEnabled;
                    }

                    if (GUILayout.Button("Add To Ignore All Sources With Errors"))
                        Target.AddToIgnoreAllSourcesWithError();

                    EditorGUILayout.Space();

                    if (GUILayout.Button("Clear Sources Info"))
                        Target.ClearSourcesInfo();

                    EditorGUILayout.Space();

                    if (GUILayout.Button("Destroy Sources"))
                        Target.DestroySourceComponents();
                 }
            }
        }

        private class CombinedObjectsOutputSection : Section
        {
            private bool _sourcesEnabled;


            public CombinedObjectsOutputSection(MeshCombineControllerEditor parent)
                : base(parent)
            {
                Label = "Combine Output";
            }

            protected override void OnFoldoutChanged(bool foldout)
            {
                base.OnFoldoutChanged(foldout);

                Target.RefreshSceneSourcesInfo();
            }

            protected override void DrawContent()
            {
                EditorGUILayout.Space();

                EditorGUILayout.LabelField($"Source Objects: {Target.SourceObjectsCount}");
                EditorGUILayout.LabelField($"Combined Objects: {Target.CombinedObjectsCount}");

                EditorGUILayout.Space();

                if (Target.CombinedObjectsCount > 0)
                {
                    string buttonText = _sourcesEnabled ? "Disable Sources/Enable Combined" : "Enable Sources/Disable Combined";

                    if (GUILayout.Button(buttonText))
                    {
                        Target.SetSourcesEnable(_sourcesEnabled, !_sourcesEnabled);

                        _sourcesEnabled = !_sourcesEnabled;
                    }

                    EditorGUILayout.Space();

                    if (GUILayout.Button("Destroy Combined Objects"))
                        Target.DestroyCombinedObjects();
                }

                EditorGUILayout.Space();
            }
        }
    }
}
