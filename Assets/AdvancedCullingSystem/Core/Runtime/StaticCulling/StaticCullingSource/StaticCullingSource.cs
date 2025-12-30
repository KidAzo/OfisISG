using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public enum SourceType { MeshRenderer, LODGroup, Light, Custom }

    [DisallowMultipleComponent]
    public class StaticCullingSource : MonoBehaviour
    {
        public SourceType SourceType
        {
            get
            {
                return _sourceType;
            }
            set
            {
                _sourceType = value;

                OnSourceTypeChanged();
            }
        }
        public string ValidationError
        {
            get
            {
                return _validationError;
            }
        }
        public CullingTarget CullingTarget
        {
            get
            {
                return _target;
            }
        }
        public IStaticCullingSourceStrategy Strategy
        {
            get
            {
                return _strategy;
            }
        }

        [SerializeField]
        private SourceType _sourceType;

        [SerializeField]
        private string _validationError;

        [SerializeField]
        private CullingTarget _target;

        [SerializeReference]
        private IStaticCullingSourceStrategy _strategy;


        private void Reset()
        {
            AutoDetectSourceType();
            CreateStrategy();
            Validate();
        }

        private void AutoDetectSourceType()
        {
            if (GetComponent<MeshRenderer>() != null)
            {
                _sourceType = SourceType.MeshRenderer;
                return;
            }

            if (GetComponent<LODGroup>() != null)
            {
                _sourceType = SourceType.LODGroup;
                return;
            }

            if (GetComponent<Light>() != null)
            {
                _sourceType = SourceType.Light;
                return;
            }

            _sourceType = SourceType.Custom;
        }

        private void OnSourceTypeChanged()
        {
            _validationError = "";

            CreateStrategy();
            Validate();
        }

        private void CreateStrategy()
        {
            if (_sourceType == SourceType.MeshRenderer)
                _strategy = new MeshRendererStaticCullingSourceStrategy(gameObject);

            else if (_sourceType == SourceType.LODGroup)
                _strategy = new LODGroupStaticCullingSourceStrategy(gameObject);

            else if (_sourceType == SourceType.Light)
                _strategy = new LightStaticCullingSourceStrategy(gameObject);

            else if (_sourceType == SourceType.Custom)
                _strategy = new CustomStaticCullingSourceStrategy(gameObject);
        }


        public bool Validate()
        {
            if (_strategy == null)
            {
                AutoDetectSourceType();
                CreateStrategy();
            }

            _validationError = "";

            return _strategy.Validate(out _validationError);
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            return _strategy.TryGetBounds(out bounds);
        }

        public void PrepareForBaking()
        {
            if (_validationError != "")
            {
                if (!Validate())
                    throw new Exception("StaticCullingSource::" + gameObject.name + " has validation errors");
            }

            _target = _strategy.CreateCullingTarget();
            
            _strategy.PrepareForBaking();
        }

        public void ClearAfterBaking()
        {
            _strategy.ClearAfterBaking();
        }


#if UNITY_EDITOR

        public static bool DrawGizmoRenderers;
        public static bool DrawGizmoLODGroups;
        public static bool DrawGizmoLights;
        public static bool DrawGizmoCustom;

        private void OnDrawGizmos()
        {
            if (_sourceType == SourceType.MeshRenderer && DrawGizmoRenderers)
                DrawGizmo(Color.blue);

            else if (_sourceType == SourceType.LODGroup && DrawGizmoLODGroups)
                DrawGizmo(Color.yellow);

            else if (_sourceType == SourceType.Light && DrawGizmoLights)
                DrawGizmo(Color.white);

            else if (_sourceType == SourceType.Custom && DrawGizmoCustom)
                DrawGizmo(Color.green);
        }

        private void DrawGizmo(Color color)
        {
            if (!TryGetBounds(out Bounds bounds))
                return;

            if (ValidationError != "")
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
            else
            {
                Gizmos.color = color;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
#endif
    }
}
