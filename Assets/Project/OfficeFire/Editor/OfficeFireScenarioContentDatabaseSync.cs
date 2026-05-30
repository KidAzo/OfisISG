using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Woi.OfficeFire.Editor
{
    /// <summary>
    /// Keeps <see cref="OfficeFireVoiceLineContentDatabase"/> assets aligned between scenarios.
    /// Shared voice lines and archive audio mappings are copied into the server database.
    /// </summary>
    public static class OfficeFireScenarioContentDatabaseSync
    {
        const string ArchiveAssetPath =
            "Assets/Project/OfficeFire/ScriptableObjects/ArchiveRoom/Content/ArchiveRoomScenarioContentDatabase.asset";

        const string ServerAssetPath =
            "Assets/Project/OfficeFire/ScriptableObjects/ServerRoom/Content/ServerRoomScenarioContentDatabase.asset";

        static readonly OfficeFireVoiceLineId[] ParallelArchiveVoiceLineIds =
        {
            OfficeFireVoiceLineId.ArchiveIncidentDetected,
            OfficeFireVoiceLineId.ArchiveElectricalFireWarning,
            OfficeFireVoiceLineId.ArchiveWaterMistake,
            OfficeFireVoiceLineId.ArchivePressAlarmInstruction,
            OfficeFireVoiceLineId.ArchiveCutPowerInstruction,
            OfficeFireVoiceLineId.ArchivePowerCutSuccess,
            OfficeFireVoiceLineId.ArchiveUseExtinguisherInstruction,
            OfficeFireVoiceLineId.ArchiveFireControlled,
            OfficeFireVoiceLineId.ArchiveFireNotControlledEvacuate,
            OfficeFireVoiceLineId.ArchiveFireGrowth,
            OfficeFireVoiceLineId.ExittedArchiveRoom,
        };

        static readonly OfficeFireVoiceLineId[] SharedVoiceLineIds =
        {
            OfficeFireVoiceLineId.SmokeWarning,
            OfficeFireVoiceLineId.CrouchInSmoke,
            OfficeFireVoiceLineId.AlarmInstruction,
            OfficeFireVoiceLineId.EvacuationInstruction,
            OfficeFireVoiceLineId.DoNotUseElevator,
            OfficeFireVoiceLineId.GoToAssemblyArea,
            OfficeFireVoiceLineId.ScenarioCompleted,
            OfficeFireVoiceLineId.LeanCorrectly,
            OfficeFireVoiceLineId.EstinguisherHandled,
            OfficeFireVoiceLineId.EstinguishingStarted,
            OfficeFireVoiceLineId.ReachAssemblyArea,
            OfficeFireVoiceLineId.ReachedExitDoor,
            OfficeFireVoiceLineId.ReachedAssemblyAreaDoor,
        };

        static readonly Dictionary<OfficeFireVoiceLineId, OfficeFireVoiceLineId[]> ServerVoiceFallbacks =
            new Dictionary<OfficeFireVoiceLineId, OfficeFireVoiceLineId[]>
            {
                {
                    OfficeFireVoiceLineId.ServerIncidentDetected,
                    new[]
                    {
                        OfficeFireVoiceLineId.ArchiveIncidentDetected,
                        OfficeFireVoiceLineId.EstinguishingStarted,
                        OfficeFireVoiceLineId.ArchiveFireNotControlledEvacuate,
                    }
                },
                {
                    OfficeFireVoiceLineId.ServerElectronicFireWarning,
                    new[]
                    {
                        OfficeFireVoiceLineId.ArchiveElectricalFireWarning,
                        OfficeFireVoiceLineId.ArchiveFireGrowth,
                    }
                },
                {
                    OfficeFireVoiceLineId.ServerWaterMistake,
                    new[]
                    {
                        OfficeFireVoiceLineId.ArchiveWaterMistake,
                        OfficeFireVoiceLineId.ExittedArchiveRoom,
                    }
                },
                {
                    OfficeFireVoiceLineId.ServerManualExtinguisherWarning,
                    new[]
                    {
                        OfficeFireVoiceLineId.ArchivePressAlarmInstruction,
                        OfficeFireVoiceLineId.AlarmInstruction,
                    }
                },
                {
                    OfficeFireVoiceLineId.ServerSuppressionInstruction,
                    new[]
                    {
                        OfficeFireVoiceLineId.ArchivePressAlarmInstruction,
                        OfficeFireVoiceLineId.AlarmInstruction,
                    }
                },
                {
                    OfficeFireVoiceLineId.ServerSuppressionCountdown,
                    new[]
                    {
                        OfficeFireVoiceLineId.ArchiveUseExtinguisherInstruction,
                        OfficeFireVoiceLineId.EstinguishingStarted,
                    }
                },
                {
                    OfficeFireVoiceLineId.ServerGasActiveLeaveArea,
                    new[]
                    {
                        OfficeFireVoiceLineId.ExittedArchiveRoom,
                        OfficeFireVoiceLineId.ArchiveFireGrowth,
                        OfficeFireVoiceLineId.EvacuationInstruction,
                    }
                },
                {
                    OfficeFireVoiceLineId.ServerFireControlled,
                    new[]
                    {
                        OfficeFireVoiceLineId.ArchiveFireControlled,
                    }
                },
            };

        [MenuItem("Woi/Office Fire/Sync Server Content Database From Archive")]
        public static void SyncServerFromArchiveMenu()
        {
            SyncServerFromArchive();
        }

        public static void SyncServerFromArchive()
        {
            OfficeFireVoiceLineContentDatabase archiveDb =
                AssetDatabase.LoadAssetAtPath<OfficeFireVoiceLineContentDatabase>(ArchiveAssetPath);
            OfficeFireVoiceLineContentDatabase serverDb =
                AssetDatabase.LoadAssetAtPath<OfficeFireVoiceLineContentDatabase>(ServerAssetPath);

            if (archiveDb == null || serverDb == null)
            {
                Debug.LogError(
                    "[OfficeFire] Could not load Archive/Server content databases. " +
                    "Run 'Create And Wire Scenario Content Databases' first.");
                return;
            }

            archiveDb.EditorSetScenario(OfficeFireScenarioId.ArchiveRoom);
            serverDb.EditorSetScenario(OfficeFireScenarioId.ServerRoom);
            archiveDb.EditorFillForAssignedScenario();
            serverDb.EditorFillForAssignedScenario();
            archiveDb.EditorRemoveDuplicateAndEmptyEntries();

            CopySharedVoiceLines(archiveDb, serverDb);
            CopyParallelArchiveVoiceLines(archiveDb, serverDb);
            CopyServerSpecificVoiceLines(archiveDb, serverDb);
            serverDb.EditorRemoveDuplicateAndEmptyEntries();

            EditorUtility.SetDirty(archiveDb);
            EditorUtility.SetDirty(serverDb);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[OfficeFire] ServerRoomScenarioContentDatabase synced from ArchiveRoomScenarioContentDatabase.\n" +
                $"- Source: {ArchiveAssetPath}\n" +
                $"- Target: {ServerAssetPath}");
        }

        static void CopySharedVoiceLines(
            OfficeFireVoiceLineContentDatabase archiveDb,
            OfficeFireVoiceLineContentDatabase serverDb)
        {
            for (int i = 0; i < SharedVoiceLineIds.Length; i++)
            {
                OfficeFireVoiceLineId id = SharedVoiceLineIds[i];
                if (!archiveDb.EditorTryGetEntry(id, out OfficeFireVoiceLineEntry archiveEntry))
                {
                    continue;
                }

                serverDb.EditorUpsertEntry(archiveEntry);
            }
        }

        static void CopyParallelArchiveVoiceLines(
            OfficeFireVoiceLineContentDatabase archiveDb,
            OfficeFireVoiceLineContentDatabase serverDb)
        {
            for (int i = 0; i < ParallelArchiveVoiceLineIds.Length; i++)
            {
                OfficeFireVoiceLineId id = ParallelArchiveVoiceLineIds[i];
                if (!archiveDb.EditorTryGetEntry(id, out OfficeFireVoiceLineEntry archiveEntry))
                {
                    continue;
                }

                serverDb.EditorUpsertEntry(archiveEntry);
            }
        }

        static void CopyServerSpecificVoiceLines(
            OfficeFireVoiceLineContentDatabase archiveDb,
            OfficeFireVoiceLineContentDatabase serverDb)
        {
            foreach (KeyValuePair<OfficeFireVoiceLineId, OfficeFireVoiceLineId[]> pair in ServerVoiceFallbacks)
            {
                if (!serverDb.EditorTryGetEntry(pair.Key, out OfficeFireVoiceLineEntry serverEntry))
                {
                    continue;
                }

                if (serverEntry.Voice != null)
                {
                    continue;
                }

                OfficeFireVoiceLineId[] archiveFallbacks = pair.Value;
                for (int i = 0; i < archiveFallbacks.Length; i++)
                {
                    if (!archiveDb.EditorTryGetEntry(archiveFallbacks[i], out OfficeFireVoiceLineEntry archiveEntry) ||
                        archiveEntry.Voice == null)
                    {
                        continue;
                    }

                    serverEntry.Voice = archiveEntry.Voice;
                    serverDb.EditorUpsertEntry(serverEntry);
                    break;
                }
            }
        }
    }
}
