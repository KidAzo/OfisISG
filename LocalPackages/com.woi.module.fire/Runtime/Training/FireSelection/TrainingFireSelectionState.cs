using FireExtinguisher.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.Game.Training.FireSelection
{
    /// <summary>
    /// Game-layer session flag on a <see cref="FireSource"/> object. When not selected, the fire
    /// stays active in the scene but should be ignored by training reports, result UI, and aggregation.
    /// Wire <see cref="OnSelected"/> / <see cref="OnNotSelected"/> in the Inspector for VFX, audio, or UI.
    /// Events run when the selection <b>changes</b>, or when <see cref="SetSelected(bool, bool)"/> is called with
    /// <c>forceNotifyIfUnchanged: true</c> (used by <see cref="FireSourcesStateManager"/> so defaults that already
    /// match the active mask still run listeners such as <c>SetActive</c> and <see cref="WoiUtils.AudioSystem.AudioTrigger.Play"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FireSource))]
    [AddComponentMenu("WOI/Training/Training Fire Selection State")]
    public sealed class TrainingFireSelectionState : MonoBehaviour
    {
        [Header("Category")]
        [Tooltip("Fire class used when filtering by player choice at session start. " +
                 "Should match the logical type for this prop; can differ from FireData if you intentionally group props.")]
        [SerializeField] private FireClass _trainingFireClass = FireClass.A;

        [Header("Session inclusion")]
        [Tooltip("When false, this fire is excluded from training results and should not appear on the result screen or in per-fire report rows.")]
        [SerializeField] private bool _isSelected = true;

        [Header("Events")]
        [Tooltip("Invoked when this fire becomes included in the active session (on false→true), or when the mask is applied and already matched (see FireSourcesStateManager).")]
        [SerializeField] private UnityEvent _onSelected;

        [Tooltip("Invoked when this fire becomes excluded from the active session.")]
        [SerializeField] private UnityEvent _onNotSelected;

        /// <summary>Fire class used for session filtering / authoring.</summary>
        public FireClass TrainingFireClass
        {
            get => _trainingFireClass;
            set => _trainingFireClass = value;
        }

        /// <summary>True when this fire counts for the current training session.</summary>
        public bool IsCurrentlySelected => _isSelected;

        /// <summary>Same as <see cref="IsCurrentlySelected"/> (readable alias for callers and docs).</summary>
        public bool IsSelected => _isSelected;

        /// <summary>True when this fire is excluded from session aggregation and UI.</summary>
        public bool NotSelected => !_isSelected;

        /// <summary>Inspector hook for <see cref="_onSelected"/>.</summary>
        public UnityEvent OnSelected => _onSelected;

        /// <summary>Inspector hook for <see cref="_onNotSelected"/>.</summary>
        public UnityEvent OnNotSelected => _onNotSelected;

        /// <summary>Includes this fire in the active session and raises <see cref="OnSelected"/> if state changed.</summary>
        public void SelectFire() => SetSelected(true);

        /// <summary>Excludes this fire from the active session and raises <see cref="OnNotSelected"/> if state changed.</summary>
        public void DeselectFire() => SetSelected(false);

        /// <summary>
        /// Sets inclusion and raises <see cref="OnSelected"/> / <see cref="OnNotSelected"/> on transition.
        /// </summary>
        /// <param name="selected">New inclusion flag.</param>
        /// <param name="forceNotifyIfUnchanged">
        /// When true and <paramref name="selected"/> already equals the current flag, still invokes the matching UnityEvent.
        /// Use from bulk sync (e.g. <see cref="FireSourcesStateManager"/>) so Inspector defaults that already match the mask
        /// still run wired actions (enable VFX, <see cref="WoiUtils.AudioSystem.AudioTrigger.Play"/>, etc.).
        /// </param>
        public void SetSelected(bool selected, bool forceNotifyIfUnchanged = false)
        {
            if (_isSelected == selected)
            {
                if (!forceNotifyIfUnchanged)
                    return;

                if (_isSelected)
                    _onSelected?.Invoke();
                else
                    _onNotSelected?.Invoke();
                return;
            }

            _isSelected = selected;

            if (_isSelected)
                _onSelected?.Invoke();
            else
                _onNotSelected?.Invoke();
        }
    }

    /// <summary>
    /// Helpers for reporting and UI: query inclusion without referencing the component type everywhere.
    /// </summary>
    public static class TrainingFireSelectionQueries
    {
        /// <summary>
        /// True if this <see cref="FireSource"/> should participate in session reports and result UI.
        /// If no <see cref="TrainingFireSelectionState"/> is present, the fire is treated as included (backward compatible).
        /// </summary>
        public static bool IsIncludedInTrainingSession(FireSource source)
        {
            if (source == null)
                return false;

            if (!source.TryGetComponent(out TrainingFireSelectionState state))
                return true;

            return state.IsCurrentlySelected;
        }

        /// <summary>True if excluded from session (convenience for <c>!IsIncludedInTrainingSession</c> with null-safe source).</summary>
        public static bool IsExcludedFromTrainingSession(FireSource source)
            => source != null && !IsIncludedInTrainingSession(source);

        /// <summary>
        /// Tries to get selection state on the same GameObject as <paramref name="source"/>.
        /// </summary>
        public static bool TryGetSelectionState(FireSource source, out TrainingFireSelectionState state)
        {
            state = null;
            return source != null && source.TryGetComponent(out state);
        }
    }
}
