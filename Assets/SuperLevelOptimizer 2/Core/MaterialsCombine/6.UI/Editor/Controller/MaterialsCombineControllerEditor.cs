using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using NGS.SLO.Shared;

namespace NGS.SLO.MaterialsCombine
{
    [CustomEditor(typeof(MaterialsCombineController))]
    public class MaterialsCombineControllerEditor : Editor
    {
        protected new MaterialsCombineController target
        {
            get
            {
                return base.target as MaterialsCombineController;
            }
        }
        private Section[] _sections;


        private void OnEnable()
        {
            _sections = new Section[]
            {
                new ObjectsSelectionSection(this),
                new ShaderPropertiesEditingSection(this),
                new CombineOptionsSection(this),
                new BackupOptionsSection(this),
                new MaterialsCombinedOutputSection(this)
            };
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("MaterialsCombine", SLOGUI.TitleLabelStyle);
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
                target.CombineMaterials();

            EditorGUILayout.Space();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }


        private abstract class Section
        {
            protected string Label { get; set; }
            protected MaterialsCombineControllerEditor Parent { get; private set; }
            protected MaterialsCombineController Target
            {
                get
                {
                    return Parent.target;
                }
            }

            private AnimBool _foldout;


            public Section(MaterialsCombineControllerEditor parent)
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
            public ObjectsSelectionSection(MaterialsCombineControllerEditor parent) 
                : base(parent)
            {
                Label = "Selection Tool";
            }

            protected override void DrawContent()
            {
                Target.ShowRenderers = EditorGUILayout.Toggle("Show Renderers", Target.ShowRenderers);

                EditorGUILayout.Space();

                Target.IncludeOnlyStatic = EditorGUILayout.Toggle("Include Only Static", Target.IncludeOnlyStatic);
                Target.IncludeMeshRenderers = EditorGUILayout.Toggle("Include MeshRenderers", Target.IncludeMeshRenderers);
                Target.IncludeSkinnedMeshRenderers = EditorGUILayout.Toggle("Include SkinnedMeshRenderers", Target.IncludeSkinnedMeshRenderers);

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Added Renderers: " + Target.Renderers.Count);

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();


                EditorGUILayout.BeginVertical();

                if (GUILayout.Button("Add Automatic"))
                    Target.AddAllRenderers();

                if (GUILayout.Button("Remove All"))
                    Target.RemoveAllRenderers();

                EditorGUILayout.EndVertical();


                EditorGUILayout.BeginVertical();

                if (GUILayout.Button("Add Selected"))
                    Target.AddSelectedRenderers();

                if (GUILayout.Button("Remove Selected"))
                    Target.RemoveSelectedRenderers();

                EditorGUILayout.EndVertical();


                EditorGUILayout.EndHorizontal();
            }
        }

        private class ShaderPropertiesEditingSection : Section
        {
            public ShaderPropertiesEditingSection(MaterialsCombineControllerEditor parent)
                : base(parent)
            {
                Label = "Shader Properties Edit";
            }

            public override bool ShouldDraw()
            {
                return Target.ShaderInfos != null && Target.ShaderInfos.Count > 0;
            }

            protected override void DrawContent()
            {
                IReadOnlyList<ShaderCombineInfo> shaderInfos = Target.ShaderInfos;

                EditorGUILayout.LabelField("Uniq Added Shaders: " + shaderInfos.Count);

                if (shaderInfos.Count == 0)
                    return;

                EditorGUILayout.Space();

                if (GUILayout.Button("Edit Shader Properties"))
                    ShadersCombineInfoEditorWindow.ShowWindow(Target.ShaderInfos);
            }
        }

        private class CombineOptionsSection : Section
        {
            public CombineOptionsSection(MaterialsCombineControllerEditor parent)
                : base(parent)
            {
                Label = "Combine Options";
            }

            protected override void DrawContent()
            {
                EditorGUI.BeginChangeCheck();

                Target.TexturePackerType = (TexturePackerType)EditorGUILayout.EnumPopup("Texture Packer:", Target.TexturePackerType);

                EditorGUILayout.Space();

                Target.MaxAtlasSize = (TextureSize)EditorGUILayout.EnumPopup("Max Atlas Size:", Target.MaxAtlasSize);
                Target.MaxTileTextureUpscaledSize = (TextureSize)EditorGUILayout.EnumPopup("Max Tile Upscale:", Target.MaxTileTextureUpscaledSize);
                Target.MaxTileTextureDownscale = EditorGUILayout.IntField("Max Tile Downscale:", Target.MaxTileTextureDownscale);
                Target.Padding = EditorGUILayout.IntField("Padding:", Target.Padding);
                Target.FillEmptyTextures = EditorGUILayout.Toggle("Fill Empty Textures:", Target.FillEmptyTextures);

                EditorGUILayout.Space();

                Target.SaveAssets = EditorGUILayout.Toggle("Save Assets:", Target.SaveAssets);

                if (Target.SaveAssets)
                {
                    EditorGUILayout.BeginHorizontal();

                    Target.DataOutputPath = EditorGUILayout.TextField("Output Path:", Target.DataOutputPath);

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

                if (!EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(Target);
            }

            private string OpenFolderPanel()
            {
                string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets/", "");

                return path;
            }
        }

        private class BackupOptionsSection : Section
        {
            public BackupOptionsSection(MaterialsCombineControllerEditor parent)
               : base(parent)
            {
                Label = "Backup Options";
            }

            protected override void DrawContent()
            {
                Target.BackupOption = (BackupOption)EditorGUILayout.EnumPopup("Backup Option:", Target.BackupOption);

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Revert All"))
                    Target.RevertAllBackups();

                if (GUILayout.Button("Destroy All"))
                    Target.DestroyAllBackups();

                EditorGUILayout.EndHorizontal();
            }
        }

        private class MaterialsCombinedOutputSection : Section
        {
            public MaterialsCombinedOutputSection(MaterialsCombineControllerEditor parent)
               : base(parent)
            {
                Label = "Materials Combined Output";
            }

            public override bool ShouldDraw()
            {
                return UnityAPI.FindObjectOfType<MaterialCombineOutput>() != null;
            }

            protected override void OnFoldoutChanged(bool foldout)
            {
                base.OnFoldoutChanged(foldout);

                Target.CalculateOutputDataInScene();
            }

            protected override void DrawContent()
            {
                EditorGUILayout.LabelField("Combined Instances: " + Target.CombinedMaterialInstancesCount);
                EditorGUILayout.LabelField("With Errors: " + Target.MaterialInstancesWithCombineErrorsCount);

                EditorGUILayout.Space();

                Target.ShowCombinedRenderers = EditorGUILayout.Toggle("Show Combined", Target.ShowCombinedRenderers);
                Target.ShowRenderersWithCombineErrors = EditorGUILayout.Toggle("Show With Errors", Target.ShowRenderersWithCombineErrors);

                EditorGUILayout.Space();

                if (GUILayout.Button("Destroy All Reports"))
                    Target.DestroyOutputData();
            }
        }
    }
}
