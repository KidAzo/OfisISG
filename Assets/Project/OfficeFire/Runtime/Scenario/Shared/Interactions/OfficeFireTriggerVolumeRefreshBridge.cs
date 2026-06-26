using System;
using System.Reflection;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Scenario-side handler for <see cref="OfficeFirePlayerTriggerRefresh"/> (Core assembly cannot reference Scenario types).
    /// </summary>
    public static class OfficeFireTriggerVolumeRefreshBridge
    {
        const string FireCriticalProximityVolumeTypeName = "Woi.Training.FireCriticalProximityVolume";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OfficeFirePlayerTriggerRefresh.RefreshRequested -= RefreshAllVolumesImmediate;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Subscribe()
        {
            OfficeFirePlayerTriggerRefresh.RefreshRequested -= RefreshAllVolumesImmediate;
            OfficeFirePlayerTriggerRefresh.RefreshRequested += RefreshAllVolumesImmediate;
        }

        public static void RefreshAllVolumesImmediate()
        {
            RefreshVolumes<ScenarioTriggerVolume>();
            RefreshVolumes<RailingHoldTriggerVolume>();
            RefreshTrainingProximityVolumes();
        }

        static void RefreshVolumes<T>() where T : MonoBehaviour, IPlayerTriggerVolumeRefresh
        {
            T[] volumes = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < volumes.Length; i++)
            {
                T volume = volumes[i];
                if (volume != null && volume.isActiveAndEnabled)
                    volume.RefreshPlayerOverlap();
            }
        }

        static void RefreshTrainingProximityVolumes()
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.isActiveAndEnabled)
                    continue;

                Type type = behaviour.GetType();
                if (!string.Equals(type.FullName, FireCriticalProximityVolumeTypeName, StringComparison.Ordinal))
                    continue;

                MethodInfo method = type.GetMethod(
                    "RefreshPlayerOverlap",
                    BindingFlags.Instance | BindingFlags.Public);
                method?.Invoke(behaviour, null);
            }
        }
    }
}
