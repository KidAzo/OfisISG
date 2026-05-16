using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Woi.UI.Result
{
    /// <summary>
    /// Exit panel EVET/HAYIR için XR UI Toolkit + <see cref="NearFarInteractor"/> yolunu kurar.
    /// Projede yalnızca NearFarInteractor kullanıldığı varsayımıyla: UI ışın etkileşimi ve raycast mask açılır.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class ExitPanelNearFarUiBootstrap : MonoBehaviour
    {
        [Header("NearFarInteractor")]
        [Tooltip("Boşsa sahnede NearFarInteractor aranır; birden fazlaysa isimde bu metin geçen (ör. Right) tercih edilir.")]
        [SerializeField]
        string preferInteractorNameContains = "Right";

        [Tooltip("Atanırsa sadece bu NearFarInteractor yapılandırılır; boşsa sahne taraması.")]
        [SerializeField]
        NearFarInteractor nearFarInteractorOverride;

        [Tooltip("True ise override boşken sahnedeki tüm NearFarInteractor bileşenleri UI için açılır.")]
        [SerializeField]
        bool includeAllNearFarInteractorsInScene = true;

        [Header("Raycast")]
        [Tooltip("NearFar ışınının UI dünya collider’larına çarpması için maskaya eklenecek katmanlar.")]
        [SerializeField]
        LayerMask extraRaycastLayers = ~0;

        [Header("Event System")]
        [SerializeField]
        bool autoCreateXrUiToolkitManager = true;

        [SerializeField]
        bool disableNonXrInputModulesOnEventSystem = true;

        static bool _warnedNoEventSystem;
        Coroutine _retry;
        readonly HashSet<int> _raycastMaskMergedInstanceIds = new();

        void Awake()
        {
            if (!IsVrLikely())
            {
                enabled = false;
                return;
            }

            _retry = StartCoroutine(BootstrapRoutine());
        }

        void OnDestroy()
        {
            if (_retry != null)
            {
                StopCoroutine(_retry);
                _retry = null;
            }
        }

        IEnumerator BootstrapRoutine()
        {
            for (int i = 0; i < 120 && enabled; i++)
            {
                ApplyOnce();
                if (FindNearFarTargets().Count > 0)
                    break;
                yield return null;
            }

            _retry = null;
        }

        void ApplyOnce()
        {
            if (!IsVrLikely())
                return;

            if (autoCreateXrUiToolkitManager)
                EnsureXrUiToolkitManager();

            EventSystem es = ResolveEventSystem();
            if (es == null)
            {
                if (!_warnedNoEventSystem)
                {
                    _warnedNoEventSystem = true;
                    Debug.LogWarning(
                        $"[{nameof(ExitPanelNearFarUiBootstrap)}] EventSystem yok — XR UI tıklaması çalışmayabilir.",
                        this);
                }

                return;
            }

            EnsurePanelInputConfiguration(es);
            EnsureXrUiInputModule(es);
            if (disableNonXrInputModulesOnEventSystem)
                DisableNonXrInputModules(es);

            IReadOnlyList<NearFarInteractor> list = FindNearFarTargets();
            foreach (NearFarInteractor n in list)
                ConfigureNearFarInteractor(n);
        }

        static bool IsVrLikely()
        {
            if (FirePlatformRuntime.IsSourceInitialized && FirePlatformRuntime.IsVR)
                return true;

#pragma warning disable CS0618
            if (XRSettings.isDeviceActive)
                return true;
#pragma warning restore CS0618

            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays)
            {
                if (d.running)
                    return true;
            }

            return false;
        }

        static void EnsureXrUiToolkitManager()
        {
            if (UnityEngine.Object.FindObjectsByType<XRUIToolkitManager>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
                return;

            new GameObject("XRUIToolkitManager (auto)").AddComponent<XRUIToolkitManager>();
        }

        static EventSystem ResolveEventSystem()
        {
            if (EventSystem.current != null)
                return EventSystem.current;

            return UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        }

        static void EnsurePanelInputConfiguration(EventSystem es)
        {
            if (es == null)
                return;

            PanelInputConfiguration pic = es.GetComponent<PanelInputConfiguration>();
            if (pic == null)
                pic = es.gameObject.AddComponent<PanelInputConfiguration>();

            TrySetPanelInputRedirectionNever(pic);
        }

        static void TrySetPanelInputRedirectionNever(PanelInputConfiguration pic)
        {
            if (pic == null)
                return;

            Type t = pic.GetType();
            foreach (string enumName in new[] { "Never", "NoInputRedirection", "NoInput", "None" })
            {
                if (!TrySetEnumProperty(t, pic, "panelInputRedirection", enumName) &&
                    !TrySetEnumProperty(t, pic, "m_PanelInputRedirection", enumName))
                    continue;

                return;
            }
        }

        static bool TrySetEnumProperty(Type hostType, object instance, string propertyName, string enumValueName)
        {
            PropertyInfo p = hostType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null || !p.CanWrite)
                return false;

            Type enumType = p.PropertyType;
            if (!enumType.IsEnum)
                return false;

            try
            {
                object value = Enum.Parse(enumType, enumValueName, ignoreCase: true);
                p.SetValue(instance, value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void EnsureXrUiInputModule(EventSystem es)
        {
            if (es == null)
                return;

            if (es.GetComponent<XRUIInputModule>() == null)
                es.gameObject.AddComponent<XRUIInputModule>();
        }

        static void DisableNonXrInputModules(EventSystem es)
        {
            if (es == null)
                return;

            foreach (BaseInputModule m in es.GetComponents<BaseInputModule>())
            {
                if (m is XRUIInputModule)
                    continue;
                m.enabled = false;
            }
        }

        IReadOnlyList<NearFarInteractor> FindNearFarTargets()
        {
            if (nearFarInteractorOverride != null)
                return new[] { nearFarInteractorOverride };

            NearFarInteractor[] all = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (all == null || all.Length == 0)
                return Array.Empty<NearFarInteractor>();

            if (!includeAllNearFarInteractorsInScene)
            {
                NearFarInteractor pick = PickPreferred(all, preferInteractorNameContains);
                return pick != null ? new[] { pick } : Array.Empty<NearFarInteractor>();
            }

            return all;
        }

        static NearFarInteractor PickPreferred(NearFarInteractor[] all, string prefer)
        {
            if (all == null || all.Length == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(prefer))
            {
                string tok = prefer.Trim();
                foreach (NearFarInteractor n in all)
                {
                    if (n != null && n.gameObject.name.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0)
                        return n;
                }
            }

            return all[0];
        }

        void ConfigureNearFarInteractor(NearFarInteractor n)
        {
            if (n == null || !n.isActiveAndEnabled)
                return;

            TrySetEnableUiInteraction(n);

            int id = n.GetInstanceID();
            if (_raycastMaskMergedInstanceIds.Add(id))
                TryMergeRaycastMask(n, extraRaycastLayers);
        }

        /// <summary>
        /// XRI sürümüne göre özellik taban sınıfta olmayabilir; property (ve gerekirse field) hiyerarşisinde aranır.
        /// </summary>
        static void TrySetEnableUiInteraction(object interactor)
        {
            if (interactor == null)
                return;

            const string propName = "enableUIInteraction";
            for (Type t = interactor.GetType(); t != null; t = t.BaseType)
            {
                PropertyInfo p = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType == typeof(bool))
                {
                    try
                    {
                        p.SetValue(interactor, true);
                        return;
                    }
                    catch
                    {
                        return;
                    }
                }

                FieldInfo f = t.GetField("m_EnableUIInteraction", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(bool))
                {
                    try
                    {
                        f.SetValue(interactor, true);
                        return;
                    }
                    catch
                    {
                        return;
                    }
                }
            }
        }

        static void TryMergeRaycastMask(NearFarInteractor n, LayerMask extra)
        {
            if (n == null)
                return;

            Type t = typeof(NearFarInteractor);
            foreach (string prop in new[] { "raycastMask", "m_RaycastMask" })
            {
                PropertyInfo p = t.GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p == null || !p.CanRead || !p.CanWrite || p.PropertyType != typeof(LayerMask))
                    continue;

                try
                {
                    var current = (LayerMask)p.GetValue(n);
                    int merged = current.value | extra.value;
                    p.SetValue(n, (LayerMask)merged);
                    return;
                }
                catch
                {
                    // try next
                }
            }

            FieldInfo f = t.GetField("m_RaycastMask", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(LayerMask))
            {
                try
                {
                    var current = (LayerMask)f.GetValue(n);
                    int merged = current.value | extra.value;
                    f.SetValue(n, (LayerMask)merged);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
