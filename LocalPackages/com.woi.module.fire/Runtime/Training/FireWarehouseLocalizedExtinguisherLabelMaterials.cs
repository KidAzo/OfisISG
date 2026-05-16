using System;
using UnityEngine;
using WOI.Modules.SDK;
using Woi.UI.Popups.Localization;

namespace Woi.Training
{
    /// <summary>
    /// Depo / eğitim istasyonundaki duvar posterleri ve tüp üzerindeki etiket mesh’lerine
    /// <see cref="ILocalizationService.CurrentLanguage"/> göre TR veya EN <see cref="Material"/> atar.
    /// Tüm posterler aynı materyal asset’ini paylaşıyorsa yalnızca duvar alanını doldurun; tüpler ayrı atlas kullanıyorsa tüp alanını da doldurun.
    /// Tüp TR/EN boşsa duvar materyalleri tüplere de uygulanır.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Training/Fire Warehouse Localized Label Materials")]
    public sealed class FireWarehouseLocalizedExtinguisherLabelMaterials : MonoBehaviour
    {
        [Header("Wall posters (shared TR / EN materials)")]
        [SerializeField]
        private Renderer[] wallLabelRenderers;

        [SerializeField]
        private Material wallMaterialTurkish;

        [SerializeField]
        private Material wallMaterialEnglish;

        [Header("Cylinder / body labels (optional — falls back to wall materials)")]
        [SerializeField]
        private Renderer[] tubeLabelRenderers;

        [SerializeField]
        private Material tubeMaterialTurkish;

        [SerializeField]
        private Material tubeMaterialEnglish;

        [Tooltip("Which material slot to replace on each Renderer (0 for single-material meshes).")]
        [SerializeField]
        [Min(0)]
        private int materialSlotIndex;

        string _lastLanguageCode = "\u0001";

        void OnEnable()
        {
            CacheLanguage();
            ApplyForCurrentLanguage();
        }

        void LateUpdate()
        {
            string now = ResolveCurrentLanguageCode();
            if (string.Equals(now, _lastLanguageCode, StringComparison.OrdinalIgnoreCase))
                return;

            _lastLanguageCode = now;
            ApplyForCurrentLanguage();
        }

        void CacheLanguage() => _lastLanguageCode = ResolveCurrentLanguageCode();

        void ApplyForCurrentLanguage()
        {
            bool preferEnglish = ShouldUseEnglishMaterials(ResolveCurrentLanguageCode());

            Material wallMat = PickMaterial(wallMaterialTurkish, wallMaterialEnglish, preferEnglish);
            Material tubeMat = PickMaterial(
                tubeMaterialTurkish != null || tubeMaterialEnglish != null ? tubeMaterialTurkish : wallMaterialTurkish,
                tubeMaterialTurkish != null || tubeMaterialEnglish != null ? tubeMaterialEnglish : wallMaterialEnglish,
                preferEnglish);

            ApplyToRenderers(wallLabelRenderers, wallMat);
            ApplyToRenderers(tubeLabelRenderers, tubeMat);
        }

        static Material PickMaterial(Material tr, Material en, bool preferEnglish)
        {
            if (preferEnglish)
                return en != null ? en : tr;
            return tr != null ? tr : en;
        }

        void ApplyToRenderers(Renderer[] renderers, Material mat)
        {
            if (renderers == null || mat == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                Material[] mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                    continue;

                int slot = Mathf.Clamp(materialSlotIndex, 0, mats.Length - 1);
                if (mats[slot] == mat)
                    continue;

                mats[slot] = mat;
                r.sharedMaterials = mats;
            }
        }

        static bool ShouldUseEnglishMaterials(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;

            if (string.Equals(code, LocalizationService.English, StringComparison.OrdinalIgnoreCase))
                return true;

            return code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        }

        static string ResolveCurrentLanguageCode()
        {
            if (ServiceLocator.TryGet<ILocalizationService>(out ILocalizationService iloc) && iloc != null
                && !string.IsNullOrEmpty(iloc.CurrentLanguage))
                return iloc.CurrentLanguage.Trim().ToLowerInvariant();

            if (LocalizationService.Instance != null && !string.IsNullOrEmpty(LocalizationService.Instance.CurrentLanguage))
                return LocalizationService.Instance.CurrentLanguage.Trim().ToLowerInvariant();

            return string.Empty;
        }
    }
}
