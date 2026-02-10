using UnityEngine;
using Woi.HazardSystem;
using PrimeTween;
using System;

public static class HazardFeedback
{
    public static void SetScale(GameObject[] objects, float scaleTarget, bool isTweenRequested, Ease ease, Action onComplete = null)
    {
        if (objects == null || objects.Length == 0)
            return;

            if (isTweenRequested)
            {
                int completedTweens = 0;
                int totalObjects = objects.Length;

                foreach (var obj in objects)
                {
                    float duration = UnityEngine.Random.Range(0.3f, 0.5f);

                    Tween.Scale(obj.transform, scaleTarget, duration, ease)
                        .OnComplete(() =>
                        {
                            completedTweens++;
                            if (completedTweens >= totalObjects)
                            {
                                onComplete?.Invoke();
                            }
                        });
                }
            }
            else
            {
                foreach (var obj in objects)
                {
                    obj.transform.localScale = Vector3.one * scaleTarget;
                }
                onComplete?.Invoke();
            }
    }
}
