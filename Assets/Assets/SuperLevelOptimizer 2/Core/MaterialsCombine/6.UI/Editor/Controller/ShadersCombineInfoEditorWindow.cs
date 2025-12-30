using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using NGS.SLO.Shared;


namespace NGS.SLO.MaterialsCombine
{
    public class ShadersCombineInfoEditorWindow : EditorWindow
    {
        private ShaderCombineInfoView[] _shaderInfoViews;
        private Vector2 _scrollPosition;


        public static void ShowWindow(IReadOnlyList<ShaderCombineInfo> shaderInfos)
        {
            var window = GetWindow<ShadersCombineInfoEditorWindow>("Shaders Combine Info Editor");

            window.Initialize(shaderInfos);
            window.Show();
        }


        private void Initialize(IReadOnlyList<ShaderCombineInfo> shaderInfos)
        {
            _shaderInfoViews = new ShaderCombineInfoView[shaderInfos.Count];

            for (int i = 0; i < shaderInfos.Count; i++)
                _shaderInfoViews[i] = new ShaderCombineInfoView(this, shaderInfos[i]);

            _shaderInfoViews = _shaderInfoViews.OrderByDescending(s => s.ShaderInfo.Shader.name).ToArray();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            SLOGUI.DrawUnderlinedText("Shader Properties Editor", SLOGUI.TitleLabelStyle);

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            foreach (var shaderView in _shaderInfoViews)
            {
                EditorGUI.BeginChangeCheck();

                shaderView.OnGUI();

                if (EditorGUI.EndChangeCheck())
                {
                    var controller = UnityAPI.FindObjectOfType<MaterialsCombineController>();

                    if (controller != null)
                        EditorUtility.SetDirty(controller);

                    else
                        EditorUtility.SetDirty(this);
                }

                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }


        private class ShaderCombineInfoView
        {
            public ShaderCombineInfo ShaderInfo
            {
                get
                {
                    return _shaderInfo;
                }
            }

            private ShaderCombineInfo _shaderInfo;

            private Dictionary<ShaderPropertyKind, List<int>> _shaderKindPropertiesList;
            private Dictionary<ShaderPropertyKind, bool> _shaderKindFoldouts;

            private string[] _textureNames;
            private Dictionary<string, int> _textureNameToIndex;

            private AnimBool _foldout;
            private bool _allIgnored = false;


            public ShaderCombineInfoView(ShadersCombineInfoEditorWindow window, ShaderCombineInfo shaderInfo)
            {
                _shaderInfo = shaderInfo;

                _foldout = new AnimBool(false);
                _foldout.valueChanged.AddListener(window.Repaint);

                GatherData();
            }

            public void OnGUI()
            {
                _foldout.target = EditorGUILayout.Foldout(_foldout.target, _shaderInfo.Shader.name, true, SLOGUI.FoldoutGUIStyle);

                if (EditorGUILayout.BeginFadeGroup(_foldout.faded))
                {
                    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                    DrawContent();

                    EditorGUILayout.Space();

                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("Reset"))
                        ResetShaderInfo();

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndFadeGroup();

                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }


            private void GatherData()
            {
                _shaderKindPropertiesList = new Dictionary<ShaderPropertyKind, List<int>>();
                _shaderKindFoldouts = new Dictionary<ShaderPropertyKind, bool>();
                _textureNameToIndex = new Dictionary<string, int>();

                List<string> textureNames = new List<string>();

                for (int i = 0; i < _shaderInfo.ShaderProperties.Count; i++)
                {
                    ShaderProperty property = _shaderInfo.ShaderProperties[i];

                    if (property.kind == ShaderPropertyKind.Texture2D)
                    {
                        textureNames.Add(property.name);

                        _textureNameToIndex.Add(property.name, textureNames.Count - 1);
                    }

                    if (!_shaderKindPropertiesList.ContainsKey(property.kind))
                    {
                        _shaderKindPropertiesList.Add(property.kind, new List<int>());
                        _shaderKindFoldouts.Add(property.kind, false);
                    }

                    _shaderKindPropertiesList[property.kind].Add(i);
                }

                _textureNames = textureNames.ToArray();
            }

            private void DrawContent()
            {
                _shaderInfo.AllowedCombine = EditorGUILayout.Toggle("Allow Combine:", _shaderInfo.AllowedCombine);

                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();

                int currentTextureIndex = _textureNameToIndex[_shaderInfo.MainTextureName];
                int newTextureIndex = EditorGUILayout.Popup("Main Texture:", currentTextureIndex, _textureNames);

                if (EditorGUI.EndChangeCheck())
                    _shaderInfo.SetMainTexture(_textureNames[newTextureIndex]);

                EditorGUILayout.Space();

                foreach (var enumValue in Enum.GetValues(typeof(ShaderPropertyKind)))
                {
                    ShaderPropertyKind shaderKind = (ShaderPropertyKind) enumValue;

                    if (!_shaderKindFoldouts.ContainsKey(shaderKind))
                        continue;

                    _shaderKindFoldouts[shaderKind] = EditorGUILayout.Foldout(_shaderKindFoldouts[shaderKind], shaderKind.ToString(), true);

                    if (_shaderKindFoldouts[shaderKind])
                    {
                        EditorGUI.indentLevel++;

                        foreach (var propertyIndex in _shaderKindPropertiesList[shaderKind])
                        {
                            ShaderProperty property = _shaderInfo.ShaderProperties[propertyIndex];

                            DrawShaderProperty(property, propertyIndex);
                        }

                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.Space();
                        EditorGUILayout.Space();

                        if (GUILayout.Button(_allIgnored ? "Include All" : "Ignore All"))
                        {
                            string dialogTitle = _allIgnored ? "Include All Properties" : "Ignore All Properties";
                            string dialogMessage = _allIgnored ?
                                "Are you sure you want to include all shader properties? This will overwrite your current manual selection." :
                                "Are you sure you want to ignore all shader properties? This will overwrite your current manual selection.";

                            if (EditorUtility.DisplayDialog(dialogTitle, dialogMessage, "Yes", "No"))
                            {
                                foreach (var propertyIndex in _shaderKindPropertiesList[shaderKind])
                                {
                                    ShaderProperty property = _shaderInfo.ShaderProperties[propertyIndex];

                                    property.ignore = !_allIgnored;

                                    _shaderInfo.SetPropertyValues(property, propertyIndex);
                                }
                                
                                _allIgnored = !_allIgnored;
                            }
                        }

                        GUILayout.FlexibleSpace();

                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Space();

                        EditorGUI.indentLevel--;
                    }
                }
            }

            private void DrawShaderProperty(ShaderProperty property, int propertyIndex)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"{property.name} ({property.kind})");

                EditorGUI.BeginChangeCheck();

                property.ignore = EditorGUILayout.ToggleLeft("Ignore", property.ignore);

                if (property.kind == ShaderPropertyKind.Texture2D)
                {
                    property.combine2DTexture = EditorGUILayout.ToggleLeft("Combine", property.combine2DTexture);
                }
                else if (property.kind != ShaderPropertyKind.Texture)
                {
                    property.threshold = EditorGUILayout.FloatField("Threshold:", property.threshold);
                }

                if (EditorGUI.EndChangeCheck())
                    _shaderInfo.SetPropertyValues(property, propertyIndex);

                EditorGUILayout.EndHorizontal();
            }

            private void ResetShaderInfo()
            {
                if (!EditorUtility.DisplayDialog("Confirmation", "Are you sure that you want to reset shader properties?", "Yes", "Cancel"))
                    return;

                _shaderInfo.ResetProperties();
            }
        }
    }
}
