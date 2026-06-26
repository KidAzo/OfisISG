using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Ensures XR extinguisher grab wiring at runtime (see <see cref="OfficeFireVrExtinguisherRigWiring"/>).
    /// </summary>
    public static class OfficeFireVrExtinguisherRigBootstrap
    {
        static bool _wired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _wired = false;
        }

        public static void EnsureWired()
        {
            if (!FirePlatformRuntime.IsVR)
                return;

            OfficeFireVrExtinguisherRigWiring.EnsureJumpSuppressed();
            OfficeFireVrExtinguisherRigWiring.EnsurePlayerTriggerCompatibility();

            if (_wired)
                return;

            _wired = OfficeFireVrExtinguisherRigWiring.EnsureWired();
        }
    }
}
