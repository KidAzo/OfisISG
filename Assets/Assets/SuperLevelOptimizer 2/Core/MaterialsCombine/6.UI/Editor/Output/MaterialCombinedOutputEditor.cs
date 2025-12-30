using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NGS.SLO.MaterialsCombine;

namespace NGS.SLO.MaterialsCombine
{
    [CustomEditor(typeof(MaterialCombineOutput))]
    public class MaterialCombinedOutputEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginDisabledGroup(true);

            base.OnInspectorGUI();

            EditorGUI.EndDisabledGroup();
        }
    }
}