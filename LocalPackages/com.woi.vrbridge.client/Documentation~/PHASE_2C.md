# Phase 2C — Unity Client Architecture Notes

## Overview

`com.woi.vrbridge.client` is a local Unity package for Quest (Unity 6000.3, IL2CPP Android) that implements the WOI Protocol V2 device role. Gameplay modules integrate through abstractions (`IBridgeModuleLauncher`, `IVrBridgeResultPublisher`) and must not send raw WebSocket messages.

## Layering

```
Runtime/Protocol/     — Newtonsoft DTOs, validation (no UnityEngine)
Runtime/Abstractions/ — Interfaces + session context
Runtime/Connection/   — Discovery, transport, state machine, idempotency, outbox
Runtime/Storage/      — device.json identity + credential stores
Runtime/UI/           — Pairing + diagnostics panels (TR/EN)
Runtime/Threading/    — Main-thread dispatcher for WS callbacks
Editor/               — BridgeClientConfig validation
Tests/                — EditMode + PlayMode with fakes
Plugins/Android/      — Keystore credential plugin + cleartext LAN template
```

## Transport

- `ClientWebSocket` (`System.Net.WebSockets`) with serialized send queue
- Receive loop marshals inbound JSON to main thread before dispatch
- Max message size 64 KB per Protocol V2

## Security

| Surface | Production (Quest) | Editor |
|---------|------------------|--------|
| Device token | Android Keystore via `WoiCredentialStore` | `EditorDevCredentialStore` (encrypted dev file under persistentDataPath, **DEV ONLY**) |
| Device ID | `persistentDataPath/WOI/VRBridge/device.json` atomic write | Same |
| Pairing code | Memory only during pairing; never logged | Same |

## Discovery & Gateway

- UDP broadcast `bridge.discovery.request` → unicast `bridge.discovery.offer`
- Validate nonce, reject loopback gateways
- Default gateway: `ws://{lan-ip}:17881/ws/device`

### ws LAN-only vs wss migration

Current Phase 2C targets **LAN development** with cleartext `ws://`. For production:

1. Bridge exposes `wss://` endpoint (TLS termination on bridge or reverse proxy)
2. Set `BridgeClientConfig.GatewayUrlOverride` or extend discovery offer parsing for `wss://`
3. Remove `network_security_config_woi_bridge.xml` cleartext exceptions from release manifests
4. No client code changes required beyond config if URL scheme is `wss://`

## Reconnect Policy

Backoff seconds: 1, 2, 4, 8, 15 + random jitter (configurable). `VrBridgeLifecycleHost` retriggers connect on focus/resume.

## Session Flow

1. Bridge sends `session.start.command` (envelope `messageId` = command id)
2. `SessionCommandHandler` validates module support, idempotency, busy state
3. Gameplay receives `BridgeSessionContext` via module launcher
4. Gameplay calls `NotifySessionStarted/Completed/Failed` and `SubmitResultAsync`
5. Client emits lifecycle + result messages; outbox retries failed results

## Android Manifest

Host project must include:

```xml
<uses-permission android:name="android.permission.INTERNET" />
```

For LAN `ws://` dev builds, merge:

```xml
android:networkSecurityConfig="@xml/network_security_config_woi_bridge"
```

## Bootstrap

Add `VrBridgeClientBootstrap` to a DontDestroyOnLoad scene object with a validated `BridgeClientConfig` asset. Optionally wire `BridgeModuleLauncherHost` for custom module routing.

## Dependencies

- `com.unity.nuget.newtonsoft-json` 3.2.1
- UniTask from project `Assets/Plugins/UniTask` (asmdef name `UniTask`)
- No Fire Training types
