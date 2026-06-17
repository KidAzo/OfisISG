using System;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    public abstract class OfficeFireScenarioController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField]
        private GameObject scenarioRoot;

        [Header("Report (runtime)")]
        [SerializeField]
        private OfficeFireScenarioReport report = new OfficeFireScenarioReport();

        [Header("Integration")]
        [SerializeField]
        private ObjectiveChangedEvent onObjectiveChanged = new ObjectiveChangedEvent();

        [SerializeField]
        private VoiceLineEvent onAnnouncementRequested = new VoiceLineEvent();

        [SerializeField]
        private CorrectActionEvent onCorrectActionRegistered = new CorrectActionEvent();

        [SerializeField]
        private MistakeEvent onMistakeRegistered = new MistakeEvent();

        [SerializeField]
        private UnityEvent onScenarioStarted = new UnityEvent();

        [SerializeField]
        private UnityEvent onScenarioCompleted = new UnityEvent();

        private float _scenarioStartTime;
        private bool _hasRecordedReaction;
        private float _reactionElapsedSeconds;
        private bool _isScenarioActive;
        private bool _isCompleted;

        public abstract OfficeFireScenarioId ScenarioId { get; }

        public abstract void HandleAction(string actionId);

        public GameObject ScenarioRoot => scenarioRoot;

        public OfficeFireScenarioReport Report => report;

        public ObjectiveChangedEvent OnObjectiveChanged => onObjectiveChanged;

        public VoiceLineEvent OnAnnouncementRequested => onAnnouncementRequested;

        public CorrectActionEvent OnCorrectActionRegistered => onCorrectActionRegistered;

        public MistakeEvent OnMistakeRegistered => onMistakeRegistered;

        public UnityEvent OnScenarioStarted => onScenarioStarted;

        public UnityEvent OnScenarioCompleted => onScenarioCompleted;

        public bool IsScenarioActive => _isScenarioActive;

        public bool IsCompleted => _isCompleted;

        /// <summary>
        /// Called when another scenario is chosen (e.g. bootstrapper / future UI). Clears active flags without completing.
        /// </summary>
        public virtual void NotifyDeselected()
        {
            _isScenarioActive = false;
        }

        public virtual void StartScenario()
        {
            if (_isScenarioActive && !_isCompleted)
            {
                Debug.LogWarning($"[{GetType().Name}] StartScenario ignored: scenario already active.", this);
                return;
            }

            ResetRuntimeState();
            report = new OfficeFireScenarioReport { scenarioId = ScenarioId };
            _scenarioStartTime = Time.time;
            _isScenarioActive = true;
            _isCompleted = false;

            Debug.Log($"[{GetType().Name}] Scenario started (id={ScenarioId}).", this);

            if (onScenarioStarted != null)
            {
                onScenarioStarted.Invoke();
            }
        }

        public void CompleteScenario()
        {
            if (_isCompleted)
            {
                return;
            }

            _isCompleted = true;
            _isScenarioActive = false;
            report.completed = true;
            report.reactionTime = _hasRecordedReaction ? _reactionElapsedSeconds : 0f;

            Debug.Log($"[{GetType().Name}] Scenario completed (id={ScenarioId}).", this);

            if (onScenarioCompleted != null)
            {
                onScenarioCompleted.Invoke();
            }
        }

        protected virtual void ResetRuntimeState()
        {
            _hasRecordedReaction = false;
            _reactionElapsedSeconds = 0f;
        }

        public bool CanProcessActions()
        {
            return _isScenarioActive && !_isCompleted;
        }

        public void SetObjective(OfficeFireObjectiveId objectiveId)
        {
            if (onObjectiveChanged != null)
            {
                onObjectiveChanged.Invoke(objectiveId);
            }
        }

        public void PlayAnnouncement(OfficeFireVoiceLineId voiceLineId)
        {
            if (voiceLineId == OfficeFireVoiceLineId.None)
            {
                return;
            }

            if (onAnnouncementRequested != null)
            {
                onAnnouncementRequested.Invoke(voiceLineId);
            }
        }

        public void RegisterCorrectAction(OfficeFireCorrectActionId actionId)
        {
            if (actionId == OfficeFireCorrectActionId.None)
            {
                return;
            }

            report.AddCorrectAction(actionId);

            if (onCorrectActionRegistered != null)
            {
                onCorrectActionRegistered.Invoke(actionId);
            }
        }

        public void RegisterMistake(OfficeFireMistakeId mistakeId)
        {
            if (mistakeId == OfficeFireMistakeId.None)
            {
                return;
            }

            report.AddMistake(mistakeId);

            if (onMistakeRegistered != null)
            {
                onMistakeRegistered.Invoke(mistakeId);
            }
        }

        /// <summary>
        /// Records <see cref="OfficeFireMistakeId.StoodInSmoke"/> when the smoke phase ends without a lean.
        /// </summary>
        public void RegisterStoodInSmokeIfNotLeaned()
        {
            if (report.correctActions.Contains(OfficeFireCorrectActionId.LeanedCorrectly))
            {
                return;
            }

            RegisterMistake(OfficeFireMistakeId.StoodInSmoke);
        }

        public void MarkReactionIfNeeded()
        {
            if (_hasRecordedReaction)
            {
                return;
            }

            _hasRecordedReaction = true;
            _reactionElapsedSeconds = Time.time - _scenarioStartTime;
        }

        public void MarkFireControlled()
        {
            report.fireControlled = true;
        }

        public void MarkEvacuated()
        {
            report.evacuated = true;
        }
    }
}
