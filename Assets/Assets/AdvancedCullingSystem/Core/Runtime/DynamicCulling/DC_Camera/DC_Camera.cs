using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    [RequireComponent(typeof(Camera))]
    public partial class DC_Camera : MonoBehaviour
    {
        [SerializeField]
        private int _raysCount = 1500;

        [SerializeField]
        private DistributionMethod _raysDistribution = DistributionMethod.R2;

        [Space]

        [Range(0, 90)]
        [SerializeField]
        private int _fovAddition = 5;

        [SerializeField]
        private bool _autoCheckChanges = false;

#if UNITY_EDITOR

        [Space]
        [SerializeField]
        private bool _DEBUG_RAYS = false;

#endif

        private IReadOnlyDictionary<Collider, IHitable> _hitablesDic;

        private Camera _camera;
        private DC_CameraSettings _settings;
        private bool _updateSettings;
        private bool _updateRaysCount;
        private int _newRaysCount;

        private Vector3[] _rayDirs;

        private NativeArray<RaycastCommand> _rayCommands;
        private NativeArray<RaycastHit> _rayHits;
        private JobHandle _jobHandle;
        private int _layerMask;
        private int _currentRay;
        private bool _cameraEnabled;


        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _newRaysCount = _raysCount;
        }

        private void Start()
        {
            _hitablesDic = DC_Controller.GetHitables();
            _layerMask = LayerMask.GetMask(DC_Controller.GetCullingLayerName());

            _updateRaysCount = true;
            _updateSettings = true;
        }

        private void Update()
        {
            _cameraEnabled = _camera.enabled && gameObject.activeInHierarchy;

            if (!_cameraEnabled)
                return;

            UpdateIfNeeded();

            int totalCount = _rayDirs.Length;
            float distance = _settings.farPlane;

            Vector3 origin = _camera.transform.position;
            Matrix4x4 matrix = _camera.transform.localToWorldMatrix;

            for (int i = 0; i < _raysCount; i++)
            {            
                _rayCommands[i] = UnityAPI.NewRaycastCommand(origin, 
                    matrix.MultiplyVector(_rayDirs[_currentRay]), distance, _layerMask);

                _currentRay++;

                if (_currentRay >= totalCount)
                    _currentRay = 0;
            }

            _jobHandle = RaycastCommand.ScheduleBatch(_rayCommands, _rayHits, 1);
        }

        private void LateUpdate()
        {
            if (!_cameraEnabled)
                return;

            _jobHandle.Complete();

            #region DEBUG_RAYS_REGION
#if UNITY_EDITOR

            if (_DEBUG_RAYS)
            {
                for (int i = 0; i < _raysCount; i++)
                {
                    Collider collider = _rayHits[i].collider;

                    if (collider != null)
                    {
                        if (_hitablesDic.TryGetValue(collider, out IHitable hitable))
                            hitable.OnHit();
                    }

                    Debug.DrawLine(_rayCommands[i].from, _rayHits[i].point, Color.green);
                }

                return;
            }

#endif
            #endregion

            for (int i = 0; i < _raysCount; i++)
            {
                Collider collider = _rayHits[i].collider;

                if (collider != null)
                {
                    if (_hitablesDic.TryGetValue(collider, out IHitable hitable))
                        hitable.OnHit();
                }
            }
        }

        private void OnDestroy()
        {
            if (_rayCommands.IsCreated)
                _rayCommands.Dispose();

            if (_rayHits.IsCreated)
                _rayHits.Dispose();
        }


        public void CameraSettingsChanged()
        {
            _updateSettings = true;
        }

        public void SetRaysCount(int count)
        {
            _updateRaysCount = true;
            _newRaysCount = count;
        }


        private bool IsCameraSettingsChanged()
        {
            if (_settings.width != _camera.pixelWidth)
                return true;

            if (_settings.height != _camera.pixelHeight)
                return true;

            if (_settings.fov != _camera.fieldOfView)
                return true;

            if (_settings.farPlane != _camera.farClipPlane)
                return true;

            return false;
        }

        private void UpdateIfNeeded()
        {
            if (!_updateSettings && _autoCheckChanges)
            {
                if (IsCameraSettingsChanged())
                    _updateSettings = true;
            }

            if (_updateSettings)
            {
                UpdateCameraSettings();
                _updateSettings = false;
            }

            if (_updateRaysCount)
            {
                UpdateRaysCount(_newRaysCount);
                _updateRaysCount = false;
            }
        }

        private void UpdateRaysCount(int count)
        {
            if (_rayCommands.IsCreated)
                _rayCommands.Dispose();

            if (_rayHits.IsCreated)
                _rayHits.Dispose();

            _rayCommands = new NativeArray<RaycastCommand>(count, Allocator.Persistent);
            _rayHits = new NativeArray<RaycastHit>(count, Allocator.Persistent);
            _raysCount = _newRaysCount;
        }

        private void UpdateCameraSettings()
        {
            _rayDirs = DC_CameraUtil.GetRaysDirections(_camera, _raysDistribution, _fovAddition);
            _settings = new DC_CameraSettings(_camera);
        }
    }
}
