using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Woi.OfficeFire;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Wires <see cref="OfficeFireLocalizedSignMaterialsBehaviour"/> on kitchen fire-scene props
    /// (blanket bag, alarm/sign cluster, extinguisher wall sign) with their TR/EN materials.
    /// </summary>
    internal static class OfficeFireLocalizedSignMaterialsEditorHelper
    {
        private const string FireBlanketTurkishPath = "Assets/AssetSection/BKAsset/Materials/FireBlanketTR.mat";
        private const string FireBlanketEnglishPath = "Assets/AssetSection/BKAsset/Materials/FireBlanketENG.mat";
        private const string FireButtonSignTurkishPath = "Assets/AssetSection/BKAsset/Materials/FireButtonAndSignTR.mat";
        private const string FireButtonSignEnglishPath = "Assets/AssetSection/BKAsset/Materials/FireButtonAndSignENG.mat";
        private const string ExtinguisherSignTurkishPath = "Assets/AssetSection/YanginTupu/tr/Yang\u0131nSondurucu.mat";
        private const string ExtinguisherSignEnglishPath = "Assets/AssetSection/YanginTupu/Yang\u0131nSondurucu ENG.mat";
        private const string SignFrameMaterialPath = "Assets/AssetSection/BKAsset/Materials/ImphenziaPixPal_URP.mat";

        public static void WireStandardFireSignsUnder(
            Transform root,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            if (root == null)
            {
                componentWarnings.Add("Localized sign materials: root transform was null.");
                return;
            }

            string rootPath = OfficeFireSceneHierarchyBuilder.FullPath(root);

            Material fireBlanketTurkish = LoadMaterial(FireBlanketTurkishPath, componentWarnings);
            Material fireBlanketEnglish = LoadMaterial(FireBlanketEnglishPath, componentWarnings);
            Material fireButtonSignTurkish = LoadMaterial(FireButtonSignTurkishPath, componentWarnings);
            Material fireButtonSignEnglish = LoadMaterial(FireButtonSignEnglishPath, componentWarnings);
            Material extinguisherSignTurkish = LoadMaterial(ExtinguisherSignTurkishPath, componentWarnings);
            Material extinguisherSignEnglish = LoadMaterial(ExtinguisherSignEnglishPath, componentWarnings);
            Material signFrameMaterial = LoadMaterial(SignFrameMaterialPath, componentWarnings);

            WireHost(
                FindChildByNameRecursive(root, "FireBlanketBAG"),
                fireBlanketTurkish,
                fireBlanketEnglish,
                signFrameMaterial,
                "FireBlanketBAG",
                rootPath,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireHost(
                FindChildByNameRecursive(root, "FireSign"),
                fireButtonSignTurkish,
                fireButtonSignEnglish,
                null,
                "FireSign",
                rootPath,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);

            WireHost(
                FindExtinguisherWallSign(root),
                extinguisherSignTurkish,
                extinguisherSignEnglish,
                null,
                "yang\u0131n sondurucu levha",
                rootPath,
                componentsAdded,
                componentsAlreadyPresent,
                componentWarnings);
        }

        private static void WireHost(
            Transform host,
            Material turkishMaterial,
            Material englishMaterial,
            Material frameMaterial,
            string label,
            string searchedUnderPath,
            List<string> componentsAdded,
            List<string> componentsAlreadyPresent,
            List<string> componentWarnings)
        {
            if (host == null)
            {
                componentWarnings.Add($"Localized sign materials: '{label}' not found under '{searchedUnderPath}'.");
                return;
            }

            if (turkishMaterial == null || englishMaterial == null)
            {
                componentWarnings.Add($"Localized sign materials: TR/EN materials missing for '{label}'.");
                return;
            }

            OfficeFireLocalizedSignMaterialsBehaviour behaviour =
                OfficeFireSceneHierarchyBuilder.TryAddComponent<OfficeFireLocalizedSignMaterialsBehaviour>(
                    host.gameObject,
                    label,
                    componentsAdded,
                    componentsAlreadyPresent,
                    componentWarnings);
            if (behaviour == null)
            {
                return;
            }

            Undo.RecordObject(behaviour, "Office Fire: Wire localized sign materials");
            SerializedObject serializedBehaviour = new SerializedObject(behaviour);
            serializedBehaviour.FindProperty("turkishMaterial").objectReferenceValue = turkishMaterial;
            serializedBehaviour.FindProperty("englishMaterial").objectReferenceValue = englishMaterial;
            serializedBehaviour.FindProperty("frameMaterial").objectReferenceValue = frameMaterial;
            serializedBehaviour.FindProperty("applyOnStart").boolValue = true;
            serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material LoadMaterial(string assetPath, List<string> componentWarnings)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                componentWarnings.Add($"Localized sign materials: material not found at '{assetPath}'.");
            }

            return material;
        }

        private static Transform FindExtinguisherWallSign(Transform root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null)
                {
                    continue;
                }

                string name = candidate.name;
                if (name.StartsWith("yang\u0131n sondurucu levha"))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform FindChildByNameRecursive(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == childName)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildByNameRecursive(parent.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
