#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using FireExtinguisher.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Woi.Equipment
{
    public static class FireVfxPoseProbe
    {
        const string ScenePath = "Assets/Project/Scenes/FireModule/FireModule_Office.unity";

        public static void RunEditorProbe()
        {
            string sceneFullPath = Path.GetFullPath(ScenePath);
            string hashBefore = HashFile(sceneFullPath);
            File.WriteAllText(FireVfxPoseDiagnostics.LogPath, string.Empty);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ExtinguisherPickupItem item = null;
            VRExtinguisherPinPuller puller = null;
            Transform nozzle = null;
            Transform newFireEx = null;
            Transform vfxOrigin = null;
            Transform mainCloud = null;
            PoseSnapshot nozzlePose = default;
            PoseSnapshot newFirePose = default;
            PoseSnapshot vfxPose = default;
            PoseSnapshot mainPose = default;
            Transform sprayOriginBefore = null;
            ExtinguisherController controller = null;

            try
            {
                item = FindServerRoomExtinguisher();
                if (item == null)
                    throw new InvalidOperationException("Server Room FireExtinguisherE Variant was not in the loaded scene.");

                controller = item.Controller;
                nozzle = FindNamed(item.transform, "NewFireEx");
                newFireEx = nozzle;
                nozzle = newFireEx != null ? newFireEx.parent : null;
                vfxOrigin = newFireEx != null ? FindNamed(newFireEx, "VFXOrigin") : null;
                mainCloud = vfxOrigin != null ? FindNamed(vfxOrigin, "MainCloud") : null;
                nozzlePose = PoseSnapshot.Capture(nozzle);
                newFirePose = PoseSnapshot.Capture(newFireEx);
                vfxPose = PoseSnapshot.Capture(vfxOrigin);
                mainPose = PoseSnapshot.Capture(mainCloud);
                sprayOriginBefore = ReadSprayOrigin(controller);

                puller = FindProbePuller();
                if (puller == null)
                    throw new InvalidOperationException("No VRExtinguisherPinPuller was in the loaded scene.");

                FireVfxPoseDiagnostics.ResetStages();
                FireVfxPoseDiagnostics.TryLogSceneLoaded();

                Invoke(puller, "PullPinOnItem", item);
                Invoke(puller, "RestoreNewFireExAnchorIfMoved");
                Invoke(puller, "ApplyNozzleSnapToThisHand", item);
                Invoke(puller, "RestoreNewFireExAnchorIfMoved");
                Invoke(controller, "BeginDischarge");

                Debug.Log("[FireVfxPoseProbe] completed");
            }
            finally
            {
                nozzlePose.Restore();
                newFirePose.Restore();
                vfxPose.Restore();
                mainPose.Restore();
                WriteSprayOrigin(controller, sprayOriginBefore);

                if (item != null && item.UsageState != null)
                    item.UsageState.ResetUsageState();

                // Reload from disk so the in-memory snap cannot be saved.
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            string hashAfter = HashFile(sceneFullPath);
            string hashLine = "[FireVfxPoseProbe]\nsceneHashUnchanged=" + (hashBefore == hashAfter ? "1" : "0") + "\n";
            Debug.Log(hashLine);
            File.AppendAllText(FireVfxPoseDiagnostics.LogPath, hashLine);

            if (hashBefore != hashAfter)
                throw new InvalidOperationException("FireModule_Office.unity changed on disk during the pose probe.");
        }

        static ExtinguisherPickupItem FindServerRoomExtinguisher()
        {
            ExtinguisherPickupItem[] items = UnityEngine.Object.FindObjectsByType<ExtinguisherPickupItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < items.Length; i++)
            {
                if (FireVfxPoseDiagnostics.IsServerRoomExtinguisher(items[i]))
                    return items[i];
            }

            return null;
        }

        static VRExtinguisherPinPuller FindProbePuller()
        {
            VRExtinguisherPinPuller[] pullers = UnityEngine.Object.FindObjectsByType<VRExtinguisherPinPuller>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            VRExtinguisherPinPuller fallback = null;
            for (int i = 0; i < pullers.Length; i++)
            {
                VRExtinguisherPinPuller puller = pullers[i];
                if (puller == null)
                    continue;

                if (fallback == null)
                    fallback = puller;

                FieldInfo leftHand = typeof(VRExtinguisherPinPuller).GetField("_isLeftHand", BindingFlags.Instance | BindingFlags.NonPublic);
                bool isLeft = leftHand != null && leftHand.GetValue(puller) is bool value && value;
                if (!isLeft)
                    return puller;
            }

            return fallback;
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

        static object Invoke(object target, string methodName, params object[] args)
        {
            if (target == null)
                return null;

            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new MissingMethodException(target.GetType().Name, methodName);

            return method.Invoke(target, args);
        }

        static Transform ReadSprayOrigin(ExtinguisherController controller)
        {
            if (controller == null)
                return null;

            FieldInfo field = typeof(ExtinguisherController).GetField("_sprayWorldOriginTransform", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(controller) as Transform : null;
        }

        static void WriteSprayOrigin(ExtinguisherController controller, Transform origin)
        {
            if (controller == null)
                return;

            FieldInfo field = typeof(ExtinguisherController).GetField("_sprayWorldOriginTransform", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(controller, origin);
        }

        static string HashFile(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            byte[] hash = sha.ComputeHash(stream);
            return System.BitConverter.ToString(hash);
        }

        struct PoseSnapshot
        {
            Transform _transform;
            Transform _parent;
            int _sibling;
            Vector3 _localPosition;
            Quaternion _localRotation;
            Vector3 _localScale;
            bool _valid;

            public static PoseSnapshot Capture(Transform transform)
            {
                if (transform == null)
                    return default;

                return new PoseSnapshot
                {
                    _transform = transform,
                    _parent = transform.parent,
                    _sibling = transform.GetSiblingIndex(),
                    _localPosition = transform.localPosition,
                    _localRotation = transform.localRotation,
                    _localScale = transform.localScale,
                    _valid = true
                };
            }

            public void Restore()
            {
                if (!_valid || _transform == null)
                    return;

                _transform.SetParent(_parent, false);
                _transform.localPosition = _localPosition;
                _transform.localRotation = _localRotation;
                _transform.localScale = _localScale;
                _transform.SetSiblingIndex(_sibling);
            }
        }
    }
}
#endif
