using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;


namespace NGS.SLO.Shared
{
    public class AssetsExporterEditorWindow : EditorWindow
    {
        [SerializeField]
        private List<Renderer> _renderers;

        private bool _includeOnlyStatic;
        private bool _includeMeshRenderers;
        private bool _includeSkinnedMeshRenderers;

        private bool _exportMeshes;
        private bool _exportMaterials;

        private string _dataOutputPath = "";

        private Vector2 _scrollPosition;

        private SerializedObject _serializedObject;
        private SerializedProperty _renderersProp;


        [MenuItem("Tools/NGSTools/Super Level Optimizer 2/Assets Exporter")]
        public static void CreateWindow()
        {
            var window = GetWindow<AssetsExporterEditorWindow>("Assets Creator");
           
            window.Show();
        }


        private void OnEnable()
        {
            _renderers = new List<Renderer>();

            _serializedObject = new SerializedObject(this);
            _renderersProp = _serializedObject.FindProperty("_renderers");

            _includeOnlyStatic = false;
            _includeMeshRenderers = true;
            _includeSkinnedMeshRenderers = true;

            _exportMeshes = true;
            _exportMaterials = true;

            _dataOutputPath = string.Format("Assets/slo_output/{0}_{1}", SceneManager.GetActiveScene().name.ToLower(), Mathf.Abs(SceneManager.GetActiveScene().GetHashCode()));
        }

        private void OnGUI()
        {
            _serializedObject.Update();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            SLOGUI.DrawUnderlinedText("Assets Exporter", SLOGUI.TitleLabelStyle);

            EditorGUILayout.Space();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_renderersProp, new GUIContent("Selected Renderers"));

            EditorGUILayout.Space();

            _includeOnlyStatic = EditorGUILayout.Toggle("Include Only Static", _includeOnlyStatic);
            _includeMeshRenderers = EditorGUILayout.Toggle("Include MeshRenderers", _includeMeshRenderers);
            _includeSkinnedMeshRenderers = EditorGUILayout.Toggle("Include SkinnedMeshRenderers", _includeSkinnedMeshRenderers);

            EditorGUILayout.Space();

            _exportMeshes = EditorGUILayout.Toggle("Export Meshes", _exportMeshes);
            _exportMaterials = EditorGUILayout.Toggle("Export Materials", _exportMaterials);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();


            EditorGUILayout.BeginVertical();

            if (GUILayout.Button("Add Automatic"))
                AddAutomatic();

            if (GUILayout.Button("Remove All"))
                RemoveAll();

            EditorGUILayout.EndVertical();


            EditorGUILayout.BeginVertical();

            if (GUILayout.Button("Add Selected"))
                AddSelected();

            if (GUILayout.Button("Remove Selected"))
                RemoveSelected();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Output Path:");
            EditorGUILayout.BeginHorizontal();

            string path = EditorGUILayout.TextField(_dataOutputPath);

            if (PathUtil.TryGetProjectRelative(path, out string relativePath))
                _dataOutputPath = relativePath;

            if (GUILayout.Button("Browse"))
            {
                EditorApplication.delayCall += () =>
                {
                    string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets/", "");

                    if (PathUtil.TryGetProjectRelative(path, out string relativePath))
                    {
                        _dataOutputPath = relativePath;
                    }
                    else
                    {
                        Debug.Log("Output folder must be in project");

                        _dataOutputPath = string.Format("Assets/slo_output/{0}_{1}", SceneManager.GetActiveScene().name.ToLower(), Mathf.Abs(SceneManager.GetActiveScene().GetHashCode()));
                    }
                };
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (GUILayout.Button("Export Selected"))
                ExportSelected();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            _serializedObject.ApplyModifiedProperties();
        }


        private void TryAddRenderer(Renderer renderer)
        {
            if (_renderers.Contains(renderer))
                return;

            if (!FilterRenderer(renderer))
                return;

            _renderers.Add(renderer);
        }

        private bool FilterRenderer(Renderer renderer)
        {
            if (_includeOnlyStatic && !renderer.gameObject.isStatic)
                return false;

            if (!_includeMeshRenderers && renderer is MeshRenderer)
                return false;

            if (!_includeSkinnedMeshRenderers && renderer is SkinnedMeshRenderer)
                return false;

            if (!renderer.TryGetSharedMesh(out _))
                return false;

            return true;
        }

        private void FilterRenderersList()
        {
            int i = 0;
            while (i < _renderers.Count)
            {
                if (!FilterRenderer(_renderers[i]))
                {
                    _renderers.RemoveAt(i);
                    continue;
                }

                i++;
            }
        }


        private void AddAutomatic()
        {
            int count = _renderers.Count;

            foreach (var renderer in UnityAPI.FindObjectsOfType<Renderer>())
            {
                TryAddRenderer(renderer);
            }

            count = _renderers.Count - count;

            Debug.Log("Added " + count + " new renderers");
        }

        private void AddSelected()
        {
            int count = _renderers.Count;

            foreach (var selectedGO in Selection.gameObjects)
            {
                foreach (var renderer in selectedGO.GetComponentsInChildren<Renderer>())
                {
                    TryAddRenderer(renderer);
                }
            }

            count = _renderers.Count - count;

            Debug.Log("Added " + count + " new renderers");
        }

        private void RemoveAll()
        {
            int count = _renderers.Count;

            _renderers.Clear();

            Debug.Log("Removed " + count + " renderers");
        }

        private void RemoveSelected()
        {
            int count = _renderers.Count;

            foreach (var selectedGO in Selection.gameObjects)
            {
                foreach (var renderer in selectedGO.GetComponentsInChildren<Renderer>())
                    _renderers.Remove(renderer);
            }

            count = count - _renderers.Count;

            Debug.Log("Removed " + count + " renderers");
        }

        private void ExportSelected()
        {
            if (!EditorUtility.DisplayDialog("Confirmation", "Export missing assets from selected objects?", "Yes", "Cancel"))
                return;

            FilterRenderersList();

            GameObject[] gosForExport = _renderers.Select(r => r.gameObject).ToArray();

            AssetsExporter.ExportMissingAssets(gosForExport, _dataOutputPath, _exportMeshes, _exportMaterials);
        }
    }
}
