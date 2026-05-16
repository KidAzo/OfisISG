using UnityEngine;

/// <summary>
/// VR’de raycast tabanlı etkileşimlerin (şalter, acil buton, Class C vanası) kullandığı dünya ışını.
/// ExtinguisherHoverTransformRaycaster ile aynı transform ve başlangıç inset’i kaydedilir;
/// böylece tüp hover ile aynı nişan üzerinden Interact doğrulaması yapılır. PC’de kullanılmaz.
/// </summary>
public static class FireVrGameplayInteractionRay
{
    static Transform s_RayOrigin;
    static float s_StartInsetMeters;
    static object s_RegisteredOwner;

    /// <summary>
    /// Genelde <c>ExtinguisherHoverTransformRaycaster</c> <see cref="MonoBehaviour"/> örneği <paramref name="owner"/> olur.
    /// </summary>
    public static void Register(object owner, Transform rayOrigin, float rayStartInsetMeters)
    {
        if (owner == null || rayOrigin == null)
            return;

        s_RegisteredOwner = owner;
        s_RayOrigin = rayOrigin;
        s_StartInsetMeters = rayStartInsetMeters;
    }

    public static void Unregister(object owner)
    {
        if (owner == null || s_RegisteredOwner != owner)
            return;

        s_RegisteredOwner = null;
        s_RayOrigin = null;
        s_StartInsetMeters = 0f;
    }

    /// <summary>
    /// Kayıtlı nişan kökü varken (ExtinguisherHoverTransformRaycaster) başlangıç ve yön (normalize).
    /// </summary>
    public static bool TryGetRay(out Vector3 origin, out Vector3 directionNormalized)
    {
        origin = default;
        directionNormalized = default;

        if (s_RayOrigin == null)
            return false;

        Vector3 dir = s_RayOrigin.forward;
        if (dir.sqrMagnitude < 1e-8f)
            return false;

        dir.Normalize();
        origin = s_RayOrigin.position + dir * Mathf.Max(0f, s_StartInsetMeters);
        directionNormalized = dir;

        if (!float.IsFinite(origin.x) || !float.IsFinite(origin.y) || !float.IsFinite(origin.z)
            || !float.IsFinite(directionNormalized.x) || !float.IsFinite(directionNormalized.y)
            || !float.IsFinite(directionNormalized.z))
        {
            origin = default;
            directionNormalized = default;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Kayıtlı XR nişan kökü (ör. kontrolcü); yoksa null. Raycast’te kendi mesh isabetlerini atlamak için.
    /// </summary>
    public static Transform RegisteredRayOriginOrNull => s_RayOrigin;
}
