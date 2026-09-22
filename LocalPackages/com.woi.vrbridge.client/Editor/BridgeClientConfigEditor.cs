using UnityEditor;
using UnityEngine;
using Woi.VrBridge.Client.Configuration;

namespace Woi.VrBridge.Client.Editor
{
    [CustomEditor(typeof(BridgeClientConfig))]
    public sealed class BridgeClientConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (BridgeClientConfig)target;
            var errors = config.Validate();
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Configuration is valid.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Validation Errors", EditorStyles.boldLabel);
            foreach (var error in errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }
    }
}
