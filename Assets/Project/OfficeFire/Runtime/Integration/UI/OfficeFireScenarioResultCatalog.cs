using System;
using System.Collections.Generic;

namespace Woi.OfficeFire
{
    public static class OfficeFireScenarioResultCatalog
    {
        private readonly struct ObjectiveRule
        {
            public readonly OfficeFireObjectiveId Objective;
            public readonly Func<OfficeFireScenarioReport, bool> IsCompleted;

            public ObjectiveRule(OfficeFireObjectiveId objective, Func<OfficeFireScenarioReport, bool> isCompleted)
            {
                Objective = objective;
                IsCompleted = isCompleted;
            }
        }

        public static void EvaluateObjectives(
            OfficeFireScenarioReport report,
            List<string> completed,
            List<string> missing,
            Func<OfficeFireObjectiveId, string> labelResolver)
        {
            if (completed == null || missing == null || labelResolver == null)
            {
                return;
            }

            completed.Clear();
            missing.Clear();

            if (report == null)
            {
                return;
            }

            IReadOnlyList<ObjectiveRule> rules = GetRules(report.scenarioId);
            for (int i = 0; i < rules.Count; i++)
            {
                ObjectiveRule rule = rules[i];
                string label = labelResolver(rule.Objective);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                if (rule.IsCompleted(report))
                {
                    completed.Add(label);
                }
                else
                {
                    missing.Add(label);
                }
            }
        }

        private static IReadOnlyList<ObjectiveRule> GetRules(OfficeFireScenarioId scenarioId)
        {
            return scenarioId switch
            {
                OfficeFireScenarioId.ArchiveRoom => ArchiveRules,
                OfficeFireScenarioId.ServerRoom => ServerRules,
                OfficeFireScenarioId.KitchenCafe => KitchenRules,
                _ => Array.Empty<ObjectiveRule>(),
            };
        }

        private static readonly ObjectiveRule[] ArchiveRules =
        {
            new ObjectiveRule(
                OfficeFireObjectiveId.CheckArchiveRoom,
                report => HasAction(report, OfficeFireCorrectActionId.NoticedSmoke)),
            new ObjectiveRule(
                OfficeFireObjectiveId.PressArchiveAlarm,
                report => HasAction(report, OfficeFireCorrectActionId.PressedAlarm)),
            new ObjectiveRule(
                OfficeFireObjectiveId.UseArchiveExtinguisher,
                report => HasAnyAction(
                    report,
                    OfficeFireCorrectActionId.UsedExtinguisherCorrectly,
                    OfficeFireCorrectActionId.ControlledArchiveFire)
                    || report.fireControlled),
            new ObjectiveRule(
                OfficeFireObjectiveId.ExitArchiveRoom,
                report => HasAnyAction(
                    report,
                    OfficeFireCorrectActionId.ExitedArchiveRoom,
                    OfficeFireCorrectActionId.ReachedExitDoor,
                    OfficeFireCorrectActionId.ReachedAssemblyArea)),
            new ObjectiveRule(
                OfficeFireObjectiveId.GoToAssemblyArea,
                report => HasAction(report, OfficeFireCorrectActionId.ReachedAssemblyArea) || report.evacuated),
        };

        private static readonly ObjectiveRule[] ServerRules =
        {
            new ObjectiveRule(
                OfficeFireObjectiveId.CheckServerRoom,
                report => HasAction(report, OfficeFireCorrectActionId.NoticedSmoke)),
            new ObjectiveRule(
                OfficeFireObjectiveId.EnterServerRoom,
                report => HasAction(report, OfficeFireCorrectActionId.EnteredServerRoomSafely)),
            new ObjectiveRule(
                OfficeFireObjectiveId.ActivateServerSuppression,
                report => HasAction(report, OfficeFireCorrectActionId.ActivatedSuppressionSystem)),
            new ObjectiveRule(
                OfficeFireObjectiveId.LeaveServerRoom,
                report => HasAction(report, OfficeFireCorrectActionId.LeftServerRoomBeforeGas)),
            new ObjectiveRule(
                OfficeFireObjectiveId.GoToAssemblyArea,
                report => HasAction(report, OfficeFireCorrectActionId.ReachedAssemblyArea) || report.evacuated),
        };

        private static readonly ObjectiveRule[] KitchenRules =
        {
            new ObjectiveRule(
                OfficeFireObjectiveId.CheckKitchenArea,
                report => HasAction(report, OfficeFireCorrectActionId.NoticedSmoke)),
            new ObjectiveRule(
                OfficeFireObjectiveId.EnterKitchenCafe,
                report => HasAction(report, OfficeFireCorrectActionId.EnteredKitchenCafeSafely)),
            new ObjectiveRule(
                OfficeFireObjectiveId.GetFireBlanket,
                report => KitchenUsedSuppressionPath(report)
                    || HasAction(report, OfficeFireCorrectActionId.SelectedFireBlanket)),
            new ObjectiveRule(
                OfficeFireObjectiveId.PlaceFireBlanket,
                report => KitchenUsedSuppressionPath(report)
                    || HasAction(report, OfficeFireCorrectActionId.PlacedFireBlanketCorrectly)
                    || report.fireControlled),
            new ObjectiveRule(
                OfficeFireObjectiveId.ActivateKitchenSuppression,
                report => KitchenUsedBlanketPath(report)
                    || HasAction(report, OfficeFireCorrectActionId.ActivatedSuppressionSystem)),
            new ObjectiveRule(
                OfficeFireObjectiveId.LeaveKitchenCafe,
                report => HasAction(report, OfficeFireCorrectActionId.LeftKitchenCafeBeforeGas)),
            new ObjectiveRule(
                OfficeFireObjectiveId.GoToAssemblyArea,
                report => HasAction(report, OfficeFireCorrectActionId.ReachedAssemblyArea) || report.evacuated),
        };

        private static bool KitchenUsedBlanketPath(OfficeFireScenarioReport report)
        {
            return HasAnyAction(
                report,
                OfficeFireCorrectActionId.SelectedFireBlanket,
                OfficeFireCorrectActionId.PlacedFireBlanketCorrectly);
        }

        private static bool KitchenUsedSuppressionPath(OfficeFireScenarioReport report)
        {
            return HasAction(report, OfficeFireCorrectActionId.ActivatedSuppressionSystem)
                || HasAnyAction(
                    report,
                    OfficeFireCorrectActionId.UsedExtinguisherCorrectly,
                    OfficeFireCorrectActionId.ControlledKitchenFire);
        }

        private static bool HasAction(OfficeFireScenarioReport report, OfficeFireCorrectActionId actionId)
        {
            return report != null && report.correctActions.Contains(actionId);
        }

        private static bool HasAnyAction(OfficeFireScenarioReport report, params OfficeFireCorrectActionId[] actionIds)
        {
            if (report == null || actionIds == null)
            {
                return false;
            }

            for (int i = 0; i < actionIds.Length; i++)
            {
                if (report.correctActions.Contains(actionIds[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
