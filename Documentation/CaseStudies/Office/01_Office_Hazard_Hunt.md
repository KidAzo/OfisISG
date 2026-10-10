# Office Hazard Hunt

## 1. One-Line Summary

An interactive office walkthrough in which a trainee searches a furnished workplace, identifies unsafe conditions, and receives a scored report of what they found and what they missed.

---

## 2. Overview

Office Hazard Hunt is one of three modules in the Office VR Training project. Its module id is `office-hazard`. The experience places the trainee inside a multi-room office and asks them to recognize hazards that are already present in the space: blocked exits, cables across walkways, overloaded power strips, poor posture, unsafe lifting, misplaced fire equipment, chemicals, clutter, and similar workplace risks.

The trainee is not asked to complete a scripted emergency. They move through the office, look at a condition or behavior, and confirm it. Each confirmation records the hazard as found, plays a localized explanation, and can change the scene so the unsafe condition is visually corrected. When the trainee chooses to finish, the module builds an Office Hazard Hunt Report with a score, a letter grade, elapsed time, and separate lists of found and missed hazards.

The audience visible in the product is an individual trainee who enters a name and a numeric user id, then trains in Turkish or English. The same office scene supports a desktop presentation and a VR presentation. From the WoiLabs Hub, the module is launched as its own Addressable entry and can return to the Hub after the report.

---

## 3. The Training Challenge

The module is built around recognition. Forty authored hazards are placed in the office, each with a name and a short explanation of why the condition is unsafe. The trainee has to notice them. Nothing in the session auto-completes when a quota is reached, and there is no countdown. The training ends when the trainee confirms that they want to finish, which means missed hazards stay missed and appear on the report.

That structure matches a “find what is wrong” exercise rather than a procedure drill. The content covers everyday office risk, including:

- slips, trips, and falls (cables, boxes, wet floors, open drawers, stairs, ladders, carrying loads)
- ergonomics (posture, screen distance, computer placement, lifting, a damaged chair)
- fire and emergency readiness (extinguisher location, blocked hose cabinet, overloaded power strip, blocked exits, fallen or sagging exit signs, an electrical panel)
- housekeeping, chemicals, and waste (unidentified chemicals, cleaning chemicals near food areas, overflowing bins, incorrect recycling, hazardous waste in an electronic-waste bin, sharp tools, clutter, dirty cafeteria tables)

The report is the teaching close. Found items confirm what the trainee recognized. Missed items name the risks they walked past.

---

## 4. Our Solution

The trainee enters through a login screen, then loads into the office. A center crosshair on the desktop view, and a controller ray in VR, defines what they are looking at. Confirming a hazard marks it found. The office answers in three ways at once: the found-count HUD advances, a popup states the hazard name and explanation, and a localized voice clip plays. Most hazards then animate from the unsafe arrangement to a corrected one, so the trainee sees the difference between the risk and the safer state.

Progress is visible during the walk, and the session stays open until the trainee asks to finish. A confirmation panel states that finishing will move them to the result screen. The report scores the session as the percentage of the hazard set that was found, assigns a letter grade, and lists both outcomes. A local leaderboard on the login screen keeps best scores on the device. When the module was opened from the Hub, the report also offers a return to the Hub.

---

## 5. Training Flow

1. The Hub loads `Hazard_Boot`, or a standalone build opens the hazard login. `Hazard_Boot` registers the module bootstrap and, for module id `office-hazard`, loads the `HazardHunt_Login` scene group. That group loads `LoginScreen`.
2. The trainee enters a player name and an optional numeric user id, chooses Turkish or English, and starts. The start button stays disabled until a name is present. A loading overlay tracks the office load.
3. The office scene (`Office`, Addressable key `HazardHunt_Office`) becomes the play space. Desktop presentation enables the PC UI and PC locomotion. VR presentation enables the VR canvases and the XR rig path.
4. The trainee walks the office. Hazards are already in the scene. Animated NPCs and waypoint walkers stage some of the unsafe behaviors. Doors can be opened and closed while exploring.
5. The trainee aims at a hazard and confirms it. On desktop, confirmation is the left mouse button or the E key, using a ray from the center of the view. In VR, confirmation is the right-controller trigger along the XR ray, within 8 meters.
6. The system marks that hazard fixed, adds it to the found set, updates the counter, shows the name and explanation, and plays the matching language clip. Toggle hazards scale the unsafe objects away and reveal the corrected objects. The cabinet/drawer hazard rotates the object closed.
7. The trainee continues until they choose to stop. On desktop, Tab opens the exit confirmation. In VR, the right-controller grip raises the same confirmation. Yes ends the session. No returns to the walk. Escape closes the desktop dialog.
8. The result screen shows the Office Hazard Hunt Report: player name, date, duration, score, letter grade, found count, and lists of found and missed hazards. The score is stored in a local leaderboard. The finish path also appends a CSV report on the machine running the session.
9. From the report, the trainee can return to the hazard login. If the session was launched from the Hub, they can also request a return to the Hub.

---

## 6. Core Gameplay / Training Mechanics

### Search and confirm

The trainee does not pick hazards from a menu. They look at an object or staged behavior and confirm it. A hazard that is already fixed is ignored by the detector. A confirmation that is not aimed at an unfixed hazard does nothing. There is no penalty score for a wrong aim.

This matters because the skill being practiced is noticing the condition in the room, not selecting an answer from a list.

### Forty placed hazards

The English and Turkish catalogs each contain 40 `HazardData` assets. The office scene contains 39 toggle-object hazards and 1 rotation hazard. The in-session counter is built against a total of 40, shown as a count and a “/ 40” label.

The authored hazards are:

- Cables running across walkways
- Boxes left on the floor
- Trip and fall risk due to office floor conditions
- No wet floor warning sign during floor cleaning
- Open cabinet or drawer doors
- Standing on a chair to reach items on upper shelves
- Failure to hold handrails while using the stairs
- Leaning against a stair railing
- Failure to follow the three-point contact rule on an A-frame ladder
- Obstructed line of sight while carrying materials
- Collision risk with a person approaching from the opposite direction
- Walking while using a mobile phone
- Walking while holding a hot beverage and a mobile phone
- Non-ergonomic sitting posture
- Working too close to the computer screen
- Improper positioning of the computer
- Lifting materials in a non-ergonomic manner
- Sitting on a broken or damaged chair
- Fire extinguisher not in its designated location
- Items or objects placed in front of the fire hose cabinet
- Fire risk from an overloaded power strip
- Emergency exit routes blocked by items and boxes
- Obstruction in front of emergency exit doors
- Emergency exit sign on the ground
- Sagging sign
- Unauthorized intervention with an electrical panel
- Unidentified chemical in the work area
- Cleaning chemicals stored near kitchen, pantry, or dining areas
- Trash bin overflowing
- Incorrect use of recycling bins
- Hazardous waste placed in an electronic-waste bin
- Sharp or cutting tools left unattended
- Scissors, utility knives, or similar sharp objects left on desks
- Cluttered work desk
- Cluttered workstation (5S)
- Cafeteria tables left dirty and cluttered
- Tea glass standing at the edge of a desk
- Uncontrolled handling of the tea/coffee machine
- Uncontrolled removal of supplies from the first-aid cabinet
- Getting hair caught in a paper shredder

### Visual correction

Most hazards are toggle operations. On confirmation, listed objects scale down and deactivate, and the corrected objects activate and scale in. The open cabinet/drawer hazard is a rotation: the configured objects rotate to a target angle over a set duration.

The trainee sees the unsafe state disappear and the safer state appear, tied to the explanation they just heard.

### Staged behavior

The office includes NPC animation controllers, waypoint walkers, a proximity trigger, and a resetter that returns NPCs to a walking state. The resetter will not reset an NPC sequence after its hazard has been fixed. These systems stage the behavioral hazards (walking, carrying, collisions, and similar) as something happening in the room.

### Doors

Doors implement the same interactable contract as hazards. Confirming a door swings it open or closed. Exploration of rooms is part of the search.

### Voluntary finish

There is no time limit and no “all hazards found” ending. `SceneTimer` records elapsed time from scene start. The trainee opens a confirmation that says, in the desktop copy, that they will finish the training and move to the result screen. Confirming that raises the finish event.

Missed hazards remain meaningful because the session can end before the set is complete.

### Scoring

The report score is the percentage of hazards found: found count divided by total count, multiplied by 100, then truncated to an integer. The per-hazard `score` field on the data assets is 0, so the letter grade follows how many hazards were found, not a weighted point value per hazard.

Grades:

| Score | Letter | English | Turkish |
|---|---|---|---|
| 90 and above | A | Excellent | Cok iyi |
| 75–89 | B | Good | Iyi |
| 60–74 | C | Average | Orta |
| Below 60 | D | Needs Improvement | Gelistirilmeli |

### Local leaderboard

On finish, the module submits user id, name, score, and duration. The stored record keeps the best score. An equal score with a shorter duration replaces the previous best. Attempt count and last-played time are updated. The login screen can show the top 10 entries stored on the device.

---

## 7. VR Interaction Design

Office Hazard Hunt uses the project’s shared PC/XR split, not a separate hazard-only rig.

**Desktop.** A `PlayerController` on a character controller moves at 5 units walking and 8 sprinting, with mouse look and gravity. The cursor locks during play and unlocks for the exit dialog and the result screen. Aiming uses a ray from the center of the camera. A screen-center crosshair is present in the office UI. Confirmation is the left mouse button or E. Tab opens the finish confirmation.

**VR.** The office scene contains an `XRPlayer` object whose child is the XR Interaction Toolkit starter prefab `XR Origin (XR Rig)`. That prefab includes left and right controllers and objects named Locomotion and Teleportation. The hazard interaction itself is a ray, not a grab or a socket. `InteractableController` takes the `XRRayInteractor` from the shared XR player service and selects the nearest interactable within 8 meters on the configured layer mask. The right-controller trigger raises the shared interact event. The right-controller grip raises the finish-confirmation event. The grip is deliberately not treated as an instant end: it opens the confirmation panel so the session does not close the moment the panel appears.

**Platform selection.** Hub launches pass a platform id. Values `vr` and `xr` select the VR presentation. Any other id, including the default `pc`, selects desktop. Standalone play uses the shared `AppMode` porting asset. On Android, an active XR device is also treated as VR. The OpenXR Android configuration for this project enables Meta Quest Support for Quest, Quest 2, Quest Pro, Quest 3, and Quest 3S. Hand-tracking aim in that settings asset is disabled.

**One scene, two presentations.** `HazardPlatformUiDriver` turns the PC UI root on for desktop and the VR gameplay canvases on for VR, and keeps the VR result root off until the report. Popups parent to a PC container or a VR container based on the same mode. Result UI selection looks for a controller whose object name contains `VR` or `XR`.

---

## 8. Feedback & Guidance

**What to do.** The login screen is the only structured onboarding found for this module. There is no separate tutorial scene or objective checklist. Guidance during the walk is the office itself, the center crosshair on desktop, the found-count HUD, and the finish confirmation copy.

**A valid find.** Confirming an unfixed hazard:

- increments the HUD through the shared `onHazardFixedEvent` (count out of 40 and a progress slider)
- opens a popup with the hazard title and description, scaled in, then hidden
- plays a clip from the language-specific sound definition, using the hazard id as the clip index
- runs the toggle or rotation correction

The popup stays up for the length of that clip, or for 1 second if no clip is resolved.

**A miss or a wrong aim.** The module does not show an error for looking at an ordinary object. Unfixed hazards simply stay unfixed. They are named later, on the report, under missed hazards.

**Language.** Login, hazard names, explanations, voice, the exit dialog, and the report are available in Turkish and English. Changing language on the login screen raises a language event. Each hazard swaps to the `HazardData` asset paired with that language.

**Completion.** The report title is “Office Hazard Hunt Report” / “Ofis Tehlike Avı Raporu”. It shows player name, date, duration as minutes and seconds, total score, letter and description, a found-versus-total percentage, and two lists: found hazards and missed hazards. The trainee can return to the hazard login. A Hub launch also shows “RETURN TO HUB” / “HUB'A DÖN”.

An outline component that moves an object onto an Outline layer is present on objects in the office scene. Per-hazard wiring of that outline to the moment of identification needs confirmation. The detector’s own highlight callbacks are not subscribed by another system.

---

## 9. Results & Progress Tracking

### Public-facing version

Each session produces an Office Hazard Hunt Report. The score is how much of the hazard set the trainee found. The report names both the hazards they identified and the ones they missed, and it records how long the walk took. The device keeps a local best-score list that the login screen can display. When the module is launched from the WoiLabs Hub, the trainee can leave the report and return to the Hub.

A server-side or admin-dashboard copy of this report was not found in the hazard result code. Treat centralized Hub storage of hazard scores as **Needs confirmation**.

### Technical notes

Completion is the finish confirmation, not a hazard quota.

`ExitPanelController.Raise` raises `onGameFinishEvent`. `HazardResultFinisher` listens to that event once per session and calls `HazardResultController.GetHazardResult`, then `GetCvsOutput.ExportHazardData`.

`HazardManager` registers every active `Hazard` at start. `BuildHazardCheckResult` splits them into found and missed `HazardData`. `HazardCheckResult.Score` is `(int)(found / total * 100)`. The report UI uses that value for the numeric score and for `HazardScoreCalculator` grades. The found-percentage label on the report uses a rounded percentage separately. The in-game counter uses a hardcoded total of 40 and increments on `onHazardFixedEvent`.

`LeaderboardService.SubmitScore` writes `leaderboard.json` under `Application.persistentDataPath`. It stores user id, user name, best score, best duration, best date, attempt count, and last played time. `LeaderboardUIController` on `LoginScreen` lists the top 10.

`HazardCsvExporter` appends `HazardResults.csv` on the desktop of the machine running the app. Columns include player name, player id, duration, detected hazard names, undetected hazard names, both counts, a safety-score string, and a timestamp. The CSV label bands (below 40, below 70, otherwise) differ from the A–D grade bands used on the report UI.

Player identity comes from the login form via `HazardHuntLogged` into `GameManager`, which persists across the scene load. An empty or non-numeric user id is stored as 0.

Hub integration in this module is launch and exit. `HazardModuleBootstrap` accepts only module id `office-hazard`, stores id, version, and platform, and loads `HazardHunt_Login`. Return to the Hub calls `IModuleExitHandler.RequestModuleExit`. No hazard script found in this pass posts the score to an HTTP API.

---

## 10. Technical Implementation

**Engine**

- Unity `6000.3.11f1`
- Universal Render Pipeline

**XR**

- OpenXR `1.16.1`
- XR Interaction Toolkit `3.5.0`, including the starter `XR Origin (XR Rig)`
- Unity Input System `1.19.0`
- Project OpenXR Android settings enable Meta Quest Support for Quest, Quest 2, Quest Pro, Quest 3, and Quest 3S

**Platform**

- Desktop presentation: character-controller player, locked cursor, screen-center ray
- VR presentation: XR Origin in the office scene, controller ray, trigger to confirm, grip to open the finish panel
- Android is a configured build target. The hazard VR path treats an active Android XR device as VR. A hazard-only Quest product listing was not separately verified.

**Architecture**

- Module entry scene `Hazard_Boot` with `HazardModuleBootstrap` and `FireServiceInstaller`
- Addressables group `Office_Hazard`, label `module-office-hazard-full`
- Addresses: `Hazard_Boot`, `HazardHunt_Login` (`LoginScreen`), `HazardHunt_Office` (`Office`)
- `SceneLoader` group entries for `HazardHunt_Login` and `HazardHunt_Office`, with catalog health checks for both
- After login, the office is loaded by `HazardOfficeStandaloneLoader`: Hub launches use the Addressable key; standalone play uses the built-in `Office` scene when it is in the player build
- Hazard runtime assembly `Woi.Office.Hazard`, referencing the shared office player, input, scene-group, audio, and module SDK assemblies
- Content is data-driven: `HazardData` ScriptableObjects, paired per language, plus operation configs on each hazard

**Backend / platform integration**

- Hub contract: `IModuleBootstrap.Initialize(ModuleLaunchContext)` and `IModuleExitHandler.RequestModuleExit`
- Session identity for the report is the name, numeric id, and language captured on the hazard login screen
- Results stay on device: report UI, `leaderboard.json`, and desktop CSV
- Shared service locator for scene loading and module exit

**Other**

- UI Toolkit login and leaderboard
- uGUI / TextMeshPro report, counter, exit panel, and popup
- PrimeTween for correction animation and popup motion
- UniTask for report layout and scene-load waits
- Obvious Soap events for hazard-fixed, finish, and interact signals
- Custom audio system: one English sound definition and one Turkish sound definition, 40 clips in the English bank, selected by hazard id

---

## Performance & Optimization

No runtime measurement was found for this module. Needs benchmark.

The values below are shared project configuration. They are not a frame-rate result, and no Hazard Hunt render override was found.

**Verified configuration**

- Standalone builds default to the `PC` quality level. Android builds default to the `Mobile` quality level. `ProjectSettings/QualitySettings.asset`
- `PC_RPAsset`: render scale `1`, URP MSAA disabled (`m_MSAA: 1`), HDR on, SRP Batcher on, main-light shadow map `2048`, additional-light shadows on, soft shadows on, `4` shadow cascades, URP shadow distance `50`. `Assets/Settings/PC_RPAsset.asset`
- `Mobile_RPAsset`: render scale `1`, URP MSAA disabled (`m_MSAA: 1`), HDR on, SRP Batcher on, main-light shadow map `1024`, additional-light shadows off, soft shadows off, `1` shadow cascade, URP shadow distance `50`. `Assets/Settings/Mobile_RPAsset.asset`
- Quality `vSyncCount` is `0` on both levels. That turns vsync off in the quality setting. It is not a measured frame rate.
- OpenXR Foveated Rendering is present and disabled on Standalone and Android. XR Performance Settings is disabled. Application SpaceWarp is disabled on Android. `Assets/XR Asset/XR/Settings/OpenXR Package Settings.asset`
- No `Application.targetFrameRate` assignment was found under `Assets/Project` or `LocalPackages`.

**Not found**

Configured refresh rate, measured FPS, CPU or GPU frame time, OVR Metrics, Unity Profiler captures, triangle or vertex counts, batches, SetPass or draw calls, a scene light count, occlusion data, LOD group counts, memory, GC, this module’s build size, and scene load time. `ProfilerCaptures` is empty. Android APKs under `Builds/Android` are named for VR Bridge, not for Hazard Hunt.

Do not publish a frame rate or a Quest performance number from this section.

---

## 11. Interesting Engineering Problems

### Problem

The same office scene has to open from the Hub as an Addressable and from a standalone player build as a normal scene.

### Solution

`HazardOfficeStandaloneLoader` checks the launch source. A Hub launch always loads Addressable key `HazardHunt_Office`. A standalone launch uses `SceneManager` when `Office` is enabled in the player build, and falls back to the Addressable key only if that scene is not in the build. An editor warning fires if `Office.unity` is both an enabled player scene and the `HazardHunt_Office` address. Catalog health checks for the hazard login and office groups resolve critical manager keys before load.

### Why It Matters

The module can ship inside the Hub catalog without a second copy of the office, and it can still be played when the scene is baked into a standalone build.

### Problem

Hub scene loading and a later “return to login” fight over which scene is the base scene.

### Solution

Hub start loads the office with single-scene mode, outside the scene-group manager, so the office becomes the base scene. A later group load of `HazardHunt_Login` cannot unload it by itself. After the login scene is loaded and set active, `HazardOfficeStandaloneLoader.UnloadAddressableOfficeIfLoaded` releases the Addressable scene handle, and the report controller unloads `Office` if the login scene is showing.

### Why It Matters

The trainee can replay from the hazard login, or leave a Hub session, without the office remaining stuck underneath the next screen.

### Problem

Addressables can load a second copy of the input ScriptableObjects, so the player and the interact listeners subscribe to events the live input manager is not raising.

### Solution

After the office loads, `HazardHuntStandaloneBootstrap` waits until `InputManager` exists and runs `OfficeFireInputSync`. That sync re-enables PC or VR gameplay input and rebinds Soap move, look, sprint, and interact listeners onto the live assets. VR grip uses the same rebind path. The hazard bootstrap logs a warning if the input manager never appears, because desktop move and look would not work.

### Why It Matters

Desktop locomotion and VR confirmation keep working after an Addressable load, which is the Hub delivery path.

### Problem

One office has to present a desktop HUD and a VR HUD without forking the scene.

### Solution

`HazardPlatformUiDriver` and `HazardHuntStandaloneBootstrap` toggle a PC UI root and VR gameplay roots from the Hub platform id or the shared `AppMode`. The info popup chooses `PCPopupContainer` or `VRPopupContainer`. The result controller keeps a PC report and a VR report and activates only the one for the current mode. The desktop exit dialog is created at runtime from the VR dialog’s events if a PC host is missing, including a localized yes/no panel that pauses movement and unlocks the cursor.

### Why It Matters

Hazard content stays in one scene while the Hub can launch the same module as desktop or VR.

### Problem

Every hazard needs a name, an explanation, and a voice line in two languages, and the language is chosen before the office exists.

### Solution

Login stores Turkish or English on `GameManager`, which survives the office load and sets `LanguageManager`. Each hazard holds a list of `HazardSettings` pairing a `HazardData` asset with a language, and refreshes when `OnLanguageChanged` fires. Voice is a separate sound definition per language. Playback uses clip index `hazardID - 1`, and the popup duration follows that clip’s length.

### Why It Matters

The same placed objects explain themselves in the language the trainee selected, including the spoken line, without a second office scene.

### Problem

The training should record misses, not only successes, and it should not end the moment the last hazard is found.

### Solution

`HazardManager` keeps every registered hazard until the trainee confirms finish. The result object is a found list plus a missed list. Score is the found ratio. The exit panel is an explicit confirmation, and the VR grip is wired to open that panel rather than to complete the session immediately.

### Why It Matters

The report can teach from what was overlooked, and a grip press in VR does not accidentally throw the trainee out of the exercise.

### Problem

Behavioral hazards have to play as ongoing office activity, then stop repeating once the trainee has identified them.

### Solution

NPCs use animation state playback and waypoint walking. `NpcResetter` stores start poses and can restart a walking state, and it refuses that reset once the hazard on the same object is fixed. A proximity trigger can fire when the player is close enough to staged NPCs.

### Why It Matters

Unsafe behavior stays visible while the trainee is still searching, and identifying it can settle the scene instead of looping the same incident.

### Problem

The in-game counter and the report must agree on the size of the exercise.

### Solution

The catalog is 40 English assets and 40 Turkish assets. The scene contains 40 hazard components. The counter prefab displays “/ 40” and listens to the shared `onHazardFixedEvent`, which the office scene references across the hazard set. The report recalculates from the live hazard list rather than from the counter.

### Why It Matters

The trainee sees a stable total during the walk, and the report still reflects the hazards actually present when the session ends.

The counter prefab’s UnityEvent still names assembly `OfficeAsmdef`. The hazard scripts now live in `Woi.Office.Hazard`. Whether that callback still resolves needs confirmation.

---

## 12. Reusable Systems

**SHARED SYSTEM**

- Hub module contract: `IModuleBootstrap`, `ModuleLaunchContext`, `IModuleExitHandler`, `ServiceLocator`
- `FireServiceInstaller` on the boot scene, registering scene loader and input manager
- `SceneLoader` / `SceneGroupManager` and Addressables catalog health checks
- `AppMode` porting (`PC` / `XR`) and `FirePlatformRuntime`, including the Android XR-device fallback
- `PlayerController`, `IXRPlayerService`, and the XR Interaction Toolkit XR Origin
- `InputManager`, PC and VR input contexts, and `OfficeFireInputSync`
- `OfficeFirePlayerInitializer` is present in the office scene for spawn placement. Fire scenario auto-start is off in this scene (`autoStartOnPlay` is disabled), so loading the office for hazard hunt does not start a fire scenario by itself
- Localization language enum and language-changed event
- Audio system `SoundDefinition` playback
- PrimeTween, UniTask, Soap events, UI Toolkit, TextMeshPro, URP

**MODULE-SPECIFIC SYSTEM**

- `HazardModuleBootstrap` and `HazardHubLaunch` for module id `office-hazard`
- `Hazard`, `HazardManager`, toggle and rotation operations
- `HazardData` catalogs under the hazard data folders
- `HazardDetector`, `InteractableController` hazard confirmation, door interaction used during the search
- Hazard HUD counter, info popup, exit confirmation, and `HazardSystemUIController` report
- `HazardScoreCalculator` letter grades
- Device leaderboard and desktop CSV export for this module
- NPC walkers, proximity trigger, and hazard-linked NPC reset used to stage hazard behaviors

---

## 13. Visuals We Should Capture

**Shot**
Wide view of the trainee inside the furnished office, crosshair or controller ray visible, with several workstations in frame.

**Purpose**
Hero. Establishes a realistic workplace search rather than a menu-driven quiz.

**Shot**
Desktop view centered on a cable across a walkway, crosshair on the cable, just before confirmation.

**Purpose**
Shows the core identification gesture: look, then confirm.

**Shot**
The same cable after confirmation, with the unsafe object gone and the corrected state visible, popup title and explanation on screen.

**Purpose**
Shows immediate feedback and the visual correction.

**Shot**
Controller ray in VR aimed at a blocked emergency exit or a fire extinguisher that is not in its place.

**Purpose**
Shows that the same search works with a headset and controller.

**Shot**
An NPC mid-action, such as walking while using a phone or standing on a chair, with the trainee aimed at them.

**Purpose**
Shows that the exercise includes unsafe behavior, not only static props.

**Shot**
The found-count HUD reading a partial total out of 40 during the walk.

**Purpose**
Shows that progress is visible before the final report.

**Shot**
The exit confirmation, Turkish or English, with the copy about finishing and moving to the result screen.

**Purpose**
Shows that ending the session is a deliberate choice.

**Shot**
The Office Hazard Hunt Report with a letter grade, duration, and the found and missed lists both populated.

**Purpose**
Shows the training output a supervisor can look at with the trainee.

**Shot**
Login screen with Turkish and English language buttons, and the local top-scores panel if it is populated with non-sensitive sample names.

**Purpose**
Shows bilingual entry and that the module remembers device-local results. Do not capture real employee ids.

**Shot**
A doorway between two office areas, such as the kitchen/café or a meeting room, opened by the trainee.

**Purpose**
Shows that the search covers a multi-room workplace.

---

## 14. Website Highlight Points

- Search a furnished office for 40 workplace hazards
- Point and confirm on desktop or in VR
- Spoken, bilingual explanation for each hazard
- See the unsafe condition change to a corrected state
- Scored report of found and missed hazards
- Hub launch with a return to the Hub

---

## 15. Website Case Study Draft

# Office Hazard Hunt

### Hero Description

Office Hazard Hunt is an interactive safety exercise set inside a working office. Trainees walk the space, identify hazards in context, and finish with a report that shows both what they found and what they missed. The same module runs as a desktop session or in VR, in Turkish or English.

### The Challenge

Workplace hazards are easy to list in a slide and easy to walk past in a real office. Cables on the floor, a blocked exit, a chair used as a ladder, an extinguisher that is not where it belongs: each one is ordinary until someone recognizes it.

Office Hazard Hunt turns that recognition into the exercise. The office is already unsafe in specific, authored ways. The trainee’s job is to notice those conditions and confirm them. The session does not end on a timer. It ends when the trainee decides they have finished looking, so anything still unfound becomes part of the result.

### The Experience

The trainee enters a name, chooses Turkish or English, and steps into the office. On a desktop they move with the keyboard and aim from the center of the screen. In VR they move with the headset rig and aim with the controller ray. Confirming a hazard marks it found.

The room responds immediately. A short panel names the hazard and explains the risk, a voice line plays in the selected language, and the scene shows a safer arrangement: the obstruction clears, the object closes, the unsafe setup is replaced. A counter tracks how many of the 40 hazards have been found. Animated coworkers carry some of the risks, so the search includes behavior as well as objects.

### How It Works

The trainee explores the office and confirms each hazard they believe is unsafe. Found hazards stay found. When they are ready, they open a confirmation and finish the session. The Office Hazard Hunt Report shows a percentage score, a letter grade from A to D, the time taken, and two lists: hazards found and hazards missed. From a Hub launch, they can return to the Hub. From the module itself, they can return to the login screen and start again.

### Key Features

- Forty authored office hazards, each with a Turkish and English name, explanation, and voice line
- Point-and-confirm interaction on desktop and with a VR controller ray
- Visual correction when a hazard is identified
- Live found-count progress during the walk
- A finish report with score, grade, duration, and missed hazards
- Local best-score list on the device, plus Hub launch and Hub return

### Technology

Built in Unity with the Universal Render Pipeline, OpenXR, and the XR Interaction Toolkit. The module is its own Addressable entry, `office-hazard`, with a small boot scene, a login scene, and the shared office scene. Desktop and VR are two presentations of that office, selected from the Hub platform or the project’s PC/XR mode. Meta Quest device targets are enabled in the project’s OpenXR Android settings.

### Integration

Office Hazard Hunt is a separate Hub module from Office Fire Training and Office Waste Collection. The Hub starts it through the module bootstrap and can take the trainee back when they leave the report. Sign-in for this module is the hazard login screen: name, numeric user id, and language. Those values appear on the report.

The score, the found and missed lists, and the duration are shown in the session and stored on the device. A local leaderboard on the login screen keeps the best result for each user id.

### Outcome

The module gives an organization a repeatable way to practice office hazard recognition in a realistic space, in the trainee’s language, without waiting for those conditions to appear at work. Each session ends with a concrete record of what the trainee recognized and what they did not.

---

# Evidence Appendix

Internal only. Not for the public website.

| Claim | Evidence | File / Scene |
|---|---|---|
| Module id is `office-hazard` | Bootstrap accepts only that id and stores the Hub launch | `Assets/Project/scripts/Bootstrap/HazardModuleBootstrap.cs` |
| Hub entry scene is `Hazard_Boot` | Scene contains `HazardModuleBootstrap` and `FireServiceInstaller`; Addressable address `Hazard_Boot` | `Assets/Project/Scenes/HazardModule/Hazard_Boot.unity`, `Assets/AddressableAssetsData/AssetGroups/Office_Hazard.asset` |
| Login scene group loads `LoginScreen` | SceneLoader group `HazardHunt_Login` uses address `HazardHunt_Login` | `Assets/Project/Reflex/Resources_moved/SceneLoader.prefab`, `Assets/OfficeStartScene/LoginScreen.unity` |
| Login collects name, numeric id, TR/EN | UI Toolkit fields and start rules; id must be digits; language 0 = TR, 1 = EN | `Assets/Project/scripts/Bootstrap/LoginScreenController.cs`, `Assets/Project/_scripts/UI_Toolkit/LoadingScreen.uxml` |
| Start stays disabled until a name exists | `UpdateStartButton` | `Assets/Project/scripts/Bootstrap/LoginScreenController.cs` |
| Office loads after login | `GameInitializer` listens for `HazardHuntLogged` and loads `Office` | `Assets/Project/scripts/Bootstrap/GameInitializer.cs` |
| Hub office load uses Addressables; standalone prefers the player build | `HazardOfficeStandaloneLoader` | `Assets/Project/scripts/Bootstrap/HazardOfficeStandaloneLoader.cs` |
| Addressable office key is `HazardHunt_Office` on `Office.unity` | Addressable group entry and scene guid | `Assets/AddressableAssetsData/AssetGroups/Office_Hazard.asset`, `Assets/Project/Scenes/Office.unity.meta` |
| Editor warns if Office is both a build scene and that address | Ownership warning | `Assets/Project/OfficeFire/Editor/HazardOfficeSceneOwnershipWarning.cs` |
| 40 English and 40 Turkish hazard definitions | Asset folders | `Assets/Project/_scripts/HazardSystem/HazardDatas/ENG`, `Assets/Project/_scripts/HazardSystem/HazardDatas/TR` |
| Scene contains 39 toggle hazards and 1 rotation hazard | Script guid counts in `Office.unity` | `ToggleObjectsHazard.cs.meta`, `RotationObjectHazard.cs.meta`, `Assets/Project/Scenes/Office.unity` |
| `EventHazard` is not placed in the office scene | No matches for its script guid | `Assets/Project/Scenes/Office.unity` |
| Per-hazard score field is 0 | `score:` search across hazard assets | `Assets/Project/_scripts/HazardSystem/HazardDatas` |
| Report score is found/total × 100 | `HazardCheckResult.Score` | `Assets/Project/HazardRuntime/Core/HazardCheckResult.cs` |
| Grades A/B/C/D at 90/75/60 | `HazardScoreCalculator.GetGrade` | `Assets/Project/HazardRuntime/Result/TehlikeAviScoreCalculator.cs` |
| Counter total is 40 and label is “/ 40” | `HazardCounterUIController`, Counter prefab text | `Assets/Project/HazardRuntime/UI/HazardCounterUIController.cs`, `Assets/Project/_scripts/HazardSystem/PrefabsUI/Counter.prefab` |
| Counter increments from the shared fixed event | Prefab binds `SetCounter` to `onHazardFixedEvent`; scene references that event guid | `Counter.prefab`, `Assets/Project/_scripts/HazardSystem/Events/onHazardFixedEvent.asset.meta`, `Office.unity` |
| Confirming a hazard marks it fixed and raises the event | `Hazard.Fix` | `Assets/Project/HazardRuntime/Core/Hazard.cs` |
| Language swaps the data asset on each hazard | `SetHazardData` / `OnLanguageChanged` | `Assets/Project/HazardRuntime/Core/Hazard.cs`, `Assets/Project/scripts/Bootstrap/LanguageManager.cs` |
| Toggle correction scales objects out and in | `ToggleObjectsOperation` | `Assets/Project/HazardRuntime/Operations/ToggleObjectsOperation.cs` |
| Rotation correction tweens rotation | `RotationObjectsOperation`; open-cabinet data type is Rotation (enum value 2) | `ToggleObjectsOperation.cs`, `Open cabinet or drawer doors.asset` |
| Voice uses hazard id as clip index | `HazardManager.PlayFixedSound`; English bank clip count is 40 | `Assets/Project/HazardRuntime/Core/HazardManager.cs`, `Assets/Project/_scripts/SoundDatas/ENG-Sound Definition.asset` |
| Popup shows title and description for the clip length | `HazardInfoPopupPresenter` | `Assets/Project/HazardRuntime/Feedback/HazardInfoPopupPresenter.cs` |
| Desktop aim is screen center; click or E confirms | `CameraCenterRayProvider`, `PcInteractionInput`, `InteractableController` | `Assets/Project/HazardRuntime/Detection/CameraCenterRayProvider.cs`, `PcInteractionInput.cs`, `Interaction/InteractableController.cs` |
| Center crosshair exists in the office UI | Scene object `Crosshair` | `Assets/Project/Scenes/Office.unity` |
| VR ray uses `XRRayInteractor` within 8 m; trigger event is `onInteractInput` | Scene values on `InteractableController`; event guid matches `onInteractInput.asset` | `Office.unity`, `InteractableController.cs`, `VrInputContext.cs` |
| Right trigger is XR Interact; right grip is FinishedGame | Input action bindings | `LocalPackages/com.woi.module.fire/Runtime/InputSystem/PlayerInputActions.cs` |
| Grip opens the exit panel and does not instantly finish | `VrInputContext` comment and raise of `preOnGameplayFinishedInput`; exit panel listens to `preOnGameFinishEvent` | `VrInputContext.cs`, `ExitPanelController.cs`, `Office.unity` |
| Desktop finish key is Tab; Escape closes the dialog | `ExitPanelController.Update`; GameplayFinished binding is Tab | `ExitPanelController.cs`, `PlayerInputActions.cs` |
| Finish confirmation copy | Desktop strings in `ApplyPcCopy` | `Assets/Project/HazardRuntime/UI/ExitPanelController.cs` |
| PC player walk 5, sprint 8 | `PlayerController` serialized in `Office.unity` | `LocalPackages/com.woi.module.fire/Runtime/Player/PlayerController.cs`, `Office.unity` |
| XR Origin starter rig is in the office under `XRPlayer` | Prefab guid `f6336ac4ac8b4d34bc5072418cdc62a0`; objects Locomotion and Teleportation | `Office.unity`, `Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab` |
| PC vs VR UI roots | `HazardPlatformUiDriver`, `HazardHuntStandaloneBootstrap` | `Assets/Project/scripts/Bootstrap/HazardPlatformUiDriver.cs`, `Assets/Project/HazardRuntime/Compat/HazardHuntStandaloneBootstrap.cs` |
| Hub platform `vr`/`xr` selects VR | `HazardHubLaunch` | `HazardModuleBootstrap.cs` |
| Android XR device counts as VR | `FirePlatformRuntime.IsXrDeviceActiveFallback` | `LocalPackages/com.woi.module.fire/Runtime/Porting/FirePlatformRuntime.cs` |
| Quest device targets enabled in OpenXR Android settings | `MetaQuestFeature Android` `m_enabled: 1`, included in the Android feature list | `Assets/XR Asset/XR/Settings/OpenXR Package Settings.asset` |
| Report contents and bilingual labels | `HazardSystemUIController.BuildReport` | `Assets/Project/HazardRuntime/Result/HazardSystemUIController.cs` |
| Result pipeline from finish event | `HazardResultFinisher` serialized to `onGameFinishEvent`; same guid on the exit panel | `HazardResultFinisher.cs`, `Office.unity` |
| Local leaderboard file and top 10 | `LeaderboardService`; UI on `LoginScreen` | `Assets/Project/HazardRuntime/Leaderboard/LeaderboardService.cs`, `LeaderboardUIController.cs`, `Assets/OfficeStartScene/LoginScreen.unity` |
| Desktop CSV export and different label bands | `HazardCsvExporter` | `Assets/Project/HazardRuntime/Data/HazardCsvExporter.cs` |
| No hazard HTTP score upload found | Result path ends at UI, JSON, CSV, and Hub exit | `HazardResultController.cs`, `HazardSystemUIController.cs` |
| Hub return | `ReturnToHub` calls `IModuleExitHandler.RequestModuleExit` | `HazardSystemUIController.cs`, `LocalPackages/com.woi.modules.sdk/Runtime/Contracts/IModuleExitHandler.cs` |
| Return to login unloads the addressable office | `LoadHubLoginAsync` | `HazardSystemUIController.cs`, `HazardOfficeStandaloneLoader.cs` |
| Doors open and close on interact | `Door` | `Assets/Project/HazardRuntime/Interaction/Door.cs` |
| NPC staging and reset-after-fix | `NPCAnimationController`, `PathWalker`, `NpcProximityTrigger`, `NpcResetter` counts in `Office.unity` | `Assets/Project/HazardRuntime/NPC/` |
| Fire scenarios do not auto-start with this office load | `OfficeFireScenarioBootstrapper.autoStartOnPlay: 0` | `Office.unity`, `OfficeFireScenarioBootstrapper.cs` |
| Office contains shared rooms used by the environment | Scene object names include `ServerRoom`, `MeetingRoomBig`, `Kitchen`, `KitchenCafe`, `ArchiveRoom` | `Office.unity` |
| Input rebind after Addressable load | `OfficeFireInputSync` called from the hazard bootstrap | `HazardHuntStandaloneBootstrap.cs`, `Assets/Project/OfficeFire/Runtime/Scenario/Shared/OfficeFireInputSync.cs` |
| Unity, URP, Addressables, Input System, XRI, OpenXR versions | Project manifest and editor version | `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `ProjectSettings/GraphicsSettings.asset` |
| Outline component exists in the scene; aim-highlight callbacks are unused | `TriggerOutlineLayer` guid count; `HazardDetector` events are private with no external subscribers | `LineworkOutlineSimple.cs`, `HazardDetector.cs`, `Office.unity` |
| No separate tutorial flow found | No tutorial/onboarding types in hazard runtime | `Assets/Project/HazardRuntime` |
| Elapsed time has no limit | `SceneTimer` only returns elapsed time | `Assets/Project/HazardRuntime/Supporting/SceneTimer.cs` |
| Counter callback assembly name may be stale | UnityEvent assembly string `OfficeAsmdef` vs asmdef `Woi.Office.Hazard` | `Counter.prefab`, `Assets/Project/HazardRuntime/Woi.Office.Hazard.asmdef` |
