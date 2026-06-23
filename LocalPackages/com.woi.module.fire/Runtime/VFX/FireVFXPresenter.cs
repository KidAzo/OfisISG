using System.Collections.Generic;
using UnityEngine;
using FireExtinguisher.Core;

namespace Woi.VFX
{
    [System.Serializable]
    public struct FireVisualGroup
    {
        public ParticleSystem Particle;
        public Light Light;

        // Hidden fields to cache initial settings during Awake
        [HideInInspector] public float InitialEmissionMultiplier;
        [HideInInspector] public float InitialStartSizeMultiplier;
        [HideInInspector] public float InitialLightIntensity;
        [HideInInspector] public Vector3 InitialLocalScale;
    }

    // Note: Assuming 'FireSource' is in an accessible namespace and has 
    // a public property 'CurrentNormalizedIntensity'. 
    // Update the namespace or add 'using' directives if needed.
    public class FireVFXPresenter : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private FireSource fireSource;
        [SerializeField] private bool autoFindComponents = true;
        
        [Header("Visual Groups")]
        [Tooltip("Paired Particle and Light components.")]
        [SerializeField] private List<FireVisualGroup> visualGroups = new List<FireVisualGroup>();

        [Header("Math Settings")]
        [Tooltip("1 = VFX matches suppression % exactly. Values below 1 keep flames thicker at low intensity.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float volumetricFalloffPower = 1f;

        [Header("Tunable Parameters (Particles)")]
        [Tooltip("Emission multiplier at 0% intensity above disable threshold.")]
        [SerializeField] private float minEmissionMultiplier = 0.0f;
        [Tooltip("Emission multiplier at 100% intensity.")]
        [SerializeField] private float maxEmissionMultiplier = 1.0f;
        
        [Space]
        [Tooltip("Start Size multiplier at 0% intensity.")]
        [SerializeField] private float minSizeMultiplier = 0f;
        [Tooltip("Start Size multiplier at 100% intensity.")]
        [SerializeField] private float maxSizeMultiplier = 1.0f;

        [Header("Tunable Parameters (Lights)")]
        [Tooltip("Light intensity multiplier at 0% fire intensity.")]
        [SerializeField] private float minLightMultiplier = 0f;
        [Tooltip("Light intensity multiplier at 100% fire intensity.")]
        [SerializeField] private float maxLightMultiplier = 1.0f;
        
        [Space]
        [Tooltip("Raw intensity value at which emission and lights stop completely.")]
        [SerializeField] private float disableThreshold = 0.01f;

        [Header("Instant extinguish (blanket, etc.)")]
        [Tooltip("Optional puff played when fire is snapped off (e.g. steam).")]
        [SerializeField] private ParticleSystem _extinguishPuff;

        bool _visualsSuppressed;

        private void Awake()
        {
            if (fireSource == null)
                fireSource = GetComponentInParent<FireSource>();

            if (autoFindComponents)
                AutoFindVisualGroups();

            CacheInitialValues();
        }

        private void OnEnable()
        {
            if (fireSource == null)
                return;

            fireSource.OnIntensityChanged += HandleFireIntensityChanged;
            fireSource.OnFullyExtinguished += HandleFullyExtinguished;
        }

        private void OnDisable()
        {
            if (fireSource == null)
                return;

            fireSource.OnIntensityChanged -= HandleFireIntensityChanged;
            fireSource.OnFullyExtinguished -= HandleFullyExtinguished;
        }

        void HandleFireIntensityChanged(float normalizedIntensity) => UpdateVFX(normalizedIntensity);

        void HandleFullyExtinguished()
        {
            if (!_visualsSuppressed)
                PlayExtinguishPuff();
        }

        /// <summary>Cuts fire visuals immediately (emergency / legacy). Prefer gradual zone drain for normal gameplay.</summary>
        public void SnapExtinguished()
        {
            _visualsSuppressed = true;
            UpdateVFX(0f);
            StopAllFireVisuals(clearParticles: true);
            PlayExtinguishPuff();
        }

        /// <summary>One-shot puff when intensity reaches zero after gradual suppression.</summary>
        public void PlayExtinguishPuff()
        {
            if (_extinguishPuff == null)
                return;

            _extinguishPuff.gameObject.SetActive(true);
            _extinguishPuff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _extinguishPuff.Play(true);
        }

        void StopAllFireVisuals(bool clearParticles)
        {
            ParticleSystem[] allParticles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < allParticles.Length; i++)
            {
                ParticleSystem ps = allParticles[i];
                if (ps == null || ps == _extinguishPuff)
                    continue;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.enabled = false;
                ps.Stop(
                    true,
                    clearParticles
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }

            Light[] allLights = GetComponentsInChildren<Light>(true);
            for (int i = 0; i < allLights.Length; i++)
            {
                if (allLights[i] != null)
                    allLights[i].enabled = false;
            }
        }

        [ContextMenu("Find Components Now (Editor)")]
        private void AutoFindVisualGroups()
        {
            visualGroups.Clear();

            // Group components by their shared parent (e.g., 'Fire (9)' parent)
            var parentMap = new Dictionary<Transform, FireVisualGroup>();

            var allParticles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticles)
            {
                var parent = ps.transform.parent;
                if (!parentMap.TryGetValue(parent, out var group))
                    group = new FireVisualGroup();

                group.Particle = ps;
                parentMap[parent] = group;
            }

            var allLights = GetComponentsInChildren<Light>(true);
            foreach (var lmt in allLights)
            {
                var parent = lmt.transform.parent;
                if (!parentMap.TryGetValue(parent, out var group))
                    group = new FireVisualGroup();

                group.Light = lmt;
                parentMap[parent] = group;
            }

            // Populate the list with the paired groups
            visualGroups.AddRange(parentMap.Values);
        }

        private void CacheInitialValues()
        {
            for (int i = 0; i < visualGroups.Count; i++)
            {
                var group = visualGroups[i];

                if (group.Particle != null)
                {
                    float emission = group.Particle.emission.rateOverTimeMultiplier;
                    group.InitialEmissionMultiplier = emission > 0f
                        ? emission
                        : Mathf.Max(0.01f, group.Particle.emission.rateOverTime.constant);
                    group.InitialStartSizeMultiplier = Mathf.Max(0.01f, group.Particle.main.startSizeMultiplier);
                    group.InitialLocalScale = group.Particle.transform.localScale;
                }

                if (group.Light != null)
                {
                    group.InitialLightIntensity = group.Light.intensity;
                }

                // Since struct is a value type, re-assign it back to the list
                visualGroups[i] = group;
            }
        }

        private void Update()
        {
            if (_visualsSuppressed || fireSource == null)
                return;

            UpdateVFX(fireSource.CurrentNormalizedIntensity);
        }

        private void UpdateVFX(float rawIntensity)
        {
            float t = Mathf.Clamp01(rawIntensity);
            bool isAlive = t > disableThreshold;

            // Default power = 1 → VFX tracks suppression % one-to-one (emission / size / light).
            float visualIntensity = Mathf.Approximately(volumetricFalloffPower, 1f)
                ? t
                : Mathf.Pow(t, volumetricFalloffPower);

            float emissionFactor = Mathf.Lerp(minEmissionMultiplier, maxEmissionMultiplier, visualIntensity);
            float sizeFactor = Mathf.Lerp(minSizeMultiplier, maxSizeMultiplier, visualIntensity);
            float lightFactor = Mathf.Lerp(minLightMultiplier, maxLightMultiplier, visualIntensity);

            for (int i = 0; i < visualGroups.Count; i++)
            {
                var group = visualGroups[i];

                // Update ParticleSystem
                if (group.Particle != null)
                {
                    var emission = group.Particle.emission;
                    var main = group.Particle.main;

                    if (!isAlive)
                    {
                        if (emission.enabled)
                            emission.enabled = false;

                        if (group.Particle.isPlaying)
                        {
                            group.Particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        }
                    }
                    else
                    {
                        if (!emission.enabled) emission.enabled = true;

                        emission.rateOverTimeMultiplier = group.InitialEmissionMultiplier * emissionFactor;
                        main.startSizeMultiplier = group.InitialStartSizeMultiplier * sizeFactor;
                        group.Particle.transform.localScale = group.InitialLocalScale * sizeFactor;
                    }
                }

                // Update Light
                if (group.Light != null)
                {
                    if (!isAlive)
                    {
                        if (group.Light.enabled) group.Light.enabled = false;
                    }
                    else
                    {
                        if (!group.Light.enabled) group.Light.enabled = true;

                        group.Light.intensity = group.InitialLightIntensity * lightFactor;
                    }
                }
            }
        }
    }
}
