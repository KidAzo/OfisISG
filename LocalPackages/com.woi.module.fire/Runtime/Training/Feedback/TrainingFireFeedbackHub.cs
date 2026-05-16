using UnityEngine;

namespace Woi.Game.Training.Feedback
{
    /// <summary>
    /// Yangın eğitimi sunum geri bildirimleri için kök obje işaretçisi.
    /// Altına <see cref="ExtinguishProximityScreenShake"/> vb. ekleyerek tek yerden yönetin.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("WOI/Training/Fire Feedback Hub")]
    public sealed class TrainingFireFeedbackHub : MonoBehaviour { }
}
