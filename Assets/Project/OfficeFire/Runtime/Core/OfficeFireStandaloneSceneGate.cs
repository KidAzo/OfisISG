using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Standalone START loads FireModule_Office in Single mode, then the rest of the scene group additively.
    /// The office scenario waits until that additive set is finished. Hub never sets this gate.
    /// </summary>
    public static class OfficeFireStandaloneSceneGate
    {
        public static bool IsWaitingForAdditiveScenes { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlayModeStatics()
        {
            IsWaitingForAdditiveScenes = false;
        }

        public static void BeginWaitingForAdditiveScenes()
        {
            IsWaitingForAdditiveScenes = true;
        }

        public static void MarkAdditiveScenesReady()
        {
            IsWaitingForAdditiveScenes = false;
        }

        public static void CancelWaiting()
        {
            IsWaitingForAdditiveScenes = false;
        }
    }
}
