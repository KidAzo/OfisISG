using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Kitchen-only: keeps smoke VFX visible for a short time after the fire is fully extinguished,
    /// then stops it. Fire particles still follow <c>FireVFXPresenter</c> intensity.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("Woi/Office Fire/Kitchen Fire Smoke Delayed Shutdown")]
    public sealed class KitchenFireSmokeDelayedShutdown : MonoBehaviour
    {
        [SerializeField]
        private FireSource fireSource;

        [SerializeField]
        private ParticleSystem[] smokeParticles;

        [SerializeField, Min(0f)]
        private float shutdownDelaySeconds = 1f;

        private bool _holdingSmoke;
        private float _shutdownAt;
        private ParticleSnapshot[] _snapshots = System.Array.Empty<ParticleSnapshot>();
        private Coroutine _shutdownRoutine;

        private struct ParticleSnapshot
        {
            public ParticleSystem System;
            public float EmissionRate;
            public float StartSize;
        }

        public void Configure(FireSource source, float delaySeconds = 1f)
        {
            if (source != null)
            {
                fireSource = source;
            }

            if (delaySeconds >= 0f)
            {
                shutdownDelaySeconds = delaySeconds;
            }
        }

        private void Awake()
        {
            ResolveDependencies();
            CacheSnapshots();
        }

        private void OnEnable()
        {
            if (fireSource == null)
            {
                return;
            }

            fireSource.OnFullyExtinguished += HandleFullyExtinguished;
        }

        private void OnDisable()
        {
            if (fireSource != null)
            {
                fireSource.OnFullyExtinguished -= HandleFullyExtinguished;
            }

            StopShutdownRoutine();
            _holdingSmoke = false;
        }

        private void LateUpdate()
        {
            if (!_holdingSmoke || Time.time >= _shutdownAt)
            {
                return;
            }

            KeepSmokeAlive();
        }

        private void HandleFullyExtinguished()
        {
            StopShutdownRoutine();
            CacheSnapshots();
            _holdingSmoke = true;
            _shutdownAt = Time.time + shutdownDelaySeconds;
            KeepSmokeAlive();
            _shutdownRoutine = StartCoroutine(ShutdownAfterDelay());
        }

        private IEnumerator ShutdownAfterDelay()
        {
            yield return new WaitForSeconds(shutdownDelaySeconds);
            _holdingSmoke = false;
            DisableSmoke();
            _shutdownRoutine = null;
        }

        private void ResolveDependencies()
        {
            if (fireSource == null)
            {
                fireSource = GetComponentInParent<FireSource>();
            }

            if (smokeParticles == null || smokeParticles.Length == 0)
            {
                smokeParticles = ResolveSmokeFromPresenter();
            }

            if (smokeParticles == null || smokeParticles.Length == 0)
            {
                smokeParticles = FindSmokeParticlesWithoutLight();
            }
        }

        private void KeepSmokeAlive()
        {
            for (int i = 0; i < _snapshots.Length; i++)
            {
                ParticleSystem ps = _snapshots[i].System;
                if (ps == null)
                {
                    continue;
                }

                if (!ps.gameObject.activeInHierarchy)
                {
                    ps.gameObject.SetActive(true);
                }

                ParticleSystem.EmissionModule emission = ps.emission;
                if (!emission.enabled)
                {
                    emission.enabled = true;
                }

                emission.rateOverTimeMultiplier = _snapshots[i].EmissionRate;

                ParticleSystem.MainModule main = ps.main;
                main.startSizeMultiplier = _snapshots[i].StartSize;
            }
        }

        private void DisableSmoke()
        {
            for (int i = 0; i < _snapshots.Length; i++)
            {
                ParticleSystem ps = _snapshots[i].System;
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.enabled = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void CacheSnapshots()
        {
            if (smokeParticles == null || smokeParticles.Length == 0)
            {
                _snapshots = System.Array.Empty<ParticleSnapshot>();
                return;
            }

            _snapshots = new ParticleSnapshot[smokeParticles.Length];
            for (int i = 0; i < smokeParticles.Length; i++)
            {
                ParticleSystem ps = smokeParticles[i];
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = ps.emission;
                ParticleSystem.MainModule main = ps.main;
                _snapshots[i] = new ParticleSnapshot
                {
                    System = ps,
                    EmissionRate = emission.rateOverTimeMultiplier,
                    StartSize = main.startSizeMultiplier
                };
            }
        }

        private ParticleSystem[] ResolveSmokeFromPresenter()
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().Name != "FireVFXPresenter")
                {
                    continue;
                }

                FieldInfo groupsField = behaviour.GetType().GetField(
                    "visualGroups",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (groupsField?.GetValue(behaviour) is not IList groups)
                {
                    continue;
                }

                var found = new List<ParticleSystem>(2);
                for (int g = 0; g < groups.Count; g++)
                {
                    object group = groups[g];
                    if (group == null)
                    {
                        continue;
                    }

                    Type groupType = group.GetType();
                    Light light = groupType.GetField("Light")?.GetValue(group) as Light;
                    ParticleSystem particle = groupType.GetField("Particle")?.GetValue(group) as ParticleSystem;
                    if (light == null && particle != null)
                    {
                        found.Add(particle);
                    }
                }

                return found.ToArray();
            }

            return System.Array.Empty<ParticleSystem>();
        }

        private ParticleSystem[] FindSmokeParticlesWithoutLight()
        {
            Transform searchRoot = fireSource != null ? fireSource.transform : transform;
            return CollectSmokeParticles(searchRoot.GetComponentsInChildren<ParticleSystem>(true));
        }

        private static ParticleSystem[] CollectSmokeParticles(ParticleSystem[] candidates)
        {
            var found = new List<ParticleSystem>(2);
            for (int i = 0; i < candidates.Length; i++)
            {
                ParticleSystem ps = candidates[i];
                Transform parent = ps.transform.parent;
                if (parent != null && parent.GetComponent<Light>() == null)
                {
                    found.Add(ps);
                }
            }

            return found.ToArray();
        }

        private void StopShutdownRoutine()
        {
            if (_shutdownRoutine == null)
            {
                return;
            }

            StopCoroutine(_shutdownRoutine);
            _shutdownRoutine = null;
        }
    }
}
