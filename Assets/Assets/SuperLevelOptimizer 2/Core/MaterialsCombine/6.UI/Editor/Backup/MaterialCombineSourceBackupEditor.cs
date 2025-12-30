using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NGS.SLO.MaterialsCombine
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MaterialCombineSourceBackup))]
    public class MaterialCombineSourceBackupEditor : Editor
    {
        protected new MaterialCombineSourceBackup target
        {
            get
            {
                return base.target as MaterialCombineSourceBackup;
            }
        }

        private SerializedProperty _backupOptionProp;
        private SerializedProperty _backupCreatedProp;

        private bool _materialsFoldout;


        private void OnEnable()
        {
            _backupOptionProp = serializedObject.FindProperty("_backupOption");
            _backupCreatedProp = serializedObject.FindProperty("_backupCreated");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();

            DrawOptions();

            if (targets.Length == 1 && _backupCreatedProp.boolValue)
            {
                EditorGUILayout.Space();
                DrawMaterialsFoldout();
            }

            EditorGUILayout.Space();

            if (!_backupCreatedProp.hasMultipleDifferentValues)
                DrawButtons();

            serializedObject.ApplyModifiedProperties();
        }


        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Backup Created:", target.BackupCreated ? "Yes" : "No");

            EditorGUI.BeginDisabledGroup(target.BackupCreated);

            EditorGUILayout.PropertyField(_backupOptionProp, new GUIContent("Backup Option"));

            EditorGUI.EndDisabledGroup();
        }

        private void DrawMaterialsFoldout()
        {
            _materialsFoldout = EditorGUILayout.Foldout(_materialsFoldout, "Materials", true);

            if (_materialsFoldout)
            {
                var renderer = target.GetComponent<Renderer>();

                if (renderer == null)
                {
                    EditorGUILayout.HelpBox("Renderer not found", MessageType.Error);
                }
                else
                {
                    var sharedMaterials = renderer.sharedMaterials;

                    if (sharedMaterials == null || sharedMaterials.Length == 0)
                    {
                        EditorGUILayout.HelpBox("This renderer not contains any material", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.BeginVertical("box");

                        for (int i = 0; i < sharedMaterials.Length; i++)
                        {
                            var mat = sharedMaterials[i];

                            EditorGUILayout.BeginHorizontal();

                            EditorGUILayout.ObjectField($"Material {i}", mat, typeof(Material), false);

                            EditorGUI.BeginDisabledGroup(!target.BackupCreated || mat == null);

                            if (GUILayout.Button("Revert"))
                                target.RevertMaterial(mat);

                            EditorGUI.EndDisabledGroup();

                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.EndVertical();
                    }
                }
            }
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (!_backupCreatedProp.boolValue)
            {
                if (GUILayout.Button("Create Backup"))
                {
                    serializedObject.ApplyModifiedProperties();

                    foreach(var target in targets)
                        (target as MaterialCombineSourceBackup).CreateBackup();
                }
            }
            else
            {
                if (GUILayout.Button("Revert All"))
                {
                    foreach (var target in targets)
                        (target as MaterialCombineSourceBackup).RevertAll();
                }

                if (GUILayout.Button("Destroy Backup"))
                {
                    foreach (var target in targets)
                        (target as MaterialCombineSourceBackup).DestroyBackup();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
