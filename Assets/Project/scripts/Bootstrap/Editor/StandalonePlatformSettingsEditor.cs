using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StandalonePlatformSettings))]
public sealed class StandalonePlatformSettingsEditor : UnityEditor.Editor
{
    static readonly string[] Labels = { "PC", "VR" };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty mode = serializedObject.FindProperty("mode");
        int current = mode.enumValueIndex == (int)AppMode.XR ? 1 : 0;
        int next = EditorGUILayout.Popup("Standalone Platform Mode", current, Labels);
        if (next != current)
            mode.enumValueIndex = next == 1 ? (int)AppMode.XR : (int)AppMode.PC;

        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox(
            "Shared by Hazard Hunt and Waste Collector when Hub/Bridge did not set a platform. " +
            "VR is stored as AppMode.XR. Quest stays VR. Default is PC.",
            MessageType.None);
    }
}
