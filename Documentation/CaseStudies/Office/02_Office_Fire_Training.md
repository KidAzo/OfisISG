# Office Fire Training

## 1. One-Line Summary

A fire-response simulation in which a trainee chooses an office scenario, handles a developing fire, and evacuates to an assembly area, then receives a pass or improvement report based on the actions they took.

---

## 2. Overview

Office Fire Training is the emergency-response module of the Office VR Training project. The login screen titles it “FIRE SAFETY / TRAINING SIMULATOR” in English and “YANGIN GÜVENLİĞİ / EĞİTİM SİMÜLATÖRÜ” in Turkish. The trainee enters a full name and a user id, chooses a language, and chooses one of three scenarios: Server Room, Archive Room, or Kitchen-Cafe.

The selected scenario is the only one activated. The trainee is placed at that scenario’s spawn, hears guidance, and works through a state machine: notice smoke, enter the room, intervene, leave, and reach an outdoor assembly area. Along the way they may use an extinguisher, a fire blanket, or a suppression control, depending on the room. Using water on the fire is recorded as a mistake. Smoke, stair rails, and the elevator are part of the evacuation.

The result is a training report, not a letter grade. It shows whether the fire was brought under control, whether evacuation was completed, reaction time, completed objectives, missing objectives, and mistakes. The session passes only when nothing required is missing and no mistake was recorded.

---

## 3. The Training Challenge

The module is built as a sequence of decisions under a developing incident, not as a hazard checklist. Each scenario starts with a short delay, then asks the trainee to notice smoke. If they do not, reminders begin after 30 seconds and repeat every 15 seconds. Fire can grow. The trainee has to choose a response that matches the room, leave before the situation escalates, and finish at the assembly area.

The three rooms teach different responses that are actually implemented:

- Archive: inspect the room, raise the alarm, use an extinguisher, leave, and evacuate. Water on this fire is recorded as using water on an electrical fire.
- Server room: inspect, enter, activate the suppression control, leave before the next stage, close the door if that end state is captured, and evacuate. Water on this fire is recorded as using water on a server fire.
- Kitchen-cafe: inspect, enter, use a fire blanket or the suppression control, do not use water, leave, and evacuate.

All three also score crouching in smoke, holding the stair handrail, and reaching the assembly area.

---

## 4. Our Solution

The trainee picks the incident before they enter. The office then runs only that scenario’s content. Voice lines tell them the current step: notice smoke, press the alarm, lean in smoke, use the extinguisher, leave the room, do not use the elevator. A controls HUD on desktop lists movement, equip, pin pull, drop, and crouch.

Intervention is physical. On desktop the trainee equips with E, pulls the extinguisher pin with R, and drops with G. In VR they grab the extinguisher into the hand, pull the pin, and discharge it. A fire blanket and a carafe are separate pickup tools. Water use is not a silent failure: the scenario records the mistake and plays an announcement.

Evacuation continues outside the fire room. Stair scenes load with the office. Holding the handrail is timed. Reaching the assembly door loads an outdoor scene. NPCs can be sent along evacuation paths when the scenario starts that phase. When the scenario completes, the report shows pass or “needs improvement,” and the session can be written to a local CSV. From a Hub launch, the trainee can return to the Hub instead of quitting the application.

---

## 5. Training Flow

1. Hub entry is `Office_Boot`. `OfficeGameModulesBootstrapper`, set to Fire Training, waits for the shared service installer and loads the scene group `OfficeFireModule_Login`.
2. The trainee completes the login form: full name, user id, scenario, and Turkish or English. Start is rejected until both name and user id are filled. On VR, the form is prefilled so it can be started. The desktop login shows a background image; VR hides that background. A leaderboard panel on the login screen reads the device’s stored training scores.
3. Start stores the profile and scenario, then loads the scene group `FireModule_Office`. That group loads the office scene and additive stair scenes.
4. If a login session is set, the scenario bootstrapper starts the chosen scenario, teleports the player to that scenario’s spawn, enables that scenario root, and disables the others. Localized sign materials are applied. Input and the VR extinguisher rig are synced after the load.
5. After a default 3-second intro, the scenario asks the trainee to notice smoke. Noticing smoke records the reaction time and advances the state.
6. The trainee opens or enters the room and intervenes:
   - Archive: alarm, extinguisher, then leave.
   - Server: suppression control, then leave; water is a mistake.
   - Kitchen: fire blanket and/or suppression control; water is a mistake.
7. If the fire-growth step happens and the trainee has not crouched, “stood in smoke” is recorded. Crouch input records a correct lean.
8. The trainee leaves toward the assembly area. On the stairs, entering the rail volume starts as “did not hold the handrail” until they hold a railing-tagged collider for the required time, 3 seconds by default. Approaching the elevator plays a do-not-use-the-elevator announcement.
9. Reaching the assembly door loads the `OutDoor` scene group. Reaching the assembly area completes the evacuation objective. Evacuation NPCs can walk spline paths during this phase and are stopped when that state exits.
10. The result screen shows the scenario title, pass or needs improvement, reaction time, fire-controlled status, evacuation status, and the three lists. Continue returns to the fire login. Quit closes a standalone player. If a Hub exit handler is registered, quit returns to the Hub instead. The report is appended to a CSV on the machine running the session.

---

## 6. Core Gameplay / Training Mechanics

### Scenario selection

One office contains three scenario controllers. The bootstrapper activates the matching scenario root and tells the others they are deselected. The trainee is moved to the spawn registered for that scenario id. Desktop and VR choose different movement roots so the unused rig is not the one that gets teleported.

### Smoke notice and reaction time

Each scenario waits, then enters a smoke-notice state and plays an announcement. The first successful “notice smoke” action stores reaction time as seconds since the scenario started. If the trainee waits, a reminder voice line plays after 30 seconds and then every 15 seconds.

### Archive room

Required report objectives:

- Inspect the archive room (smoke noticed)
- Press the alarm
- Crouch in smoke
- Use the extinguisher (completed in the report when the extinguisher is grabbed)
- Exit the archive room
- Hold the handrail on the stairs
- Go to the assembly area

During intervention, water is recorded as “Used water on electrical fire” and an announcement plays. The state order implemented is smoke notice, door, intervention, extinguisher use, leave the room, assembly, completed.

### Server room

Required report objectives:

- Inspect the server room
- Press the alarm
- Crouch in smoke
- Enter the server room
- Activate the suppression system
- Leave the server room before gas release
- Close the server room door, when a door end-state was captured
- Hold the handrail
- Go to the assembly area

Activating suppression records that action, allows extinguisher spray, and can start evacuation NPCs. Water is recorded as “Used water on server fire.” If the scenario ends without a captured door state, the door objective is omitted from the missing list rather than failed.

### Kitchen-cafe

Required report objectives:

- Check the kitchen area
- Press the alarm
- Crouch in smoke
- Enter the kitchen area
- Fire blanket usage (selecting or correctly placing the blanket)
- Water usage (this objective is completed when water was not used)
- Activate suppression, satisfied either by the blanket path or by activating the suppression system
- Leave the kitchen before gas release
- Hold the handrail
- Go to the assembly area

Using water records “Used water on kitchen fire.” Using the blanket plays a success line and is handled as the blanket-on-fire action. Pickup scripts exist for the fire blanket and the carafe.

### Extinguisher

The shared fire equipment system supports equipping, pulling the pin, dropping, and discharging, with spray presentation and an aim evaluator. Desktop bindings shown in the HUD are E to equip, R to pull the pin, and G to drop. VR uses a hand grabber that detects an extinguisher within a radius and holds it on the controller, plus a separate pin-pull interaction. The scenario can block spray until the alarm or suppression step allows it.

Mistake labels exist for wrong extinguisher distance and angle. This pass did not find those mistake ids being registered by the office scenario controllers or the extinguisher package. Treat distance and angle scoring as **Needs confirmation**.

### Smoke, stairs, and elevator

Crouch is Left Ctrl on desktop, bridged into the active scenario as a lean action. If a fire-growth step arrives first, the report records standing in smoke. On the stairs, the handrail volume assumes the hold was missed until the trainee keeps contact with a railing-tagged collider for the configured duration. Elevator proximity plays a “do not use the elevator” line. This pass did not find that proximity writing the “used elevator” mistake into the report.

### Evacuation cast

`EvacuationNpcDirector` starts spline-following NPCs when the scenario calls for evacuation and can stop and reset them afterward. Fire intensity can drive smoke scale. These systems make the exit feel occupied and the fire visible while the trainee is still deciding.

### Pass rule

The report passes only when the missing-objective list is empty and the mistake list is empty. Otherwise the status is “NEEDS IMPROVEMENT” / “İYİLEŞTİRME GEREKİYOR”. Handrail and smoke failures are shown as mistakes, not also as missing objectives. Kitchen water failure is handled the same way.

---

## 7. VR Interaction Design

Office Fire Training uses the same PC/XR split as the rest of the office project.

**Desktop.** WASD movement, mouse look, and sprint come from the shared player controller. The controls HUD is written for keyboard training: move, equip, pin, drop, crouch. Menu and result screens unlock the cursor.

**VR.** The XR Interaction Toolkit rig is the VR player. Extinguishers are grabbed into the hand rather than selected from a menu. Pin pull is a separate VR interaction on the extinguisher. The login and result UI can be presented as world-space UI. After the office loads, the module runs a VR extinguisher-rig bootstrap and re-syncs VR gameplay input, including grab, because Addressables can duplicate the input assets.

**Platform choice.** `FirePlatformRuntime` reads the shared PC/XR porting asset. On Android, an active XR device is treated as VR even if that asset still says PC. The project’s OpenXR Android settings enable Meta Quest Support for Quest, Quest 2, Quest Pro, Quest 3, and Quest 3S.

**Scene composition.** `FireModule_Office` is the gameplay scene. The scene group also loads stair scenes additively. The assembly ending loads the `OutDoor` group, with a fade to black of 0.45 seconds by default before that load. A black fade during VR scene loads is part of the shared scene loader when XR loading-screen settings are present.

---

## 8. Feedback & Guidance

**What to do.** The current objective is set on the scenario controller as the state changes. Voice lines announce the step: incident detected, press the alarm, lean, extinguisher handled, extinguishing started, leave the room, evacuation instruction, do not use the elevator, and scenario-specific lines such as kitchen blanket success. If the trainee does not notice smoke, reminders repeat. Assembly reminders can loop until the exit trigger fires.

**Whether it was valid.** Correct actions are appended to the scenario report. Mistakes are appended and can play a dedicated announcement, including the water-mistake lines. Suppression and alarm steps explicitly allow extinguisher spray, so spray before that allowance is not the successful path.

**What is next.** State transitions move the objective from inspect, to intervene, to leave, to assembly. The desktop HUD stays available as a control reminder until the result screen hides it.

**Language.** Login, objective labels, mistake labels, voice-line selection, and sign materials follow Turkish or English. The login default is Turkish.

**Completion.** The result screen is titled “Training Result” / “Eğitim Sonucu,” with the scenario name as the subtitle. It shows reaction time in seconds, whether the fire was brought under control, whether evacuation was completed, and scrollable lists for completed objectives, missing objectives, and mistakes. Empty lists have their own sentences. Buttons are Continue and Quit Game. A Hub return label exists in the result model (“Return to Hub” / “Hub'a Dön”). In the controller found here, the quit button returns to the Hub when an exit handler is registered, and otherwise quits the player.

---

## 9. Results & Progress Tracking

### Public-facing version

Each scenario ends in a training result the trainee can read immediately: pass or needs improvement, reaction time, whether the fire was controlled, whether evacuation was completed, and lists of completed steps, missing steps, and mistakes. The same report can be saved as a local spreadsheet file for the session. The fire login screen can show a device-local score list.

This pass did not find the office-fire result screen itself uploading that report to a server. Treat admin-dashboard storage as **Needs confirmation**.

### Technical notes

`OfficeFireScenarioController` keeps an `OfficeFireScenarioReport`: scenario id, correct actions, mistakes, reaction time, `fireControlled`, `evacuated`, and completion. Reaction time is the first recorded reaction after `StartScenario`.

`OfficeFireScenarioResultCatalog` turns that report into completed and missing objectives using the rules in section 6. `OfficeFireResultScreenMapper` localizes every label and sets `Passed` when both missing objectives and mistakes are empty.

`OfficeFireResultScreenController` builds that model, exports CSV once, and presents it. `OfficeFireSessionResultCsvExporter` writes `ofis_yangin_oyuncu_sonuclari.csv` under a desktop folder named `OfisYanginOyuncuSonuclari`, or under the persistent data path if the desktop path is unavailable. Columns include timestamp, name, user id, scenario, reaction time, fire status, evacuation status, overall status, and the three lists.

`TrainingLeaderboardStore` keeps up to 10 scores in PlayerPrefs under a fixed key, replacing the previous score for the same user id. The fire login screen displays those lines. `TrainingResultScreenSessionBinder` can record `FinalScore * 100` from a different session-report type. `OfficeFireResultScreenController` exports CSV and does not call `TryRecordScore`. Whether a completed office-fire scenario updates the login leaderboard needs confirmation.

Identity on the CSV comes from `OfficeFireLoginSession` (name, user id, language code, scenario id). VR login writes a test profile into the fields before start. Do not publish that profile.

Hub exit is `IModuleExitHandler.RequestModuleExit` from the quit button when the handler exists. Continue reloads `OfficeFireModule_Login`.

---

## 10. Technical Implementation

**Engine**

- Unity `6000.3.11f1`
- Universal Render Pipeline

**XR**

- OpenXR `1.16.1` with Meta Quest Support enabled for Quest, Quest 2, Quest Pro, Quest 3, and Quest 3S
- XR Interaction Toolkit `3.5.0`
- Unity Input System `1.19.0`
- VR extinguisher grab and pin-pull components in the shared fire module

**Platform**

- Desktop and VR presentations of the same scenarios
- Android build target, with an active XR device forcing VR mode
- Additive stair scenes and a separate outdoor assembly scene

**Architecture**

- Addressables group `Office_Safety`, label `module-office-fire-full`
- Addresses include `Office_Boot`, `OfficeFireModule_Login`, `FireModule_Office`, and stair scenes
- `SceneLoader` group `FireModule_Office` loads the office plus `FireStairsOfis` and `FireStairsOfis 1`
- Group `OutDoor` is loaded from the assembly-door step
- `OfficeGameModulesBootstrapper` implements `IModuleBootstrap` and loads the fire login after `FireServiceInstaller` registers the scene loader
- Per-scenario state machines: `ArchiveRoomScenarioController`, `ServerRoomScenarioController`, `KitchenCafeScenarioController`
- Shared scenario base class for objectives, mistakes, reaction time, and completion
- Content and voice ids are enums and scenario reports, not a single hardcoded script per line of dialogue

**Backend / platform integration**

- Hub launch and Hub return through the module SDK
- Login session held in `OfficeFireLoginSession` for the office load
- Local CSV export of the full result
- Device PlayerPrefs leaderboard displayed on the fire login
- No office-fire HTTP result post was found in the result controller

**Other**

- UI Toolkit login, controls HUD, and result screen
- Localization service plus runtime language code
- Spline-based evacuation NPCs
- PrimeTween is in the project; fire scenario motion in this pass is primarily state, VFX, and scene loading
- Smoke scale driven from fire intensity
- Localized sign materials applied when a scenario starts

---

## Performance & Optimization

No runtime measurement was found for this module. Needs benchmark.

The values below are shared project configuration. They are not a frame-rate result, and no Fire Training render override was found. Smoke, additive stair scenes, and the outdoor load are content facts, not timing data.

**Verified configuration**

- Standalone builds default to the `PC` quality level. Android builds default to the `Mobile` quality level. `ProjectSettings/QualitySettings.asset`
- `PC_RPAsset`: render scale `1`, URP MSAA disabled (`m_MSAA: 1`), HDR on, SRP Batcher on, main-light shadow map `2048`, additional-light shadows on, soft shadows on, `4` shadow cascades, URP shadow distance `50`. `Assets/Settings/PC_RPAsset.asset`
- `Mobile_RPAsset`: render scale `1`, URP MSAA disabled (`m_MSAA: 1`), HDR on, SRP Batcher on, main-light shadow map `1024`, additional-light shadows off, soft shadows off, `1` shadow cascade, URP shadow distance `50`. `Assets/Settings/Mobile_RPAsset.asset`
- Quality `vSyncCount` is `0` on both levels. That turns vsync off in the quality setting. It is not a measured frame rate.
- OpenXR Foveated Rendering is present and disabled on Standalone and Android. XR Performance Settings is disabled. Application SpaceWarp is disabled on Android. `Assets/XR Asset/XR/Settings/OpenXR Package Settings.asset`
- No `Application.targetFrameRate` assignment was found under `Assets/Project` or `LocalPackages`.

**Not found**

Configured refresh rate, measured FPS, CPU or GPU frame time, OVR Metrics, Unity Profiler captures, triangle or vertex counts, batches, SetPass or draw calls, a scene light count, occlusion data, LOD group counts, memory, GC, this module’s build size, and scene load time. `ProfilerCaptures` is empty. Android APKs under `Builds/Android` are named for VR Bridge, not for Fire Training.

Do not publish a frame rate or a Quest performance number from this section.

---

## 11. Interesting Engineering Problems

### Problem

Three different fires have to live in one office without all of them running at once.

### Solution

Each scenario is a controller with its own root, state machine, and spawn point. `OfficeFireScenarioBootstrapper` activates only the selected root, deselects the others, teleports the platform-correct player root, and registers the active controller so later systems (lean, extinguisher HUD, announcements) talk to that scenario only.

### Why It Matters

The trainee chooses one incident, and the building does not play the kitchen, archive, and server emergencies together.

### Problem

The “right” response is different in each room, but the report has to stay comparable.

### Solution

A shared report stores correct-action ids and mistake ids. `OfficeFireScenarioResultCatalog` maps those ids onto a checklist per scenario. Pass is structural: no missing required objective and no recorded mistake. Labels are translated in one mapper, so the result screen does not embed a second copy of the rules.

### Why It Matters

Archive, server, and kitchen can teach different techniques and still produce the same kind of training record.

### Problem

Evacuation does not fit in the fire-room scene.

### Solution

The office group already loads stair scenes additively. The handrail is a trigger volume with its own hold timer. The assembly door requests the `OutDoor` scene group after a fade. NPC evacuation is a director over spline followers that can start and stop with the scenario state.

### Why It Matters

The trainee practices leaving the building, not only fighting the fire in the room where it started.

### Problem

A dangerous action should be explained when it happens, not only listed at the end.

### Solution

Water use, standing in smoke, and elevator proximity call the scenario announcement system at the moment of the action. Water and standing in smoke are also written into the report. Smoke-notice and assembly steps repeat voice lines on a timer until the expected action arrives.

### Why It Matters

The trainee can correct course during the incident, and the report still shows what was missed.

### Problem

VR extinguisher control and desktop key control have to drive the same scenario actions.

### Solution

Scenario states do not read the keyboard directly. They accept action ids such as grab extinguisher, use extinguisher, use blanket, use water, press alarm, and player leaned. Desktop input and VR grab/pin components raise those actions. After an Addressable load, `OfficeFireInputSync` rebinds the live input assets and wires the VR extinguisher rig.

### Why It Matters

The scenario logic stays one state machine while the Hub can launch desktop or headset play.

### Problem

Returning from the result must not quit the Hub player.

### Solution

The quit button looks for `IModuleExitHandler`. If the module was launched from the Hub, it requests module exit and keeps the cursor in a menu state so the gameplay lock is not restored while the Hub scene loads. A standalone build quits the application. Continue loads the fire login group again.

### Why It Matters

The same result screen works for a kiosk-style build and for a module inside the Hub.

### Problem

Stair and smoke checks can be failed by doing nothing, which is easy to double-count.

### Solution

The handrail volume registers “did not hold” on entry, then clears it if the hold succeeds. The smoke check registers “stood in smoke” only if the growth step happens without a lean. The result catalog then suppresses those items from the missing-objective list when the mistake is already present.

### Why It Matters

The report shows one clear failure for the rail or the smoke, not two lines for the same lapse.

---

## 12. Reusable Systems

**SHARED SYSTEM**

- `Office_Boot`, `FireServiceInstaller`, `IModuleBootstrap`, `IModuleExitHandler`
- `SceneLoader`, Addressables, and catalog health checks
- `FirePlatformRuntime` and PC/XR porting
- `PlayerController`, XR Origin, and `OfficeFirePlayerInitializer`
- `InputManager` and `OfficeFireInputSync`
- Extinguisher equipment, pin pull, spray, and aim evaluation from the shared fire module
- UI Toolkit, localization service, URP, OpenXR, Input System
- Device `TrainingLeaderboardStore` used by the fire login

**MODULE-SPECIFIC SYSTEM**

- Fire login form with scenario dropdown and `OfficeFireLoginSession`
- Archive, server, and kitchen state machines and their objective catalogs
- Fire blanket and carafe pickup for the kitchen and server flows
- Evacuation NPC director, stair handrail volume, smoke-lean bridge, and outdoor assembly load
- Office Fire result screen, pass rule, and `ofis_yangin_oyuncu_sonuclari.csv`
- Scenario voice-line ids and localized office sign materials
- Addressables label `module-office-fire-full`

---

## 13. Visuals We Should Capture

**Shot**
Login screen titled Fire Safety Training Simulator, with the scenario dropdown open on Server Room, Archive Room, and Kitchen-Cafe.

**Purpose**
Hero context. Shows that this is a choice of emergency, in two languages.

**Shot**
Trainee at a doorway with visible smoke in the archive or server room, before they enter.

**Purpose**
Establishes the “notice and decide” opening.

**Shot**
Close view of an extinguisher in the hand or equipped, pin pulled, spray aimed at the fire.

**Purpose**
Shows the hands-on intervention, especially in VR.

**Shot**
Fire blanket covering a pan in the kitchen, or the trainee at the suppression control in the server room.

**Purpose**
Shows that the response changes with the type of fire.

**Shot**
A wrong tool moment only if it can be captured cleanly: carafe or water used on the fire, with the mistake feedback visible.

**Purpose**
Shows that unsafe technique is recognized during the drill.

**Shot**
Stairs during evacuation, trainee’s hand or controller at the handrail, smoke still in the background.

**Purpose**
Shows the evacuation skill, not only firefighting.

**Shot**
NPCs moving along the evacuation route toward the exit.

**Purpose**
Shows the office reacting around the trainee.

**Shot**
Outdoor assembly area after the fade, trainee arrived.

**Purpose**
Closes the story of leaving the building.

**Shot**
Training Result screen with a pass or needs-improvement badge, reaction time, and the mistake list visible. Use a test profile, not a real employee.

**Purpose**
Shows the record the session produces.

---

## 14. Website Highlight Points

- Three office fire scenarios in one building
- Extinguisher, fire blanket, and suppression-system responses
- Evacuation through stairs to an outdoor assembly area
- Spoken guidance and repeated prompts during the incident
- Pass or improvement report with mistakes listed
- Desktop and VR from one module

---

## 15. Website Case Study Draft

# Office Fire Training

### Hero Description

Office Fire Training is a scenario-based fire drill inside a working office. The trainee chooses a server room, an archive, or a kitchen, responds to that fire, and evacuates to an assembly area. The session ends with a clear result: what they did, what they skipped, and whether the response passed.

### The Challenge

A fire procedure is a sequence. Notice the smoke. Raise the alarm. Use the right method for that room. Stay low. Leave. Use the stairs, not the elevator. Gather outside. A slide can list those steps. It cannot show what happens when water is used on the wrong fire, or when the trainee stays upright in smoke.

This module runs the incident. The room, the tools, and the exit are already there. The trainee has to act in order, and the simulation records both the correct steps and the unsafe ones.

### The Experience

After a short login, the trainee is placed at the chosen scenario. Smoke is the cue. Voice guidance tells them what the current step is, and repeats if they wait. In the archive they take an extinguisher. In the server room they activate suppression and get out. In the kitchen they use a fire blanket or the suppression control, and water on that fire is marked as a mistake.

Then the drill becomes an evacuation. The trainee moves through the stairs, holds the handrail, avoids the elevator, and reaches an outdoor assembly area while other people in the office follow evacuation paths. Desktop play uses the keyboard and mouse. VR play uses the headset and controllers, including picking up and discharging the extinguisher by hand.

### How It Works

The trainee selects Server Room, Archive Room, or Kitchen-Cafe, then enters the office. Only that scenario runs. Each action — alarm, extinguisher, blanket, water, crouch, exit — is checked against that room’s rules. At the end, the training result shows reaction time, whether the fire was controlled, whether evacuation was completed, and three lists: completed objectives, missing objectives, and mistakes. The session passes when the required steps are done and no mistake was recorded. A Hub session can return to the Hub from the result screen.

### Key Features

- Three fire scenarios: archive, server room, and kitchen-cafe
- Extinguisher handling on desktop and in VR, plus fire blanket and suppression controls
- Timed smoke response, crouch-in-smoke, and stair handrail checks
- Evacuation to an outdoor assembly area with NPC evacuees
- Bilingual guidance and a bilingual result screen
- Local session report export and Hub return

### Technology

Built in Unity with the Universal Render Pipeline, OpenXR, and the XR Interaction Toolkit. The module is delivered as Addressable content: a boot scene, a login scene, the office, stair scenes, and an outdoor assembly scene. Scenario logic is a state machine per room, sharing one report format. Meta Quest device targets are enabled in the project’s OpenXR Android settings.

### Integration

Office Fire Training launches from the WoiLabs Hub as its own module and can hand control back to the Hub when the trainee leaves the result. Sign-in is the fire login: name, user id, language, and scenario. Those values travel into the office session and into the saved report.

The result the trainee sees is produced in the session. A copy can be written to a local file on the computer running the training. The login screen can list device-local scores.

### Outcome

The module lets people practice a full office fire response — the right tool for the room, then a full evacuation — before they have to do it for real. Every session leaves a record of the steps they completed and the mistakes the simulation caught.

---

# Evidence Appendix

Internal only. Not for the public website.

| Claim | Evidence | File / Scene |
|---|---|---|
| Login title is Fire Safety Training Simulator | `ApplyLanguage` | `Assets/Project/OfficeFire/Runtime/Integration/OfficeFireLoginScreenController.cs` |
| Three scenarios: Server, Archive, Kitchen-Cafe | Dropdown labels and `OfficeFireScenarioId` | `OfficeFireLoginScreenController.cs`, `Assets/Project/OfficeFire/Runtime/Core/Enums/OfficeFireScenarioId.cs` |
| Name and user id are required | `OnStartClicked` | `OfficeFireLoginScreenController.cs` |
| Turkish is the default language | `EnsureDefaultLanguageSelection` | `OfficeFireLoginScreenController.cs` |
| Login stores the session and loads `FireModule_Office` | `OfficeFireLoginSession.Set`, `LoadScene` | `OfficeFireLoginScreenController.cs` |
| Hub boot loads fire login | `OfficeGameModulesBootstrapper` FireTraining → `OfficeFireModule_Login` | `Assets/Project/OfficeFire/Runtime/Integration/OfficeGameModulesBootstrapper.cs` |
| Addressable label `module-office-fire-full` | Group `Office_Safety` | `Assets/AddressableAssetsData/AssetGroups/Office_Safety.asset` |
| Office group includes stairs | SceneLoader group `FireModule_Office` | `Assets/Project/Reflex/Resources_moved/SceneLoader.prefab` |
| Only the selected scenario root is active | `StartScenario` | `Assets/Project/OfficeFire/Runtime/Scenario/Shared/Bootstrap/OfficeFireScenarioBootstrapper.cs` |
| Player spawn depends on PC vs VR root | `OfficeFirePlayerInitializer.ResolveMovementRoot` | `Assets/Project/OfficeFire/Runtime/Core/Player/OfficeFirePlayerInitializer.cs` |
| Smoke notice delay default 3s; reminders 30s then 15s | Serialized defaults on each scenario controller | `ArchiveRoomScenarioController.cs`, `ServerRoomScenarioController.cs`, `KitchenCafeScenarioController.cs` |
| Archive required objectives | `ArchiveRules` | `Assets/Project/OfficeFire/Runtime/Integration/UI/OfficeFireScenarioResultCatalog.cs` |
| Server required objectives, including door exception | `ServerRules`, `ServerRoomDoorClosed` | `OfficeFireScenarioResultCatalog.cs` |
| Kitchen required objectives, including blanket and water | `KitchenRules` | `OfficeFireScenarioResultCatalog.cs` |
| Pass requires no missing objectives and no mistakes | `OfficeFireResultScreenMapper.FromReport` | `Assets/Project/OfficeFire/Runtime/Integration/UI/OfficeFireResultScreenMapper.cs` |
| Water mistakes and announcements | State `UseWater` cases | Archive, server, and kitchen scenario controllers |
| Extinguisher grab completes the archive “use extinguisher” objective | `GrabbedExtinguisher` in catalog; registration in archive controller | `OfficeFireScenarioResultCatalog.cs`, `ArchiveRoomScenarioController.cs` |
| Spray can be withheld until alarm/suppression | `AllowExtinguisherSpray` calls in state enter/handlers | Scenario controllers |
| Desktop control labels | `ControlsHUDController` | `LocalPackages/com.woi.module.fire/Runtime/Training/UI/ControlsHUDController.cs` |
| VR grab radius and pin pull exist | `VRHandExtinguisherGrabber`, `VRExtinguisherPinPuller` | `LocalPackages/com.woi.module.fire/Runtime/Equipment/` |
| Crouch/lean dispatches `player_leaned` | `OfficeFirePlayerLeanScenarioBridge` | `Assets/Project/OfficeFire/Runtime/Integration/OfficeFirePlayerLeanScenarioBridge.cs` |
| Standing in smoke is recorded if growth happens without a lean | `RegisterStoodInSmokeIfNotLeaned` | `OfficeFireScenarioController.cs` and scenario states |
| Handrail starts as a mistake and clears after a hold | `RailingHoldTriggerVolume`, default 3 seconds | `Assets/Project/OfficeFire/Runtime/Scenario/Shared/Interactions/RailingHoldTriggerVolume.cs` |
| Elevator plays a warning line | `ElevatorProximity` → `DoNotUseElevator` | `ArchiveRoomScenarioController.cs` (same action exists on the other scenarios) |
| Outdoor assembly load | `outdoorSceneGroupName = "OutDoor"`, fade 0.45s | Scenario controller headers |
| Evacuation NPCs follow splines | `EvacuationNpcDirector`, `SplineNpcController` | `Assets/Project/OfficeFire/Runtime/Scenario/Shared/Evacuation/` |
| Result fields and bilingual copy | `OfficeFireResultScreenModel`, mapper | `OfficeFireResultScreenModel.cs`, `OfficeFireResultScreenMapper.cs` |
| CSV path and columns | `OfficeFireSessionResultCsvExporter` | `Assets/Project/OfficeFire/Runtime/Integration/UI/OfficeFireSessionResultCsvExporter.cs` |
| Hub quit vs application quit | `HandleQuitApplicationClicked` | `OfficeFireResultScreenController.cs` |
| Login leaderboard reads PlayerPrefs store | `TrainingLeaderboardStore` | `LocalPackages/com.woi.module.fire/Runtime/Events/Data/TrainingLeaderboardStore.cs`, `OfficeFireLoginScreenController.cs` |
| Office fire result controller does not call `TryRecordScore` | Search in `OfficeFireResultScreenController.cs` | `OfficeFireResultScreenController.cs` |
| Distance/angle mistake ids are labeled but not registered in the searched gameplay code | Enum and mapper only | `OfficeFireMistakeId.cs`, `OfficeFireResultScreenMapper.cs` |
| VR login prefills a test profile | `ApplyXrTestProfileDefaults` | `OfficeFireLoginScreenController.cs` |
| Unity, URP, OpenXR, XRI versions | Project settings and manifest | `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `Assets/XR Asset/XR/Settings/OpenXR Package Settings.asset` |
