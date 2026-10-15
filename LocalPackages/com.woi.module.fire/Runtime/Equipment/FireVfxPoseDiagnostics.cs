using System.Globalization;
using System.IO;
using System.Text;
using FireExtinguisher.Core;
using UnityEngine;

namespace Woi.Equipment
{
    /// <summary>
    /// One-shot pose log for the Server Room extinguisher VFX chain.
    /// Does not move transforms.
    /// </summary>
    public static class FireVfxPoseDiagnostics
    {
        public const string LogPath = @"C:\Users\azoci\AppData\Local\Temp\FireVfxPose.txt";

        const string ExtinguisherName = "FireExtinguisherE Variant";

        static bool _sceneLoaded;
        static bool _pinPulled;
        static bool _afterSnap;
        static bool _restoreFirst;
        static bool _restoreAfterSnap;
        static bool _spray;
        static bool _snapLogged;
        static ExtinguisherController _sprayController;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            TryLogSceneLoaded();
        }

        public static void ResetStages()
        {
            _sceneLoaded = false;
            _pinPulled = false;
            _afterSnap = false;
            _restoreFirst = false;
            _restoreAfterSnap = false;
            _spray = false;
            _snapLogged = false;
            UnsubscribeSpray();
        }

        public static bool IsServerRoomExtinguisher(Component component)
        {
            if (component == null)
                return false;

            Transform extinguisher = component.transform;
            if (extinguisher.name != ExtinguisherName)
                return false;

            Transform room = extinguisher.parent;
            if (room == null || room.name != "ServerRoom")
                return false;

            Transform scenariosRoom = room.parent;
            return scenariosRoom != null && scenariosRoom.name == "ServerRoom";
        }

        public static void TryLogSceneLoaded()
        {
            if (_sceneLoaded)
                return;

            ExtinguisherPickupItem item = FindServerRoomExtinguisher();
            if (item == null)
                return;

            _sceneLoaded = true;
            LogChain("scene-loaded", item.transform, restoreWrote: null);
            SubscribeSpray(item);
        }

        public static void LogPinPulled(ExtinguisherPickupItem item)
        {
            if (_pinPulled || !IsServerRoomExtinguisher(item))
                return;

            _pinPulled = true;
            LogChain("pin-pulled", item.transform, restoreWrote: null);
        }

        public static void LogAfterNozzleSnap(ExtinguisherPickupItem item)
        {
            if (_afterSnap || !IsServerRoomExtinguisher(item))
                return;

            _afterSnap = true;
            _snapLogged = true;
            LogChain("after-nozzle-snap", item.transform, restoreWrote: null);
        }

        public static bool ConsumeRestoreFirst(ExtinguisherPickupItem item)
        {
            if (_restoreFirst || !IsServerRoomExtinguisher(item))
                return false;

            _restoreFirst = true;
            return true;
        }

        public static bool ConsumeRestoreAfterSnap(ExtinguisherPickupItem item)
        {
            if (!_snapLogged || _restoreAfterSnap || !IsServerRoomExtinguisher(item))
                return false;

            _restoreAfterSnap = true;
            return true;
        }

        public static void LogRestore(string stage, ExtinguisherPickupItem item, bool wrote)
        {
            if (!IsServerRoomExtinguisher(item))
                return;

            LogChain(stage, item.transform, wrote);
        }

        static void SubscribeSpray(ExtinguisherPickupItem item)
        {
            UnsubscribeSpray();
            ExtinguisherController controller = item.Controller;
            if (controller == null)
                return;

            _sprayController = controller;
            _sprayController.OnSprayStarted += HandleSprayStarted;
        }

        static void UnsubscribeSpray()
        {
            if (_sprayController == null)
                return;

            _sprayController.OnSprayStarted -= HandleSprayStarted;
            _sprayController = null;
        }

        static void HandleSprayStarted()
        {
            if (_spray || _sprayController == null)
                return;

            if (!IsServerRoomExtinguisher(_sprayController))
                return;

            _spray = true;
            LogChain("spray-start", _sprayController.transform, restoreWrote: null);
        }

        static ExtinguisherPickupItem FindServerRoomExtinguisher()
        {
            ExtinguisherPickupItem[] items = Object.FindObjectsByType<ExtinguisherPickupItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < items.Length; i++)
            {
                if (IsServerRoomExtinguisher(items[i]))
                    return items[i];
            }

            return null;
        }

        static void LogChain(string stage, Transform extinguisher, bool? restoreWrote)
        {
            Transform newFireEx = FindNamed(extinguisher, "NewFireEx");
            Transform nozzle = newFireEx != null ? newFireEx.parent : FindNamed(extinguisher, "Nozzle_low");
            Transform vfxOrigin = newFireEx != null ? FindNamed(newFireEx, "VFXOrigin") : null;
            Transform mainCloud = vfxOrigin != null ? FindNamed(vfxOrigin, "MainCloud") : null;

            var builder = new StringBuilder(2048);
            AppendPose(builder, stage, nozzle);
            AppendPose(builder, stage, newFireEx);
            AppendPose(builder, stage, vfxOrigin);
            AppendPose(builder, stage, mainCloud);
            AppendDistances(builder, stage, nozzle, newFireEx, vfxOrigin, mainCloud, restoreWrote);

            string text = builder.ToString();
            Debug.Log(text);
            File.AppendAllText(LogPath, text);
        }

        static void AppendPose(StringBuilder builder, string stage, Transform transform)
        {
            builder.Append("[FireVfxPose]\n");
            builder.Append("stage=").Append(stage).Append('\n');
            if (transform == null)
            {
                builder.Append("name=missing\nparent=\nlocalPosition=\nlocalRotation=\nworldPosition=\nworldRotation=\n");
                return;
            }

            builder.Append("name=").Append(transform.name).Append('\n');
            builder.Append("parent=").Append(transform.parent != null ? transform.parent.name : "null").Append('\n');
            builder.Append("localPosition=").Append(FormatVector(transform.localPosition)).Append('\n');
            builder.Append("localRotation=").Append(FormatQuaternion(transform.localRotation)).Append('\n');
            builder.Append("worldPosition=").Append(FormatVector(transform.position)).Append('\n');
            builder.Append("worldRotation=").Append(FormatQuaternion(transform.rotation)).Append('\n');
        }

        static void AppendDistances(
            StringBuilder builder,
            string stage,
            Transform nozzle,
            Transform newFireEx,
            Transform vfxOrigin,
            Transform mainCloud,
            bool? restoreWrote)
        {
            builder.Append("[FireVfxDistances]\n");
            builder.Append("stage=").Append(stage).Append('\n');
            if (restoreWrote.HasValue)
                builder.Append("restoreWrote=").Append(restoreWrote.Value ? "1" : "0").Append('\n');

            builder.Append("nozzleToNewFireEx=").Append(Distance(nozzle, newFireEx)).Append('\n');
            builder.Append("nozzleToVFXOrigin=").Append(Distance(nozzle, vfxOrigin)).Append('\n');
            builder.Append("nozzleToMainCloud=").Append(Distance(nozzle, mainCloud)).Append('\n');
            builder.Append("newFireExToVFXOrigin=").Append(Distance(newFireEx, vfxOrigin)).Append('\n');
        }

        static string Distance(Transform a, Transform b)
        {
            if (a == null || b == null)
                return "missing";

            return Vector3.Distance(a.position, b.position).ToString("F6", CultureInfo.InvariantCulture);
        }

        static Transform FindNamed(Transform root, string name)
        {
            if (root == null)
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == name)
                    return transforms[i];
            }

            return null;
        }

        static string FormatVector(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:F4}, {1:F4}, {2:F4})",
                value.x,
                value.y,
                value.z);
        }

        static string FormatQuaternion(Quaternion value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:F6}, {1:F6}, {2:F6}, {3:F6})",
                value.x,
                value.y,
                value.z,
                value.w);
        }
    }
}
