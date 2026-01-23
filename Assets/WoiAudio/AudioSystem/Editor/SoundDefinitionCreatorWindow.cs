#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace WoiUtils.AudioSystem.Editor
{
    public class SoundDefinitionCreatorWindow : EditorWindow
    {
        enum Prefix { SFX, VO, AMB, MUS, Custom }

        Prefix prefix = Prefix.SFX;
        string customPrefix = "SFX";
        string baseName = "NewSound";
        DefaultAsset targetFolder;

        [MenuItem("Tools/Woi Audio/Create Sound Definition")]
        public static void Open()
        {
            var w = GetWindow<SoundDefinitionCreatorWindow>("Create SoundDefinition");
            w.minSize = new Vector2(420, 180);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("SoundDefinition Creator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Creates a SoundDefinition in the selected folder with a clean naming convention.", EditorStyles.miniLabel);

            EditorGUILayout.Space(8);

            prefix = (Prefix)EditorGUILayout.EnumPopup("Prefix", prefix);
            if (prefix == Prefix.Custom)
                customPrefix = EditorGUILayout.TextField("Custom Prefix", customPrefix);

            baseName = EditorGUILayout.TextField("Name", baseName);

            targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("Target Folder", "If empty, uses the currently selected folder in Project window."),
                targetFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Create", GUILayout.Height(28)))
            {
                CreateAsset();
            }
        }

        void CreateAsset()
        {
            string folderPath = GetFolderPath();
            if (string.IsNullOrEmpty(folderPath))
            {
                EditorUtility.DisplayDialog("Create SoundDefinition", "Please select a valid folder in the Project window.", "OK");
                return;
            }

            string p = prefix == Prefix.Custom ? customPrefix : prefix.ToString();
            p = string.IsNullOrWhiteSpace(p) ? "SFX" : p.Trim();

            string cleanName = string.IsNullOrWhiteSpace(baseName) ? "NewSound" : baseName.Trim();
            string fileName = $"{p}_{cleanName}.asset";

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, fileName));

            var asset = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        string GetFolderPath()
        {
            if (targetFolder != null)
            {
                var p = AssetDatabase.GetAssetPath(targetFolder);
                if (AssetDatabase.IsValidFolder(p)) return p;
            }

            // Use selected folder
            var obj = Selection.activeObject;
            if (obj == null) return null;

            var path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path)) return path;

            // If selection is an asset, use its folder
            return Path.GetDirectoryName(path)?.Replace("\\", "/");
        }
    }
}
#endif
