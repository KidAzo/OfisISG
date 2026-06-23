using UnityEngine;

namespace Woi.OfficeFire
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Localized Sign Materials")]
    public sealed class OfficeFireLocalizedSignMaterialsBehaviour : MonoBehaviour
    {
        [SerializeField]
        private Material turkishMaterial;

        [SerializeField]
        private Material englishMaterial;

        [SerializeField]
        [Tooltip("Optional frame material to leave unchanged (e.g. ImphenziaPixPal).")]
        private Material frameMaterial;

        [SerializeField]
        private bool applyOnStart = true;

        private void Start()
        {
            if (applyOnStart)
            {
                Apply();
            }
        }

        public void Apply()
        {
            OfficeFireLocalizedSignMaterials.ApplyToHierarchy(
                transform,
                turkishMaterial,
                englishMaterial,
                frameMaterial);
        }
    }
}
