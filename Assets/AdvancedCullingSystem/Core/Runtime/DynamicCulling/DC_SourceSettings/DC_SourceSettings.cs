using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    public enum CullingMethod { FullDisable, KeepShadows }
    public enum SourceType { SingleMesh, LODGroup, Custom } 

    [DisallowMultipleComponent]
    public class DC_SourceSettings : MonoBehaviour
    {
        public bool ReadyForCulling
        {
            get
            {
                return _strategy != null && _strategy.ReadyForCulling;
            }
        }
        public int CullingLayer
        {
            get
            {
                return DC_Controller.GetCullingLayer();
            }
        }
        public SourceType SourceType
        {
            get
            {
                return _sourceType;
            }
            set
            {
                if (value == _sourceType)
                    return;

                _sourceType = value;

                OnSourceTypeChanged();
            }
        }
        
        [field: SerializeField]
        public int ControllerID { get; set; }

        [field: SerializeField]
        public bool IsIncompatible { get; private set; }

        [field: SerializeField]
        public string IncompatibilityReason { get; private set; }

        [SerializeField]
        private SourceType _sourceType;

        [SerializeReference]
        private IDC_SourceSettingsStrategy _strategy;


        private void Reset()
        {
            DetectSourceType();
            CheckCompatibility();
        }

        private void Awake()
        {
            if (_strategy == null)
                CreateStrategy();
        }

        private void Start()
        {
            try
            {
                if (!CheckCompatibility())
                {
                    enabled = false;
                    return;
                }

                if (!_strategy.ReadyForCulling)
                    _strategy.PrepareForCulling();

                DC_Controller.GetById(ControllerID).AddObjectForCulling(
                    _strategy.CreateCullingTarget(),
                    _strategy.GetColliders());

                Destroy(this);
            }
            catch (Exception ex)
            {
                IsIncompatible = true;
                IncompatibilityReason = ex.Message + ex.StackTrace;
            }
        }


        public T GetStrategy<T>() where T : IDC_SourceSettingsStrategy
        {
            return (T)_strategy;
        }

        public bool TryGetBounds(ref Bounds bounds)
        {
            if (_strategy == null)
                return false;

            return _strategy.TryGetBounds(ref bounds);
        }

        public bool CheckCompatibility()
        {
            if (_strategy == null)
                CreateStrategy();

            IsIncompatible = !_strategy.CheckCompatibilityAndGetComponents(out string reason);
            IncompatibilityReason = reason;

            return !IsIncompatible;
        }

        public void Bake()
        {
            if (Application.isPlaying)
            {
                Debug.Log("'Bake' can only be called in editor mode");
                return;
            }

            if (_strategy != null && _strategy.ReadyForCulling)
                _strategy.ClearData();

            if (CheckCompatibility())
                _strategy.PrepareForCulling();
        }

        public void ClearBakedData()
        {
            if (Application.isPlaying)
            {
                Debug.Log("'ClearBakedData' can only be called in editor mode");
                return;
            }

            _strategy?.ClearData();
        }


        private void DetectSourceType()
        {
            if (GetComponent<LODGroup>() != null)
                SourceType = SourceType.LODGroup;

            else if (GetComponent<MeshRenderer>() != null)
                SourceType = SourceType.SingleMesh;

            else
                SourceType = SourceType.Custom;
        }

        private void OnSourceTypeChanged()
        {
            if (_strategy != null && _strategy.ReadyForCulling)
                _strategy.ClearData();

            CreateStrategy();
            CheckCompatibility();
        }

        private void CreateStrategy()
        {
            if (SourceType == SourceType.SingleMesh)
            {
                _strategy = new DC_RendererSourceSettingsStrategy(this);
            }
            else if (SourceType == SourceType.LODGroup)
            {
                _strategy = new DC_LODGroupSourceSettingsStrategy(this);
            }
            else if (SourceType == SourceType.Custom)
            {
                _strategy = new DC_CustomSourceSettingsStrategy(this);
            }
            else
                throw new NotSupportedException();
        }
    }
}
