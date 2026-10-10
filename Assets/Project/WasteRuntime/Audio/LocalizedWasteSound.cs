using UnityEngine;
using WoiUtils.AudioSystem;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// TR/EN pair of <see cref="SoundDefinition"/> assets resolved against the Waste Collection
    /// language system (<see cref="WasteCollectionLocalization.IsEnglish"/>), not the generic
    /// LocalizationService. Use a RandomWeighted SoundDefinition when several variants should play.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocalizedWasteSound",
        menuName = "Waste Collection/Audio/Localized Waste Sound")]
    public class LocalizedWasteSound : ScriptableObject
    {
        [SerializeField] private SoundDefinition turkish;
        [SerializeField] private SoundDefinition english;

        public SoundDefinition Resolve()
        {
            if (WasteCollectionLocalization.IsEnglish)
                return english != null ? english : turkish;

            return turkish != null ? turkish : english;
        }

        public void StopAllInstances(AudioSystem audioSystem)
        {
            if (audioSystem == null)
                return;

            if (turkish != null)
                audioSystem.StopAllInstances(turkish);

            if (english != null)
                audioSystem.StopAllInstances(english);
        }
    }
}
