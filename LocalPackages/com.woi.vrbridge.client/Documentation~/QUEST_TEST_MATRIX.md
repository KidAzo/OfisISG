# Quest Test Matrix — WOI VR Bridge Client (Protocol V2)

| # | Scenario | Steps | Expected |
|---|----------|-------|----------|
| 1 | First pairing | Launch app without token; enter valid 6-digit code | `device.accepted`, token stored in Keystore (Android) or dev store (Editor) |
| 2 | Reconnect with token | Relaunch app with stored token | Hello with token only; no pairing UI |
| 3 | Invalid token | Corrupt/delete keystore entry partially | `device.rejected` VRB-AUTH-012; pairing UI shown |
| 4 | UDP discovery | No gateway override; bridge on LAN | `bridge.discovery.offer` with matching nonce and ws:// gateway |
| 5 | Discovery timeout | Stop bridge; launch client | VRB-DISC-004 localized error |
| 6 | Nonce mismatch | Inject fake offer with wrong nonce | VRB-DISC-005; no WS connect |
| 7 | Loopback rejection | Override gateway to 127.0.0.1 | VRB-DISC-006 |
| 8 | Gateway override | Set config override to valid LAN ws URL | Skips UDP; connects directly |
| 9 | Handshake timeout | Connect but never send hello response | Reconnect backoff |
| 10 | Heartbeat cadence | Connected idle | `device.heartbeat` every accepted interval |
| 11 | Busy heartbeat | Accept session | Heartbeat `deviceState=Busy` with activeSessionId |
| 12 | Session accept | Receive `session.start.command` | `session.command.ack accepted=true`; module launcher invoked |
| 13 | Session busy reject | Second session while active | ACK rejected VRB-SESSION-015 |
| 14 | Idempotent repeat | Resend identical command | Duplicate ACK accepted=true |
| 15 | Idempotency conflict | Same sessionId different payload | ACK rejected VRB-IDEMP-016 |
| 16 | Session lifecycle | Complete module flow | `session.started` → result → `session.completed` |
| 17 | Result hash | Submit result | `payloadSha256` matches serialized result body |
| 18 | Result outbox retry | Disconnect before ack | Outbox persists; retries on reconnect |
| 19 | Cancel command | Bridge sends cancel | Module cancel; session cleared |
| 20 | Reconnect backoff | Drop WS | Delays 1,2,4,8,15s + jitter |
| 21 | App pause/resume | Background Quest app | Lifecycle host triggers reconnect |
| 22 | Reset identity | Diagnostics double-confirm reset | New deviceId; token deleted; re-pair required |

## Notes

- Ports: discovery UDP **17778**, gateway **ws://host:17881/ws/device**
- Never use legacy 7777/7778/8080 or Name|ID pairing
- Tokens must not appear in logs, PlayerPrefs, or plaintext JSON on disk (except Editor dev store)
