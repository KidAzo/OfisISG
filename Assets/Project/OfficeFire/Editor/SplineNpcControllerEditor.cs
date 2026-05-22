using UnityEditor;
using UnityEngine;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    [CustomEditor(typeof(SplineNpcController))]
    public sealed class SplineNpcControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SplineNpcController controller = (SplineNpcController)target;
            if (controller.Path == null)
            {
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Snap To Path Start"))
            {
                Undo.RecordObject(controller.transform, "Snap NPC To Path Start");
                controller.SnapToPathStart(storeAsResetPose: true);
                EditorUtility.SetDirty(controller);
            }
        }
    }
}
