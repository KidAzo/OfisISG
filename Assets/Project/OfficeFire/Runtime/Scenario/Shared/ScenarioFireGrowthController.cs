using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    [Serializable]
    public sealed class FireGrowthStage
    {
        [Tooltip("This stage activates and scales these objects from 0 to their original local scale.")]
        public GameObject[] objects = Array.Empty<GameObject>();
    }

    /// <summary>
    /// Grows scenario fire visuals in timed stages after the player enters the room.
    /// Each stage enables its objects and animates local scale from 0 to the cached original scale.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Woi/Office Fire/Scenario Fire Growth")]
    public sealed class ScenarioFireGrowthController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("3 growth parts: index 0, 1, 2 play in order.")]
        private FireGrowthStage[] stages = Array.Empty<FireGrowthStage>();

        [Header("Timing")]
        [SerializeField]
        [Min(0f)]
        private float delayBeforeFirstStageSeconds;

        [SerializeField]
        [Min(0.1f)]
        private float secondsBetweenStages = 10f;

        [SerializeField]
        [Min(0.05f)]
        private float scaleUpDurationSeconds = 2f;

        [Header("Events")]
        [SerializeField]
        private UnityEvent<int> onStageStarted = new UnityEvent<int>();

        [SerializeField]
        private UnityEvent onAllStagesCompleted = new UnityEvent();

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLogs = true;

        private struct CachedObject
        {
            public Transform Transform;
            public Vector3 TargetLocalScale;
        }

        private CachedObject[][] _cachedStages = Array.Empty<CachedObject[]>();
        private Coroutine _growthRoutine;
        private bool _hasPrepared;
        private int _activeStageIndex = -1;

        public int ActiveStageIndex => _activeStageIndex;

        public int StageCount => stages != null ? stages.Length : 0;

        public bool IsGrowing => _growthRoutine != null;

        public event Action AllStagesCompleted;

        private void OnDestroy()
        {
            AllStagesCompleted = null;
        }

        private void Awake()
        {
            PrepareStages();
        }

        /// <summary>
        /// Starts the timed growth sequence from stage 0.
        /// </summary>
        public void BeginGrowth()
        {
            PrepareStages();

            if (_growthRoutine != null)
            {
                StopCoroutine(_growthRoutine);
            }

            _growthRoutine = StartCoroutine(GrowthRoutine());
            Log("Growth sequence started.");
        }

        /// <summary>
        /// Stops the sequence and hides all staged objects again.
        /// </summary>
        public void ResetGrowth()
        {
            if (_growthRoutine != null)
            {
                StopCoroutine(_growthRoutine);
                _growthRoutine = null;
            }

            _activeStageIndex = -1;
            HideAllStages();
            Log("Growth sequence reset.");
        }

        private void PrepareStages()
        {
            if (_hasPrepared)
            {
                return;
            }

            if (stages == null || stages.Length == 0)
            {
                LogWarning("Stages array is empty — assign 3 growth parts in the Inspector.");
                return;
            }

            _cachedStages = new CachedObject[stages.Length][];
            for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                FireGrowthStage stage = stages[stageIndex];
                GameObject[] objects = stage != null ? stage.objects : null;
                if (objects == null || objects.Length == 0)
                {
                    _cachedStages[stageIndex] = Array.Empty<CachedObject>();
                    continue;
                }

                CachedObject[] cachedObjects = new CachedObject[objects.Length];
                for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
                {
                    GameObject target = objects[objectIndex];
                    if (target == null)
                    {
                        continue;
                    }

                    Transform targetTransform = target.transform;
                    cachedObjects[objectIndex] = new CachedObject
                    {
                        Transform = targetTransform,
                        TargetLocalScale = targetTransform.localScale,
                    };

                    targetTransform.localScale = Vector3.zero;
                    target.SetActive(false);
                }

                _cachedStages[stageIndex] = cachedObjects;
            }

            _hasPrepared = true;
        }

        private IEnumerator GrowthRoutine()
        {
            if (delayBeforeFirstStageSeconds > 0f)
            {
                yield return new WaitForSeconds(delayBeforeFirstStageSeconds);
            }

            for (int stageIndex = 0; stageIndex < _cachedStages.Length; stageIndex++)
            {
                if (stageIndex > 0)
                {
                    yield return new WaitForSeconds(secondsBetweenStages);
                }

                yield return GrowStage(stageIndex);
            }

            _growthRoutine = null;
            onAllStagesCompleted?.Invoke();
            AllStagesCompleted?.Invoke();
            Log("All growth stages completed.");
        }

        private IEnumerator GrowStage(int stageIndex)
        {
            _activeStageIndex = stageIndex;
            CachedObject[] cachedObjects = _cachedStages[stageIndex];
            onStageStarted?.Invoke(stageIndex);
            Log($"Stage {stageIndex} started ({cachedObjects.Length} object(s)).");

            if (cachedObjects.Length == 0)
            {
                yield break;
            }

            for (int i = 0; i < cachedObjects.Length; i++)
            {
                CachedObject cached = cachedObjects[i];
                if (cached.Transform == null)
                {
                    continue;
                }

                cached.Transform.gameObject.SetActive(true);
                cached.Transform.localScale = Vector3.zero;
            }

            float elapsed = 0f;
            while (elapsed < scaleUpDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / scaleUpDurationSeconds);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                for (int i = 0; i < cachedObjects.Length; i++)
                {
                    CachedObject cached = cachedObjects[i];
                    if (cached.Transform == null)
                    {
                        continue;
                    }

                    cached.Transform.localScale = Vector3.LerpUnclamped(
                        Vector3.zero,
                        cached.TargetLocalScale,
                        smoothT);
                }

                yield return null;
            }

            for (int i = 0; i < cachedObjects.Length; i++)
            {
                CachedObject cached = cachedObjects[i];
                if (cached.Transform == null)
                {
                    continue;
                }

                cached.Transform.localScale = cached.TargetLocalScale;
            }
        }

        private void HideAllStages()
        {
            if (_cachedStages == null)
            {
                return;
            }

            for (int stageIndex = 0; stageIndex < _cachedStages.Length; stageIndex++)
            {
                CachedObject[] cachedObjects = _cachedStages[stageIndex];
                for (int i = 0; i < cachedObjects.Length; i++)
                {
                    CachedObject cached = cachedObjects[i];
                    if (cached.Transform == null)
                    {
                        continue;
                    }

                    cached.Transform.localScale = Vector3.zero;
                    cached.Transform.gameObject.SetActive(false);
                }
            }
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[ScenarioFireGrowth] {message}", this);
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.LogWarning($"[ScenarioFireGrowth] {message}", this);
        }
    }
}
