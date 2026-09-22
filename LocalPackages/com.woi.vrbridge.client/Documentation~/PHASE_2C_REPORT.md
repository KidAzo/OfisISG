# WOI VR Bridge Phase 2C — Quest Client Report

## Existing Unity architecture inspected

| Item | Finding |
|------|---------|
| Project | `C:\Users\azoci\Desktop\UnityProjects\OfisISG` |
| Unity | **6000.3.11f1** |
| Android ID | `com.Woi.OfficeEvacuation` |
| Scripting | IL2CPP, ARM64 |
| OpenXR | 1.16.1 |
| XR Interaction Toolkit | 3.5.0 |
| Bootstrap scene | `Assets/Project/Scenes/FireModule/FireModule_Bootstrapper.unity` |
| Local packages | `LocalPackages/` (`com.woi.module.fire`, `com.woi.modules.sdk`) |
| Legacy networking | `SessionManager` UDP **7777** + HTTP **8080** `Name\|ID` — now gated behind `useLegacyUdpSessionNetworking` (default **false**) |
| WebSocket libs | None previously — Phase 2C uses `System.Net.WebSockets.ClientWebSocket` |
| JSON | Newtonsoft via `com.unity.nuget.newtonsoft-json` |
| Async | UniTask (existing) |
| Secure storage | None previously — Android Keystore plugin added |
| Localization | Fire `LocalizationService` (en/tr); Bridge UI has internal TR/EN catalog |
| Session end | `ExtinguisherSessionRecorder.OnSessionEnded(SessionReport)` |
| Firebase upload | Not present in OfisISG Fire package; Bridge result publisher is independent |

## Package architecture

`LocalPackages/com.woi.vrbridge.client`

- Assemblies: Protocol → Runtime → UI (+ Editor, Tests)
- Fire adapter: `com.woi.module.fire/Runtime/Integrations/VrBridge` (`Woi.Fire.VrBridge`)
- App launcher: `OfficeFireBridgeModuleLauncher` in `Woi.OfficeFire.Integration`

## Public APIs

`IVrBridgeClient`, `IVrBridgeSessionContext` / `BridgeSessionContext`, `IVrBridgeResultPublisher`, `IVrBridgeDiagnostics`, `IDeviceCredentialStore`, `IBridgeDiscoveryClient`, `IBridgeWebSocketTransport`, `IBridgeModuleLauncher`

## WebSocket implementation selected

`ClientWebSocket` (`ClientWebSocketTransport`) — Unity 6 / .NET Standard, serialized send queue, main-thread marshal via `VrBridgeMainThreadDispatcher`. Quest IL2CPP device validation still required.

## Device identity storage

`Application.persistentDataPath/WOI/VRBridge/device.json` — atomic temp→validate→replace. Corruption surfaces `VRB-STOR-004` and does **not** silently regenerate DeviceId.

## Android credential security

`Plugins/Android/WoiCredentialStore.java` — Android Keystore AES/GCM. Editor uses marked `EditorDevCredentialStore`. Tokens never in PlayerPrefs / diagnostics.

## Discovery / pairing / auth / heartbeat / reconnect

1. Last successful gateway → 2. UDP 17778 discovery → 3. Manual host / Editor override  
Pairing UI TR/EN → `device.hello` → token store → heartbeat from server interval → reconnect 1/2/4/8/15s + jitter → lifecycle pause/focus resume.

## Session / module / Fire / outbox

Validated ACK (not gameplay-ready) → `IBridgeModuleLauncher` → Fire adapter notifies `session.started` on recorder start → on end: durable outbox → `session.completed` → `result.submit` / ACK. Duplicate resultId reused. Firebase path untouched.

## Diagnostics UI / localization / Android

Pairing + diagnostics panels (TR/EN). Project already has `INTERNET` + cleartext for LAN `ws://`. Network security template under package `Plugins/Android/res/xml/`. Future: `wss://`.

## Verification

| Check | Status |
|-------|--------|
| Unity script compilation | **Passed** (batchmode return code 0, Unity 6000.3.11f1) |
| EditMode tests | Assembly wiring fixed; re-run in Editor Test Runner (`Woi.VrBridge.Client.Tests.EditMode`) |
| PlayMode tests | Present; run in Editor |
| Android Quest build | Not run in this session |
| IL2CPP link validation | Not run in this session |
| Editor↔Bridge integration | Not run in this session (start Bridge with `Protocol.Adapter=V2`, set Editor gateway override) |
| Physical Quest verification | **Not claimed** |

## Known limitations

- Quest IL2CPP WebSocket + Keystore need headset soak tests
- `ws://` is LAN/dev only; plan `wss://`
- Office Fire launcher maps module id `fire-training` / `default`; wire Addressables launch in app layer as needed
- EditMode CLI initially reported 0 tests until TestAssemblies optional reference was added — verify in Editor

## Physical Quest test steps

See `QUEST_TEST_MATRIX.md` (22 scenarios). Record Bridge + Quest error codes, timestamps, deviceId, sessionId, connection state, sanitized logs on every failure.
