using System;
using System.Collections.Generic;
using UnityEngine;
using Woi.Events;

namespace Woi.Porting
{
    public enum AppMode { PC, XR }

    public interface IModeParticipant
    {
        void OnBeforeModeChange(AppMode from, AppMode to);

        void OnAfterModeChange(AppMode mode);
    }

    public interface IPortingService
    {
        AppMode CurrentMode { get; }
        event Action<AppMode, AppMode> OnModeChanging;
        event Action<AppMode> OnModeChanged;

        void Register(IModeParticipant participant);
        void Unregister(IModeParticipant participant);
        void Toggle();
        void SetMode(AppMode mode);
    }

    public sealed class PortingController : MonoBehaviour, IPortingService
    {
        public event Action<AppMode, AppMode> OnModeChanging;
        public event Action<AppMode> OnModeChanged;

        [SerializeField] ScriptableEnumPortingVariable currentMode;

        readonly List<IModeParticipant> _participants = new();
        bool _isSwitching;

        public AppMode CurrentMode { get; private set; }

        void Awake()
        {
            CurrentMode = currentMode.Value;
        }

        void Start()
        {
            SetMode(currentMode.Value);
        }

        void OnEnable()
        {
            EventBus.Subscribe<OnSceneGroupLoaded>(OnSceneGroupLoadedHandler);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<OnSceneGroupLoaded>(OnSceneGroupLoadedHandler);
        }

        void OnSceneGroupLoadedHandler(OnSceneGroupLoaded evt)
        {
            SetMode(currentMode.Value);
        }

        /// <summary>
        /// Scene’deki participant’ları otomatik toplamak istersen çağır.
        /// (Örn: scene load sonrası)
        /// </summary>
        public void AutoRegisterFromScene()
        {
            _participants.Clear();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb is IModeParticipant p) _participants.Add(p);
            }
        }

        public void Register(IModeParticipant participant)
        {
            if (participant == null) return;
            if (_participants.Contains(participant)) return;
            _participants.Add(participant);
        }

        public void Unregister(IModeParticipant participant)
        {
            if (participant == null) return;
            _participants.Remove(participant);
        }

        public void Toggle()
        {
            SetMode(CurrentMode == AppMode.PC ? AppMode.XR : AppMode.PC);
        }

        public void SetMode(AppMode mode)
        {
            if (_isSwitching) return;

            var from = CurrentMode;
            var to = mode;

            _isSwitching = true;

            // 1) before callbacks
            OnModeChanging?.Invoke(from, to);
            for (int i = 0; i < _participants.Count; i++)
                SafeBefore(_participants[i], from, to);

            // 2) state update + apply
            CurrentMode = to;

            // 3) after callbacks
            for (int i = 0; i < _participants.Count; i++)
                SafeAfter(_participants[i], to);

            OnModeChanged?.Invoke(to);

            _isSwitching = false;
        }

        static void SafeBefore(IModeParticipant p, AppMode from, AppMode to)
        {
            try { p?.OnBeforeModeChange(from, to); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void SafeAfter(IModeParticipant p, AppMode mode)
        {
            try { p?.OnAfterModeChange(mode); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }


}
