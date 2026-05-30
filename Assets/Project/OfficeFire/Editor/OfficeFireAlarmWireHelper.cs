using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    public static class OfficeFireAlarmWireHelper
    {
        const string AlarmPressedAssetPath =
            "Assets/Project/OfficeFire/ScriptableObjects/Events/onAlarmPressed.asset";

        public static void WireAlarmLikeArchive(
            Transform host,
            OfficeFireScenarioController controller,
            string actionId,
            string instructionText,
            string instructionTextTurkish,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            if (host == null)
            {
                return;
            }

            SelectableScenarioAction legacyAction = host.GetComponent<SelectableScenarioAction>();
            if (legacyAction != null)
            {
                Undo.DestroyObjectImmediate(legacyAction);
            }

            EnsurePcHoverCollider(host.gameObject, componentsAdded);

            Outline outline = OfficeFireSceneHierarchyBuilder.TryAddComponent<Outline>(
                host.gameObject,
                "Outline",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            if (outline != null)
            {
                Undo.RecordObject(outline, "Office Fire: Configure alarm outline");
                outline.OutlineColor = new Color(1f, 0.92f, 0f, 1f);
                outline.OutlineWidth = 2f;
                outline.enabled = false;
            }

            Alarm alarm = OfficeFireSceneHierarchyBuilder.TryAddComponent<Alarm>(
                host.gameObject,
                "Alarm",
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            if (alarm == null)
            {
                return;
            }

            Undo.RecordObject(alarm, "Office Fire: Wire Alarm");
            SerializedObject so = new SerializedObject(alarm);

            SerializedProperty actionIdProp = so.FindProperty("actionId");
            if (actionIdProp != null)
            {
                actionIdProp.stringValue = actionId;
            }
            else
            {
                componentWarnings.Add("Alarm: serialized field 'actionId' not found.");
            }

            SerializedProperty alarmPressedProp = so.FindProperty("alarmPressed");
            if (alarmPressedProp != null)
            {
                ScriptableObject alarmPressed = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AlarmPressedAssetPath);
                if (alarmPressed != null)
                {
                    alarmPressedProp.objectReferenceValue = alarmPressed;
                }
                else
                {
                    componentWarnings.Add("Alarm: onAlarmPressed asset not found at " + AlarmPressedAssetPath);
                }
            }

            if (controller != null)
            {
                SerializedProperty targetProp = so.FindProperty("targetScenario");
                if (targetProp != null)
                {
                    targetProp.objectReferenceValue = controller;
                }
            }

            SerializedProperty instructionTextProp = so.FindProperty("instructionText");
            if (instructionTextProp != null)
            {
                instructionTextProp.stringValue = instructionText;
            }

            SerializedProperty instructionTextTrProp = so.FindProperty("instructionTextTurkish");
            if (instructionTextTrProp != null)
            {
                instructionTextTrProp.stringValue = instructionTextTurkish;
            }

            SerializedProperty outlineProp = so.FindProperty("outline");
            if (outlineProp != null && outline != null)
            {
                outlineProp.objectReferenceValue = outline;
            }

            SerializedProperty useWidthProp = so.FindProperty("useOutlineWidth");
            if (useWidthProp != null)
            {
                useWidthProp.boolValue = true;
            }

            SerializedProperty widthProp = so.FindProperty("hoverOutlineWidth");
            if (widthProp != null)
            {
                widthProp.floatValue = 5f;
            }

            so.ApplyModifiedProperties();
        }

        static void EnsurePcHoverCollider(GameObject host, List<string> componentsAdded)
        {
            BoxCollider[] colliders = host.GetComponents<BoxCollider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider collider = colliders[i];
                if (collider != null && !collider.isTrigger && collider.enabled)
                {
                    return;
                }
            }

            BoxCollider trigger = host.GetComponent<BoxCollider>();
            BoxCollider hoverCollider = Undo.AddComponent<BoxCollider>(host);
            hoverCollider.isTrigger = false;
            hoverCollider.enabled = true;

            if (trigger != null)
            {
                hoverCollider.center = trigger.center;
                hoverCollider.size = trigger.size;
            }
            else
            {
                hoverCollider.center = new Vector3(0f, 0.0012664199f, 0.02315612f);
                hoverCollider.size = new Vector3(0.15000002f, 0.11010504f, 0.04631224f);
            }

            componentsAdded.Add($"BoxCollider on '{OfficeFireSceneHierarchyBuilder.FullPath(host.transform)}'");
        }
    }
}
