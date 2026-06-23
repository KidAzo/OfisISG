using System.Reflection;
using UnityEngine;

namespace Woi.Game
{
    /// <summary>
    /// Works with both Assets/QuickOutline and package QuickOutline script copies.
    /// </summary>
    internal static class QuickOutlineBehaviour
    {
        const string OutlineTypeName = "Outline";

        static PropertyInfo _colorProperty;
        static PropertyInfo _widthProperty;
        static System.Type _cachedType;

        public static void Ensure(ref MonoBehaviour outline, GameObject host)
        {
            if (IsOutline(outline))
                return;

            outline = FindOn(host);
        }

        public static MonoBehaviour FindOn(GameObject host)
        {
            if (host == null)
                return null;

            MonoBehaviour[] behaviours = host.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (IsOutline(behaviours[i]))
                    return behaviours[i];
            }

            return null;
        }

        public static bool IsOutline(MonoBehaviour behaviour) =>
            behaviour != null && behaviour.GetType().Name == OutlineTypeName;

        public static void Show(MonoBehaviour outline, Color color, float width)
        {
            if (!IsOutline(outline))
                return;

            outline.enabled = true;
            SetColor(outline, color);
            SetWidth(outline, width);
            RefreshMaterialProperties(outline);
        }

        public static void Hide(MonoBehaviour outline)
        {
            if (!IsOutline(outline))
                return;

            SetWidth(outline, 0f);
            outline.enabled = false;
        }

        static void SetColor(MonoBehaviour outline, Color color)
        {
            CacheProperties(outline.GetType());
            _colorProperty?.SetValue(outline, color);
        }

        static void SetWidth(MonoBehaviour outline, float width)
        {
            CacheProperties(outline.GetType());
            _widthProperty?.SetValue(outline, width);
        }

        static void RefreshMaterialProperties(MonoBehaviour outline)
        {
            MethodInfo update = outline.GetType().GetMethod(
                "UpdateMaterialProperties",
                BindingFlags.Instance | BindingFlags.NonPublic);

            update?.Invoke(outline, null);
        }

        static void CacheProperties(System.Type type)
        {
            if (_cachedType == type && _colorProperty != null)
                return;

            _cachedType = type;
            _colorProperty = type.GetProperty("OutlineColor", BindingFlags.Instance | BindingFlags.Public);
            _widthProperty = type.GetProperty("OutlineWidth", BindingFlags.Instance | BindingFlags.Public);
        }
    }
}
