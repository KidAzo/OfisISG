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
        [Tooltip("Compensates for the 3D volume collapse of particles. 0.85 gives good separation at the bottom end. 1.0 is plain linear.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float volumetricFalloffPower = 0.85f;

        [Header("Tunable Parameters (Particles)")]
        [Tooltip("Emission multiplier at 0% intensity above disable threshold.")]
        [SerializeField] private float minEmissionMultiplier = 0.0f;
        [Tooltip("Emission multiplier at 100% intensity.")]
        [SerializeField] private float maxEmissionMultiplier = 1.0f;
        
        [Space]
        [Tooltip("Start Size multiplier at 0% intensity.")]
        [SerializeField] private float minSizeMultiplier = 0.1f;
        [Tooltip("Start Size multiplier at 100% intensity.")]
        [SerializeField] private float maxSizeMultiplier = 1.0f;

        [Header("Tunable Parameters (Lights)")]
        [Tooltip("Light intensity multiplier at 0% fire intensity.")]
        [SerializeField] private float minLightMultiplier = 0.1f;
        [Tooltip("Light intensity multiplier at 100% fire intensity.")]
        [SerializeField] private float maxLightMultiplier = 1.0f;
        
        [Space]
        [Tooltip("Raw intensity value at which emission and lights stop completely.")]
        [SerializeField] private float disableThreshold = 0.05f;

        private void Awake()
        {
            if (autoFindComponents)
            {
                AutoFindVisualGroups();
            }

            CacheInitialValues();
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
                    group.InitialEmissionMultiplier = group.Particle.emission.rateOverTimeMultiplier;
                    group.InitialStartSizeMultiplier = group.Particle.main.startSizeMultiplier;
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
            if (fireSource == null) return;

            UpdateVFX(fireSource.CurrentNormalizedIntensity);
        }

        private void UpdateVFX(float rawIntensity)
        {
            bool isAlive = rawIntensity > disableThreshold;

            // Volumetric Math Correction
            // Because particles occupy 3D space, shrinking linearly makes the fire disappear exponentially fast.
            // Using a power (like 0.5, which is square root) forces the fire to stay thicker at low intensities (e.g. 30% -> 55%).
            float visualIntensity = Mathf.Pow(rawIntensity, volumetricFalloffPower);

            float emissionFactor = Mathf.Lerp(minEmissionMultiplier, maxEmissionMultiplier, visualIntensity);
            float sizeFactor = Mathf.Lerp(minSizeMultiplier, maxSizeMultiplier, visualIntensity);
            
            // Light scales differently to the human eye, so we leave it slightly closer to linear
            float lightFactor = Mathf.Lerp(minLightMultiplier, maxLightMultiplier, rawIntensity);

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
                        if (emission.enabled) emission.enabled = false;
                    }
                    else
                    {
                        if (!emission.enabled) emission.enabled = true;

                        emission.rateOverTimeMultiplier = group.InitialEmissionMultiplier * emissionFactor;
                        main.startSizeMultiplier = group.InitialStartSizeMultiplier * sizeFactor;
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
