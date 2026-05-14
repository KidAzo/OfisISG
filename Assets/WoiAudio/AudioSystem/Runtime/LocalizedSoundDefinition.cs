using UnityEngine;
using Woi.UI.Popups.Localization;

namespace WoiUtils.AudioSystem
{
    /// <summary>
    /// Two <see cref="SoundDefinition"/> assets (English / Turkish). Resolves with <see cref="LocalizationService"/> / <see cref="ILocalizationService"/> active language.
    /// Use from <see cref="AudioTrigger"/> or call <see cref="ResolveForCurrentLanguage"/> in code.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizedSound", menuName = "WoiAudio/Localized Sound (EN + TR)", order = 4)]
    public sealed class LocalizedSoundDefinition : ScriptableObject
    {
        [Tooltip("Played when Current Language is en (or fallback if Turkish missing).")]
        public SoundDefinition english;

        [Tooltip("Played when Current Language is tr.")]
        public SoundDefinition turkish;

        /// <summary>Picks EN or TR sound for <see cref="ILocalizationService.CurrentLanguage"/>.</summary>
        public SoundDefinition ResolveForCurrentLanguage() =>
            LocalizedLanguageAssetResolver.Pick(english, turkish);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (english == null && turkish == null)
                Debug.LogWarning($"[LocalizedSoundDefinition] '{name}': assign English and/or Turkish SoundDefinition.", this);
        }
#endif
    }
}
