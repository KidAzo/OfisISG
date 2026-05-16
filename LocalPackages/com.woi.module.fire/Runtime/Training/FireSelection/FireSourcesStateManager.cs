using System;
using System.Collections.Generic;
using FireExtinguisher.Core;
using UnityEngine;
using Woi.Events.Data;

namespace Woi.Game.Training.FireSelection
{
    /// <summary>
    /// Scene-level registry of <see cref="TrainingFireSelectionState"/> instances. Refreshes the list from the
    /// scene and applies inclusion in bulk (e.g. after the player picks which <see cref="FireClass"/> types to train).
    /// Place one instance per scene (or per training area) and call <see cref="Refresh"/> if fires are spawned at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("WOI/Training/Fire Sources State Manager")]
    public sealed class FireSourcesStateManager : MonoBehaviour
    {
        [Header("Discovery")]
        [Tooltip("When true, Refresh() also finds components on inactive GameObjects.")]
        [SerializeField] private bool _includeInactiveObjects;

        [Tooltip("Rebuild the registry when this component is enabled.")]
        [SerializeField] private bool _refreshOnEnable = true;

        [Header("Active fire types (Inspector)")]
        [Tooltip("Tick the fire classes that should be IN this training session. " +
                 "Each registered fire is selected only if its Training Fire Class matches a ticked item.")]
        [SerializeField] private List<FireClass> _activeFireClasses = new List<FireClass>();

        [Tooltip("When the manager enables, apply the mask above to all registered TrainingFireSelectionState components.")]
        [SerializeField] private bool _applyMaskOnEnable = true;

        [Tooltip("If true, also applies the mask in Edit Mode (writes selection on fire prefabs/scene objects). Use with care.")]
        [SerializeField] private bool _applyMaskInEditMode;

        private readonly List<TrainingFireSelectionState> _states = new();

        /// <summary>
        /// Set to true after <see cref="Start"/> runs. Guards <see cref="OnEnable"/> from applying
        /// the mask before all scene objects have finished their own Awake()+OnEnable() calls.
        /// </summary>
        private bool _hasStarted;

        /// <summary>Cached selection states from the last <see cref="Refresh"/>.</summary>
        public IReadOnlyList<TrainingFireSelectionState> States => _states;

        /// <summary>Number of registered <see cref="TrainingFireSelectionState"/> components.</summary>
        public int RegisteredCount => _states.Count;

        private void Awake()
        {
            Refresh();
        }

        /// <summary>
        /// Deferred first-time initialization. Unity guarantees Start() runs only after ALL
        /// scene objects have completed Awake() + OnEnable(), making Refresh() fully reliable
        /// regardless of scene-load order or Script Execution Order settings.
        /// </summary>
        private void Start()
        {
            _hasStarted = true;

            if (_sessionData != null)
            {
                // Read current SO state (data was written by GameInitializer before scene load)
                HandleSessionUpdated();
            }
            else
            {
                if (_refreshOnEnable)
                    Refresh();
                if (_applyMaskOnEnable && (Application.isPlaying || _applyMaskInEditMode))
                    ApplyMaskToRegisteredStatesOnly();
            }
        }

        [Header("Session Data")]
        [Tooltip("If assigned, the manager will initialize its mask from this SO.")]
        [SerializeField] private Woi.Events.Data.SessionDataSO _sessionData;

        private void OnEnable()
        {
            if (_sessionData != null)
            {
                _sessionData.OnSessionUpdated += HandleSessionUpdated;

                // Only apply immediately when re-enabled after the first Start().
                // On the very first enable at scene load, Start() below will handle this
                // so that all TrainingFireSelectionState objects are guaranteed to exist.
                if (_hasStarted)
                    HandleSessionUpdated();
            }
            else
            {
                // No session data: apply the inspector mask, but only if Start() has
                // already run (same determinism guarantee).
                if (_hasStarted)
                {
                    if (_refreshOnEnable)
                        Refresh();
                    if (_applyMaskOnEnable && (Application.isPlaying || _applyMaskInEditMode))
                        ApplyMaskToRegisteredStatesOnly();
                }
            }
        }

        private void OnDisable()
        {
            if (_sessionData != null)
            {
                _sessionData.OnSessionUpdated -= HandleSessionUpdated;
            }
        }

        private void HandleSessionUpdated()
        {
            // ── 1. Primary source: static GameSessionData (written by GameInitializer before scene load)
            //       Reliable regardless of SO wiring or event timing.
            if (GameSessionData.IsSet)
            {
                _activeFireClasses = new List<FireClass>(GameSessionData.SelectedClasses);
                Debug.Log($"[FireSourcesStateManager] Loaded {_activeFireClasses.Count} fire class(es) from GameSessionData. (User: {GameSessionData.UserName})");
            }
            // ── 2. Fallback: SessionDataSO (kept for backward compat / reactive mid-session changes)
            else if (_sessionData != null && _sessionData.SelectedClasses != null)
            {
                _activeFireClasses = new List<FireClass>(_sessionData.SelectedClasses);
                Debug.Log($"[FireSourcesStateManager] Loaded {_activeFireClasses.Count} fire class(es) from SessionDataSO. (User: {_sessionData.UserName})");
            }
            else
            {
                Debug.LogWarning("[FireSourcesStateManager] HandleSessionUpdated: No session data available (GameSessionData.IsSet=false, _sessionData=null or empty). Mask not applied.");
                return;
            }

            Refresh();

            if (_applyMaskOnEnable && (Application.isPlaying || _applyMaskInEditMode))
                ApplyMaskToRegisteredStatesOnly();
        }

        /// <summary>
        /// Re-scans the loaded scene(s) for <see cref="TrainingFireSelectionState"/>. Call after instantiating or
        /// enabling fire prefabs at runtime.
        /// </summary>
        public void Refresh()
        {
            _states.Clear();

            var inactive = _includeInactiveObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
            _states.AddRange(FindObjectsByType<TrainingFireSelectionState>(inactive, FindObjectsSortMode.None));

            Debug.Log($"[FireSourcesStateManager] Refresh() found {_states.Count} TrainingFireSelectionState objects in the scene. (Include Inactive? {_includeInactiveObjects})");
        }

        /// <summary>Inspector / designer list. Updating this field does not apply until you call <see cref="ApplySerializedMask"/> or re-enable the manager (if apply-on-enable is on).</summary>
        public List<FireClass> ActiveFireClasses
        {
            get => _activeFireClasses;
            set => _activeFireClasses = value;
        }

        /// <summary>Refreshes the registry, then selects each fire whose <see cref="TrainingFireSelectionState.TrainingFireClass"/> is included in <see cref="ActiveFireClasses"/>.</summary>
        [ContextMenu("Apply Active Fire Class List Now")]
        public void ApplySerializedMask()
        {
            Refresh();
            ApplyMaskToRegisteredStatesOnly();
        }

        /// <summary>Applies inclusion using an explicit list (does not change the serialized Inspector list).</summary>
        public void ApplyActiveFireMask(List<FireClass> classes)
        {
            Refresh();
            foreach (TrainingFireSelectionState state in _states)
                state.SetSelected(classes.Contains(state.TrainingFireClass), forceNotifyIfUnchanged: true);
        }

        /// <summary>Writes <paramref name="classes"/> to the serialized field and applies it (same as Inspector).</summary>
        public void SetActiveFireClassMaskAndApply(List<FireClass> classes)
        {
            _activeFireClasses = classes != null ? new List<FireClass>(classes) : new List<FireClass>();
            ApplySerializedMask();
        }

        private void ApplyMaskToRegisteredStatesOnly()
        {
            int activatedCount = 0;
            foreach (TrainingFireSelectionState state in _states)
            {
                bool shouldActivate = _activeFireClasses.Contains(state.TrainingFireClass);
                state.SetSelected(shouldActivate, forceNotifyIfUnchanged: true);
                if (shouldActivate) activatedCount++;
            }
            Debug.Log($"[FireSourcesStateManager] ApplyMaskToRegisteredStatesOnly: Applied {_activeFireClasses.Count} fire classes. Activated {activatedCount} out of {_states.Count} total firesources.");
        }

        /// <summary>
        /// Each registered fire is selected iff its <see cref="TrainingFireSelectionState.TrainingFireClass"/> is in
        /// <paramref name="activeClasses"/>.
        /// </summary>
        public void ApplyActiveFireClasses(ISet<FireClass> activeClasses)
        {
            if (activeClasses == null)
                throw new ArgumentNullException(nameof(activeClasses));

            foreach (TrainingFireSelectionState state in _states)
                state.SetSelected(activeClasses.Contains(state.TrainingFireClass), forceNotifyIfUnchanged: true);
        }

        /// <summary>Convenience overload: builds a set from <paramref name="activeClasses"/>.</summary>
        public void ApplyActiveFireClasses(params FireClass[] activeClasses)
        {
            if (activeClasses == null || activeClasses.Length == 0)
            {
                foreach (TrainingFireSelectionState state in _states)
                    state.SetSelected(false, forceNotifyIfUnchanged: true);
                return;
            }

            var set = new HashSet<FireClass>(activeClasses);
            ApplyActiveFireClasses(set);
        }

        /// <summary>Selects all registered fires.</summary>
        public void SelectAll()
        {
            foreach (TrainingFireSelectionState state in _states)
                state.SetSelected(true);
        }

        /// <summary>Deselects all registered fires.</summary>
        public void DeselectAll()
        {
            foreach (TrainingFireSelectionState state in _states)
                state.SetSelected(false);
        }

        /// <summary>
        /// Applies selection using a custom rule. Return <c>true</c> to include that fire in the session.
        /// </summary>
        public void ApplyPredicate(Func<TrainingFireSelectionState, bool> shouldBeSelected)
        {
            if (shouldBeSelected == null)
                throw new ArgumentNullException(nameof(shouldBeSelected));

            foreach (TrainingFireSelectionState state in _states)
                state.SetSelected(shouldBeSelected(state), forceNotifyIfUnchanged: true);
        }
    }
}
