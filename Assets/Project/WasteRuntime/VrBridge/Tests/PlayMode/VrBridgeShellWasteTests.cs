using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Woi.Game.Training.VrBridge;
using Woi.OfficeFire.VrBridge;
using Woi.VrBridge.Client.Abstractions;
using Woi.VrBridge.Client.Configuration;
using Woi.VrBridge.Client.Connection;
using Woi.VrBridge.Client.Protocol.Messages;
using Woi.VrBridge.Client.UI;
using Woi.VrBridge.Client.UnityLifecycle;

namespace Woi.WasteCollectionMode.VrBridge.Tests
{
    /// <summary>
    /// Builds the real VrBridgeShellInstaller (no network: connectOnStart off, lifecycle host removed) and checks the
    /// module router, the office-waste self-registration and the prepared-training panel in world space.
    /// </summary>
    public sealed class VrBridgeShellWasteTests
    {
        GameObject _root;
        GameObject _camera;
        GameObject _eventSystem;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            if (_camera != null)
                UnityEngine.Object.Destroy(_camera);
            if (_eventSystem != null)
                UnityEngine.Object.Destroy(_eventSystem);

            WasteBridgeRun.Clear();
        }

        [UnityTest]
        public IEnumerator Shell_RoutesOfficeWaste_AndShowsAWorkingWorldSpaceStartPanel()
        {
            _camera = new GameObject("Main Camera") { tag = "MainCamera" };
            var head = _camera.AddComponent<Camera>();
            _camera.transform.SetPositionAndRotation(new Vector3(1f, 1.6f, -2f), Quaternion.Euler(0f, 30f, 0f));

            _eventSystem = new GameObject("EventSystem");
            _eventSystem.AddComponent<EventSystem>();
            var screenModule = _eventSystem.AddComponent<InputSystemUIInputModule>();

            var config = ScriptableObject.CreateInstance<BridgeClientConfig>();
            config.SupportedModuleIds = new[] { "fire-training", "default", "office-waste" };
            config.DefaultLocale = "en";

            _root = new GameObject("VrBridgeRoot (test)");
            _root.SetActive(false);
            var shell = _root.AddComponent<VrBridgeShellInstaller>();
            SetField(shell, "_config", config);
            SetField(shell, "_connectOnStart", false);
            // Existing behaviour: the bootstrap is added before the shell assigns its config.
            LogAssert.Expect(LogType.Error, "[VRBridge] BridgeClientConfig is not assigned.");
            _root.SetActive(true);
            UnityEngine.Object.DestroyImmediate(_root.GetComponent<VrBridgeLifecycleHost>());
            yield return null;

            // One client, one router: Office Fire keeps fire-training and default, Waste registers office-waste.
            Assert.IsNotNull(shell.ModuleRouter);
            Assert.AreSame(shell.ModuleRouter, VrBridgeRuntime.ModuleRouter);
            Assert.AreSame(shell.Client, VrBridgeRuntime.ClientBehaviour);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<VrBridgeClient>(FindObjectsSortMode.None).Length);
            Assert.IsInstanceOf<OfficeFireBridgeModuleLauncher>(shell.ModuleRouter.Resolve("fire-training"));
            Assert.IsInstanceOf<OfficeFireBridgeModuleLauncher>(shell.ModuleRouter.Resolve("default"));
            Assert.IsNotNull(WasteBridgeInstaller.Launcher, "WasteBridgeInstaller registers itself once the Bridge client exists.");
            Assert.AreSame(WasteBridgeInstaller.Launcher, shell.ModuleRouter.Resolve("office-waste"));

            var pairing = _root.GetComponentInChildren<VrBridgePairingPanel>(true);
            var diagnostics = _root.GetComponentInChildren<VrBridgeDiagnosticsPanel>(true);
            Assert.IsNotNull(pairing, "Pairing panel is built.");
            Assert.IsNotNull(diagnostics, "Diagnostics panel is built.");
            Assert.IsFalse(pairing.gameObject.activeSelf);
            Assert.IsFalse(diagnostics.gameObject.activeSelf);

            // Pairing follows the client state.
            RaiseClientEvent<Action<VrBridgeConnectionState, string>>(shell.Client, "StateChanged", h => h(VrBridgeConnectionState.PairingRequired, "test"));
            Assert.IsTrue(pairing.gameObject.activeSelf, "Pairing panel must be subscribed to StateChanged.");
            RaiseClientEvent<Action<VrBridgeConnectionState, string>>(shell.Client, "StateChanged", h => h(VrBridgeConnectionState.Disconnected, "test"));
            Assert.IsFalse(pairing.gameObject.activeSelf);

            // Diagnostics opens from its toggle and renders the client snapshot.
            var summary = diagnostics.GetComponentsInChildren<Text>(true).First(t => t.name == "Summary");
            _root.GetComponentsInChildren<Button>(true).First(b => b.name == "DiagnosticsToggle").onClick.Invoke();
            yield return null;
            Assert.IsTrue(diagnostics.gameObject.activeSelf);
            Assert.AreNotEqual("Diagnostics", summary.text, "Diagnostics panel must refresh from the bound client.");
            _root.GetComponentsInChildren<Button>(true).First(b => b.name == "DiagnosticsToggle").onClick.Invoke();
            Assert.IsFalse(diagnostics.gameObject.activeSelf);

            var panel = _root.GetComponentInChildren<AuthorizedTrainingPanel>(true);
            Assert.IsNotNull(panel);
            Assert.IsFalse(panel.gameObject.activeSelf, "The panel stays hidden until an authorization is ready.");
            var presenter = panel.GetComponent<VrBridgeAuthorizedPanelPresenter>();
            Assert.IsNotNull(presenter);
            presenter.Initialize(_root.transform, forceWorldSpace: true);

            var context = new BridgeSessionContext(
                "sess-waste-1",
                "quest-under-test",
                "office-waste",
                "msg-1",
                new SessionParticipantPayload { FullName = "Ayşe Yılmaz", PersonnelId = "P-1001" },
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(5),
                DateTimeOffset.UtcNow,
                "connection-1",
                1,
                "auth-waste-1");
            RaiseClientEvent<Action<BridgeSessionContext>>(shell.Client, "AuthorizationReady", handler => handler(context));
            yield return null;

            Assert.IsTrue(panel.gameObject.activeSelf, "AuthorizedTrainingPanel must be subscribed to AuthorizationReady.");
            Assert.IsNull(_root.GetComponent<FireBridgeSessionAdapter>().BoundContext, "The Fire adapter must not bind an office-waste session.");

            var texts = panel.GetComponentsInChildren<Text>(true).Select(t => t.text).ToList();
            Assert.That(texts, Has.Some.EqualTo("Waste Collection"));
            Assert.That(texts, Has.Some.Contains("Ayşe Yılmaz"));
            Assert.That(texts, Has.Some.Contains("P-1001"));

            var canvas = panel.GetComponent<Canvas>();
            Assert.IsTrue(presenter.IsWorldSpace);
            Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);
            Assert.IsTrue(canvas.isRootCanvas, "In VR the panel is its own world-space canvas, off the screen overlay.");
            Assert.AreSame(head, canvas.worldCamera);
            Assert.IsNotNull(panel.GetComponent<TrackedDeviceGraphicRaycaster>());
            Assert.IsNotNull(panel.GetComponent<GraphicRaycaster>());
            Vector3 toPanel = panel.transform.position - head.transform.position;
            Assert.That(toPanel.magnitude, Is.InRange(1.2f, 1.6f));
            Assert.Greater(Vector3.Dot(Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized, Vector3.ProjectOnPlane(toPanel, Vector3.up).normalized), 0.99f);
            Assert.Greater(Vector3.Dot(panel.transform.forward, toPanel.normalized), 0.9f, "A world-space canvas reads correctly when its forward points away from the viewer.");
            Assert.IsNotNull(_eventSystem.GetComponent<XRUIInputModule>());
            Assert.IsFalse(screenModule.enabled, "Only the XR UI module drives the EventSystem in VR.");

            var overlay = _root.GetComponentsInChildren<Canvas>(true).First(c => c.name == "BridgeUiCanvas");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, overlay.renderMode, "Pairing and diagnostics keep their overlay canvas.");

            // The Start button reaches the client (no authorization is held, so the start is refused).
            var status = panel.GetComponentsInChildren<Text>(true).First(t => t.name == "Status");
            panel.GetComponentInChildren<Button>(true).onClick.Invoke();
            yield return null;
            StringAssert.StartsWith("Could not start training", status.text);

            // Gameplay reporting session.started hides the panel.
            RaiseClientEvent<Action<BridgeSessionContext>>(shell.Client, "SessionStarted", handler => handler(context));
            yield return null;
            Assert.IsFalse(panel.gameObject.activeSelf);
        }

        static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        static void RaiseClientEvent<T>(VrBridgeClient client, string eventName, Action<T> invoke) where T : Delegate
        {
            var field = typeof(VrBridgeClient).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, eventName);
            var handler = field.GetValue(client) as T;
            Assert.IsNotNull(handler, eventName + " has no subscribers.");
            invoke(handler);
        }
    }
}
