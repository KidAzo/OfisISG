using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Applies TR/EN sign materials based on <see cref="OfficeFireSessionLanguage"/>.
    /// </summary>
    public static class OfficeFireLocalizedSignMaterials
    {
        public static void ApplyToHierarchy(
            Transform root,
            Material turkishMaterial,
            Material englishMaterial,
            Material frameMaterial = null)
        {
            if (root == null || turkishMaterial == null || englishMaterial == null)
            {
                return;
            }

            Material targetMaterial = OfficeFireSessionLanguage.UseTurkish()
                ? turkishMaterial
                : englishMaterial;

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ApplyToRenderer(renderers[i], targetMaterial, turkishMaterial, englishMaterial, frameMaterial);
            }
        }

        public static void ApplyAllInScene()
        {
            OfficeFireLocalizedSignMaterialsBehaviour[] behaviours =
                Object.FindObjectsByType<OfficeFireLocalizedSignMaterialsBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i]?.Apply();
            }
        }

        private static void ApplyToRenderer(
            MeshRenderer renderer,
            Material targetMaterial,
            Material turkishMaterial,
            Material englishMaterial,
            Material frameMaterial)
        {
            if (renderer == null)
            {
                return;
            }

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int slot = 0; slot < materials.Length; slot++)
            {
                Material current = materials[slot];
                if (!ShouldReplaceSignMaterial(current, turkishMaterial, englishMaterial, frameMaterial))
                {
                    continue;
                }

                if (current == targetMaterial)
                {
                    continue;
                }

                materials[slot] = targetMaterial;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }

        private static bool ShouldReplaceSignMaterial(
            Material current,
            Material turkishMaterial,
            Material englishMaterial,
            Material frameMaterial)
        {
            if (current == null)
            {
                return false;
            }

            if (frameMaterial != null && current == frameMaterial)
            {
                return false;
            }

            if (current == turkishMaterial || current == englishMaterial)
            {
                return true;
            }

            string materialName = current.name;
            if (materialName.StartsWith("AcilToplanma")
                || materialName.StartsWith("FireBlanket")
                || materialName.StartsWith("FireButtonAndSign")
                || materialName.StartsWith("Yang\u0131nSondurucu"))
            {
                return true;
            }

            return materialName == "Material";
        }
    }
}
