#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Woi.WasteCollectionMode.Editor
{
    public static class WasteBinIconLibraryBuilder
    {
        private const string LibraryPath = "Assets/Project/WasteCollection/UI/WasteBinIconLibrary.asset";
        private const string IconsFolder = "Assets/Project/WasteCollection/UI/IconsPng";

        private static readonly string[] IconKeys =
        {
            "file-text",
            "package",
            "beaker",
            "glass-water",
            "apple",
            "battery",
            "printer",
            "monitor",
            "disc",
            "cylinder",
            "flame",
            "trash-2",
            "triangle-alert",
            "briefcase-medical",
            "lightbulb",
        };

        [MenuItem("Waste Collection/Assign Lucide Bin Icons")]
        public static void AssignLucideBinIcons()
        {
            AssetDatabase.Refresh();

            WasteBinIconLibrary library = AssetDatabase.LoadAssetAtPath<WasteBinIconLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<WasteBinIconLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            SerializedObject serializedLibrary = new SerializedObject(library);
            SerializedProperty headerProp = serializedLibrary.FindProperty("headerIcon");
            SerializedProperty iconsProp = serializedLibrary.FindProperty("icons");

            headerProp.objectReferenceValue = LoadTexture("circle-check");
            iconsProp.arraySize = IconKeys.Length;

            for (int i = 0; i < IconKeys.Length; i++)
            {
                SerializedProperty entry = iconsProp.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("iconKey").stringValue = IconKeys[i];
                entry.FindPropertyRelative("texture").objectReferenceValue = LoadTexture(IconKeys[i]);
            }

            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log("[WasteBinIconLibraryBuilder] PNG icons assigned to WasteBinIconLibrary.");
        }

        [MenuItem("Waste Collection/Regenerate PNG Icons From SVG")]
        public static void RegeneratePngIconsFromSvg()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string scriptPath = Path.Combine(projectRoot, "Temp", "svg2png", "convert.mjs");
            if (!File.Exists(scriptPath))
            {
                Debug.LogError("[WasteBinIconLibraryBuilder] Missing Temp/svg2png/convert.mjs. Run npm install @resvg/resvg-js in that folder first.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Regenerate PNG Icons",
                    "This runs Node.js to rebuild PNG icons from SVG sources. Continue?",
                    "Regenerate",
                    "Cancel"))
                return;

            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
            process.WaitForExit();
            Debug.Log(process.StandardOutput.ReadToEnd());
            string error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(error))
                Debug.LogWarning(error);

            AssetDatabase.Refresh();
            AssignLucideBinIcons();
        }

        private static Texture2D LoadTexture(string iconKey)
        {
            string pngPath = $"{IconsFolder}/{iconKey}.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            if (texture != null)
                return texture;

            Debug.LogWarning($"[WasteBinIconLibraryBuilder] Texture2D not found for '{iconKey}' at {pngPath}.");
            return null;
        }
    }
}
#endif
