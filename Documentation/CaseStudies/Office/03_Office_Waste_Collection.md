# Office Waste Collection

## 1. One-Line Summary

A zero-waste office exercise in which the trainee picks up discarded items, sorts each one into a waste bin, and finishes with a report of correct, incorrect, and uncollected items.

---

## 2. Overview

Office Waste Collection is the third module in the Office VR Training project. Its module id is `office-waste`. The login screen calls it “ZERO WASTE / TRAINING SIMULATOR” in English and “SIFIR ATIK / EĞİTİM SİMÜLATÖRÜ” in Turkish.

The trainee walks an office, selects a waste item, and then chooses which bin it belongs to. The choice is checked immediately. A correct or incorrect result is shown with sound, and many items also play a spoken explanation. The item disappears from the room once it has been picked up. A counter shows how many items have been collected out of how many are in the scene.

The session does not end when the last item is sorted. The trainee confirms that they want to finish. The result is a Waste Classification Evaluation Report: each sorted item, the bin they chose, the correct bin, and a status. Items still in the room are listed as not found.

---

## 3. The Training Challenge

The module is a sorting drill. Office waste is not one category. The authored key distinguishes paper and cardboard, plastic bottles, bottle caps, glass, food waste, batteries, electronics, metal cans, composite cartons, medical masks, wet wipes, and similar items. Several of those do not follow the generic material type: a toner cartridge and a light bulb are mapped to electronic waste, a juice carton to composite waste, a wet wipe to domestic waste, and a bottle cap to its own cap bin.

The trainee has to recognize the object in the room and then name the right bin. A wrong bin is recorded, explained, and still counts as an attempt. Items they never pick up stay visible to the report as not found, so leaving waste behind is part of the result.

---

## 4. Our Solution

The office contains selectable waste objects. Aiming and confirming one outlines it, plays a selection sound, scales it away, and records the pickup. Movement then pauses and a bin menu opens for that item. The menu asks the trainee to select the target bin. Twelve bins are offered, with icons and localized names.

The answer is compared with a fixed catalog. Correct and incorrect each have their own sound. If the item has an explanation, a popup shows the status and the explanation text for about 2.5 seconds while a voice line continues. Gameplay resumes after that. The trainee repeats this until they choose to finish.

Desktop uses a mouse ray. VR uses a visible ray from the right controller, up to 12 meters, and presents the menus in world space in front of the trainee. Finish is Tab on desktop and the right-controller grip in VR. Grip is ignored while the bin menu is open.

---

## 5. Training Flow

1. The Hub loads `Waste_Boot` for module id `office-waste` and then the `WasteLogin` scene group. A standalone start can also open that login.
2. The trainee enters a full name and a user id, and chooses Turkish or English. Both fields are required. The login shows a device-local leaderboard.
3. Start stores the profile. A Hub launch then loads the `Waste_Office` scene group. A standalone launch loads the `WasteCollector` group, which loads `FireModule_Office`.
4. The trainee moves through the office. A counter reads collected versus the number of waste objects found in the scene. An intro announcement can play until the first item is collected, then it stops.
5. The trainee aims at an item and confirms. The item outlines, scales out, and is removed. Its name, type, time, and position are stored.
6. The bin menu opens with the item’s display name and the prompt “Select the target waste bin.” Choosing a bin records the selected bin and the correct bin. Closing the menu without a choice dismisses it.
7. The trainee hears correct or incorrect, and, when the item has one, sees and hears an explanation. Movement returns.
8. Tab, or the VR grip, asks “Are you sure you want to finish the game?” Confirming opens the result. Cancel returns to the office.
9. The report lists every classification and every item still in the scene. Correct and incorrect counts are shown. A success percentage is stored on the device leaderboard, and the rows are appended to a local CSV. From a Hub launch, the trainee can return to the Hub. They can also restart.

---

## 6. Core Gameplay / Training Mechanics

### Pick up, then sort

Selection and classification are two steps. Picking an item does not choose the bin. The pickup removes the object and opens the menu. That split keeps “I found this” separate from “I know where it goes.”

### What the catalog sorts

Display names and correct bins implemented for the named items:

| Item | Correct bin |
|---|---|
| Paper, cardboard box | Paper and cardboard |
| Water bottles | Plastic |
| Plastic bottle cap | Plastic cap |
| Glass bottle | Glass |
| Food waste | Biodegradable |
| Battery | Used battery |
| Toner cartridge, broken keyboard, light bulb | Electronic waste |
| Soda can | Metal |
| Juice box (including the `sut` key) | Composite |
| Face mask | Medical |
| Baby wipe | Domestic |

If a name is not in that table, the code falls back by material type: paper, plastic, glass, metal, or organic. Anything else falls back to the recyclable bin.

The selection menu offers these bins: paper and cardboard, plastic, glass, biodegradable, used battery, electronic waste, plastic cap, metal, recyclable, domestic, medical, and composite. The wider name catalog also defines cardboard-only, toner and cartridge, cigarette butt, and hazardous waste. Those four are not buttons in the selection menu.

### Immediate judgment

A choice is correct only when the selected bin id equals the catalog bin id. The popup says CORRECT or INCORRECT. Correct and incorrect sounds are separate localized banks. The explanation voice is allowed to continue after the popup hides itself.

### Progress

The counter label is `collected/total`. Total is the number of `WasteController` objects in the loaded scene, including inactive ones, counted when the HUD starts. Each pickup raises a collected event with the new total.

### Finish and leftovers

Finishing is voluntary. The result table has four columns: waste, selected bin, correct bin, and status. Sorted rows use correct or incorrect. Items still active in the scene are added as not found, with no selected bin and with the correct bin filled in. If nothing was sorted and nothing remains, the table says that no classifications were made yet.

The leaderboard percentage is correct classifications divided by classifications made, rounded. Uncollected items are on the report and are not part of that percentage.

---

## 7. VR Interaction Design

**Desktop.** Selection uses a ray from the mouse position into the scene. Left click confirms. Tab opens the finish confirmation. While the bin menu or explanation is up, movement and look are frozen and selection input is turned off. The explanation step turns gameplay input back on so the trainee is not stuck for the whole voice line, while the popup still auto-hides.

**VR.** `SelectionVrInteractionRay` finds the right controller and casts along its forward direction, starting 8 centimeters out, up to 12 meters. A thin world-space line is drawn in front of the controller. The bin menu, explanation, and result are presented by a world-UI presenter so the same UI Toolkit documents sit in the room. The right-controller grip toggles the exit confirmation, and it does nothing while the bin menu is visible. A locomotion gate can hold VR movement while a panel is open.

**Platform selection.** Hub platform ids `vr` and `xr` select VR and update the shared PC/XR porting asset. Other ids default to PC. Waste presentation then follows that porting asset. If the asset is already initialized as PC, an XR camera in the scene does not flip the session to VR.

**Scenes.** Hub play uses `Waste_Office`, which contains the waste collection root, the tracker, the waste UI, and a VR bootstrap. Standalone play loads `FireModule_Office` through the `WasteCollector` group. That fire office scene is also the fire-training gameplay scene, and its fire scenario bootstrapper is set to auto-start. How visible that fire scenario is during a standalone waste session needs confirmation in play. The Hub waste scene did not contain that auto-start flag.

---

## 8. Feedback & Guidance

**What to do.** The bin menu subtitle is “WASTE ITEM CONFIRMED” and the description is “Select the target waste bin.” The item name at the top is the localized display name. The counter shows collection progress. The finish dialog asks for confirmation before the report.

**Whether the sort was right.** Status text, status color, a correct or incorrect icon, a result sound, and an optional explanation are all driven from the last classification. The explanation text is Turkish or English from the waste definition. If the English text is empty, Turkish is used.

**Language.** Login defaults through the Turkish dropdown label. At runtime, a session language choice wins, then the waste login session, then the shared localization service.

**Completion.** The report title is “Simulation Completed” and the subtitle is “Waste Classification Evaluation Report.” Counts are labeled Correct and Wrong. Buttons include Restart, Quit Game, and, on a Hub launch, Return to Hub. Quit is hidden or replaced according to that launch. There is no separate letter grade.

**Pickup feedback.** The selected object enables a child outline at width 5, plays feedback audio, scales to zero, then deactivates. A per-item selection sound can also play at the object’s position.

---

## 9. Results & Progress Tracking

### Public-facing version

The trainee leaves with a classification report: what they picked up, which bin they chose, which bin was correct, and what they left in the office. Correct and incorrect totals are on the screen. The device remembers a success percentage for the login leaderboard. A local file copy of the session can be written for review. From the Hub, they can return to the Hub or start again.

A Hub PC launch does not send that file to the older companion-PC uploader. Other launches can, when a session manager is present. Treat any external admin dashboard beyond that as **Needs confirmation**.

### Technical notes

`WasteCollectTracker` stores `WasteCollectRecord` rows at pickup and `WasteClassificationRecord` rows when a bin is chosen. Classification stores waste name, waste type, selected bin id and name, correct bin id and name, and `isCorrect`.

`WasteResultScreenController` builds the table from classifications plus `GetUncollectedSceneWastes`, which scans active `WasteController` objects. Export runs only if at least one classification exists. `WasteLeaderboardStore` keeps a success percent in PlayerPrefs, keyed by user id. `WasteSessionResultCsvExporter` appends `atik_toplama_oyuncu_sonuclari.csv` under a desktop folder named `AtıkToplamaOyuncuSonucları`.

The same exporter can call `SessionManager.SendResultToPC` with the CSV text. That call is skipped when the launch is a Hub PC launch. The session manager’s address is a local network setting and must not be published.

Identity comes from `WasteLoginSession` or `GameSessionData`, which the login fills with the typed name and id. The Hub launch explicitly does not copy a Hub account into that participant login.

---

## 10. Technical Implementation

**Engine**

- Unity `6000.3.11f1`
- Universal Render Pipeline

**XR**

- OpenXR `1.16.1` and XR Interaction Toolkit `3.5.0`, shared with the office project
- Right-controller ray and grip through the shared input events
- World-space UI Toolkit for VR panels
- Meta Quest device targets are enabled in the project OpenXR Android settings

**Platform**

- Desktop mouse selection and Tab to finish
- VR controller ray, world UI, and grip to finish
- Hub platform id selects PC or VR before the office loads

**Architecture**

- Module id `office-waste`
- Boot scene `Waste_Boot` with `WasteModuleBootstrap` and the shared service installer
- Addressable groups for waste login and `Waste_Office`
- Scene groups: `WasteLogin`, `Waste_Office`, and `WasteCollector` (`FireModule_Office`)
- `WasteCollectTracker` plus `WasteDefinition` assets for each item
- `WasteBinCatalog` and `WasteNameCatalog` as the sorting and display keys
- Assembly `Woi.Office.Waste`

**Backend / platform integration**

- Hub bootstrap and `IModuleExitHandler.RequestModuleExit`
- Participant name and id captured on the waste login, not copied from a Hub account
- Local CSV and local leaderboard
- Optional CSV handoff to a companion PC through `SessionManager` when that manager exists and the launch is not Hub PC

**Other**

- UI Toolkit menus, counter, explanation popup, and result table
- Localized voice on select, on correct or incorrect, and as an explanation
- PrimeTween scale-out on pickup
- Outline highlight on the selected object
- Input freeze while the bin menu is open

---

## Performance & Optimization

No runtime measurement was found for this module. Needs benchmark.

The values below are shared project configuration. They are not a frame-rate result, and no Waste Collection render override was found. Hub play loads `Waste_Office`. Standalone play loads `FireModule_Office`. That split is a scene-path fact, not a benchmark.

**Verified configuration**

- Standalone builds default to the `PC` quality level. Android builds default to the `Mobile` quality level. `ProjectSettings/QualitySettings.asset`
- `PC_RPAsset`: render scale `1`, URP MSAA disabled (`m_MSAA: 1`), HDR on, SRP Batcher on, main-light shadow map `2048`, additional-light shadows on, soft shadows on, `4` shadow cascades, URP shadow distance `50`. `Assets/Settings/PC_RPAsset.asset`
- `Mobile_RPAsset`: render scale `1`, URP MSAA disabled (`m_MSAA: 1`), HDR on, SRP Batcher on, main-light shadow map `1024`, additional-light shadows off, soft shadows off, `1` shadow cascade, URP shadow distance `50`. `Assets/Settings/Mobile_RPAsset.asset`
- Quality `vSyncCount` is `0` on both levels. That turns vsync off in the quality setting. It is not a measured frame rate.
- OpenXR Foveated Rendering is present and disabled on Standalone and Android. XR Performance Settings is disabled. Application SpaceWarp is disabled on Android. `Assets/XR Asset/XR/Settings/OpenXR Package Settings.asset`
- No `Application.targetFrameRate` assignment was found under `Assets/Project` or `LocalPackages`.

**Not found**

Configured refresh rate, measured FPS, CPU or GPU frame time, OVR Metrics, Unity Profiler captures, triangle or vertex counts, batches, SetPass or draw calls, a scene light count, occlusion data, LOD group counts, memory, GC, this module’s build size, and scene load time. `ProfilerCaptures` is empty. Android APKs under `Builds/Android` are named for VR Bridge, not for Waste Collection.

Do not publish a frame rate or a Quest performance number from this section.

---

## 11. Interesting Engineering Problems

### Problem

Sorting has to be a decision, not a side effect of touching the object.

### Solution

`WasteController.Select` collects the item and plays pickup feedback. Only `WasteCollectedEvent` opens the bin menu. `WasteCollectionResultController` freezes movement, disables further selection, and records a classification when a bin id comes back. Dismissing the menu does not invent a bin.

### Why It Matters

The trainee can see the object, then deliberately choose a stream, which is the skill the report measures.

### Problem

The correct bin is not always the material enum on the object.

### Solution

`WasteBinCatalog` maps specific names first. Comments in that map record the intended exceptions: cardboard shares the paper-cardboard bin, wet wipes are domestic waste, cartridge and bulb are electronic, juice is composite. Only unknown names fall through `WasteType`.

### Why It Matters

The exercise can teach real office exceptions, such as caps, batteries, and cartons, without a new interaction for each exception.

### Problem

The same menus have to work on a monitor and inside a headset.

### Solution

The documents are UI Toolkit. VR adds a world presenter, a controller ray with its own line, a locomotion gate, and grip-to-exit. PC uses the mouse and Tab. Both paths call the same menu, tracker, and result controller. The explanation popup lives on the same UI root the VR presenter shows, so the correct or incorrect panel is not a second implementation.

### Why It Matters

Content and scoring stay one system while the Hub can launch either presentation.

### Problem

A trainee can finish before the room is clear, and the report should say so.

### Solution

The result does not require every item to be sorted. After the classifications, it scans waste objects that are still active and adds them as “NOT FOUND,” with the correct bin and an empty selection.

### Why It Matters

Leaving waste in the office is visible in the training record, not silently ignored.

### Problem

Voice explanation and the next pickup can overlap.

### Solution

`WasteAudioFeedback` keeps one active voice chain and can stop it when another item is collected. The explanation popup hides on its own timer and does not wait for the clip to end. Collecting a new item cancels an in-progress explanation flow before the new menu opens.

### Why It Matters

The trainee can keep moving through the office without queued voice lines stacking up.

### Problem

Hub delivery and an older companion-PC result path both exist.

### Solution

Every qualifying session still writes the CSV locally and updates the device leaderboard. The companion upload runs only when a `SessionManager` is present and the launch is not a Hub PC session. The bootstrap also refuses any module id other than `office-waste`.

### Why It Matters

The Hub module does not depend on the older room server, and a mis-addressed launch does not start the waste login.

### Problem

Standalone and Hub do not load the same office scene.

### Solution

After login, Hub loads `Waste_Office`. Standalone loads `WasteCollector`, whose only scene is `FireModule_Office`. `Waste_Office` contains the waste root and tracker. `FireModule_Office` is the fire-training office and has fire auto-start enabled.

### Why It Matters

The waste systems are real in both entries, but the standalone scene is shared with fire training. That difference should be checked before a public demo is recorded from the standalone path.

---

## 12. Reusable Systems

**SHARED SYSTEM**

- Hub `IModuleBootstrap`, service locator, and `FireServiceInstaller`
- `SceneLoader`, Addressables, and catalog health checks
- `FirePlatformRuntime` PC/XR porting
- Shared player, XR rig, and input events, including the grip event used to finish
- Localization service and session language state
- UI Toolkit, audio system, URP, OpenXR
- `SessionManager` companion-PC channel, when present

**MODULE-SPECIFIC SYSTEM**

- Module id `office-waste` and `WasteHubLaunch`
- Waste login and `WasteLoginSession`
- `WasteController`, `WasteCollectable`, `WasteDefinition`, and `WasteCollectTracker`
- Bin menu, explanation popup, counter, and classification report
- `WasteBinCatalog` and `WasteNameCatalog`
- VR selection ray, world UI, and waste locomotion gate
- `Waste_Office` scene content and the waste Addressable group
- Device `WasteLeaderboardStore` and the waste CSV exporter

---

## 13. Visuals We Should Capture

**Shot**
Wide office view with several obvious waste items on desks and floors, trainee aimed at one of them.

**Purpose**
Hero. Shows a workplace sorting task, not a diagram of bins.

**Shot**
Close view of a highlighted bottle, battery, or mask as it is selected, outline visible.

**Purpose**
Shows the pickup moment.

**Shot**
The bin menu in front of the trainee, item name at the top, grid of colored bins.

**Purpose**
Shows the decision the training is about.

**Shot**
Correct or incorrect explanation popup with the status and a short explanation. Use a harmless sample item.

**Purpose**
Shows immediate teaching feedback.

**Shot**
VR controller ray touching an item, world-space menu in the room.

**Purpose**
Shows the headset version of the same task.

**Shot**
The collection counter, for example a partial total, during play.

**Purpose**
Shows that progress is visible before the report.

**Shot**
Finish confirmation: “Are you sure you want to finish the game?”

**Purpose**
Shows that ending is intentional.

**Shot**
The evaluation report with correct and incorrect counts and at least one not-found row. Use test names only.

**Purpose**
Shows the record: chosen bin, correct bin, and what was left behind.

**Shot**
A set of contrasting items in one frame: paper, a plastic bottle, a battery, and food waste.

**Purpose**
Communicates that the office contains more than one waste stream.

---

## 14. Website Highlight Points

- Pick up office waste and sort it into the correct bin
- Separate streams for paper, plastic, glass, batteries, electronics, medical waste, and more
- Immediate correct or incorrect feedback with a spoken explanation
- Items left behind are listed as not found
- Desktop and VR controller play
- Session report with a return to the Hub

---

## 15. Website Case Study Draft

# Office Waste Collection

### Hero Description

Office Waste Collection is a zero-waste training exercise inside an office. Trainees find discarded items, choose the bin each one belongs to, and see at once whether that choice was right. The session ends with a classification report that includes both the items they sorted and the ones they left behind.

### The Challenge

Office waste looks simple until the exceptions show up. A bottle cap is not the bottle. A juice carton is not paper. A light bulb and a toner cartridge are electronic waste. A face mask is medical waste. A wet wipe is domestic waste. People need practice recognizing those differences where the objects actually sit, not only on a poster of colored bins.

This module turns that recognition into a repeated decision. Every pickup asks for a bin. Every answer is checked.

### The Experience

The trainee signs in, chooses Turkish or English, and enters the office. They look at an item and confirm it. The item is taken, and a bin menu opens with the item’s name. They choose paper and cardboard, plastic, glass, biodegradable, battery, electronic waste, a bottle-cap bin, metal, recyclable, domestic, medical, or composite.

The response is immediate. Correct and incorrect have their own sound and status. Many items also explain the classification in the selected language. A counter tracks how many items have been collected. On a desktop they use the mouse. In VR they aim with the controller, and the menus sit in the room.

### How It Works

The trainee collects and sorts at their own pace. Nothing auto-finishes the room. When they are done, they confirm the end of the session. The Waste Classification Evaluation Report lists each sorted item, the bin they chose, the correct bin, and whether that row was correct. Anything still in the office is marked not found. They can restart, quit a standalone session, or return to the Hub.

### Key Features

- Office items mapped to specific waste streams, including caps, batteries, electronics, cartons, and medical waste
- Two-step interaction: pick up the item, then choose the bin
- Bilingual names, bin labels, and spoken explanations
- Live collection counter
- Report of correct, incorrect, and uncollected items
- Desktop and VR play, with Hub launch and Hub return

### Technology

Built in Unity with the Universal Render Pipeline, OpenXR, and the XR Interaction Toolkit. The module id is `office-waste`. A boot scene loads the waste login, and a Hub launch continues into a waste office scene. Sorting rules live in a catalog, so each object carries a definition rather than its own scoring script. VR uses a controller ray and world-space UI. Meta Quest device targets are enabled in the project’s OpenXR Android settings.

### Integration

Office Waste Collection is a separate Hub module from hazard hunt and fire training. The Hub starts it and can take the trainee back from the result screen. The name and user id on the report are what the trainee types at the waste login.

Results are shown in the session and written to a local file. The device leaderboard stores a success percentage of the items that were classified. From a Hub launch, the trainee can return to the Hub.

### Outcome

The module gives people a way to practice office waste sorting on the objects themselves, with an immediate explanation and a record of every choice. Missed items stay on the report, so the result describes both the sorting and the search.

---

# Evidence Appendix

Internal only. Not for the public website. Do not copy network addresses from `SessionManager` into public pages.

| Claim | Evidence | File / Scene |
|---|---|---|
| Module id is `office-waste` | Bootstrap accepts only that id | `Assets/Project/WasteRuntime/Bootstrap/WasteModuleBootstrap.cs` |
| Hub loads `WasteLogin`, then Hub office load uses `Waste_Office` | `Initialize` loads login; login chooses `OfficeSceneGroup` when `WasteHubLaunch.IsHubLaunch` | `WasteModuleBootstrap.cs`, `WasteLoginScreenController.cs` |
| Standalone login loads `WasteCollector`, which is `FireModule_Office` | `TargetSceneGroup` and SceneLoader group | `WasteLoginScreenController.cs`, `Assets/Project/Reflex/Resources_moved/SceneLoader.prefab` |
| Login title is Zero Waste Training Simulator | `LoginTitleSub` / `LoginTitleMain` | `Assets/Project/WasteRuntime/Login/WasteCollectionLocalization.cs` |
| Name and user id are required | `OnStartClicked` | `WasteLoginScreenController.cs` |
| Hub does not copy Hub account into the waste login | Class comment on `WasteHubLaunch` | `WasteModuleBootstrap.cs` |
| Pickup outlines, scales away, and raises collected | `WasteController` | `Assets/Project/WasteRuntime/Core/WasteController.cs` |
| Pickup opens the bin menu and freezes the player | `WasteCollectionResultController.OnWasteCollected` | `Assets/Project/WasteRuntime/Core/WasteCollectionResultController.cs` |
| Menu bins and prompt copy | `wasteBins` list; localization strings | `WasteSelectionMenu.cs`, `WasteCollectionLocalization.cs` |
| Correct bin by item name, then by type | `WasteNameToBinId`, `GetCorrectBinId` | `Assets/Project/WasteRuntime/UI/WasteBinCatalog.cs` |
| Display names TR/EN | `WasteNameCatalog` | `Assets/Project/WasteRuntime/Data/WasteNameCatalog.cs` |
| Explanation popup duration 2.5s and does not wait for voice | `WasteExplanationPopup` | `Assets/Project/WasteRuntime/UI/WasteExplanationPopup.cs` |
| Correct/incorrect and explanation audio | `WasteAudioFeedback` | `Assets/Project/WasteRuntime/Audio/WasteAudioFeedback.cs` |
| Counter is collected/total from scene objects | `WasteCollectionCounterUI` | `Assets/Project/WasteRuntime/UI/WasteCollectionCounterUI.cs` |
| VR ray from the right controller, 12 m, visible line | `SelectionVrInteractionRay` | `Assets/Project/WasteRuntime/Selection/SelectionVrInteractionRay.cs` |
| Desktop selection is a mouse ray and click | `SelectionSystemController`, `SelectionInputController` | `Assets/Project/WasteRuntime/Selection/SelectionSystemController.cs` |
| Finish is Tab on PC and right grip in VR; grip ignored while the menu is open | `WasteVrExitInput` | `Assets/Project/WasteRuntime/VR/WasteVrExitInput.cs` |
| Exit and report copy | `ExitTitle`, `ResultTitle`, table headers, not-found status | `WasteCollectionLocalization.cs` |
| Uncollected items become not-found rows | `GetUncollectedSceneWastes`, `CreateNotFoundTableRow` | `WasteCollectTracker.cs`, `WasteResultScreenController.cs` |
| Success percent is correct/classified | `RecordLeaderboardScore` | `WasteResultScreenController.cs` |
| Local CSV folder and file name | `WasteSessionResultCsvExporter` | `Assets/Project/WasteRuntime/Persistence/WasteSessionResultCsvExporter.cs` |
| Companion upload skipped for Hub PC | `ShouldUploadToPcServer` | `WasteSessionResultCsvExporter.cs` |
| Hub return | `RequestModuleExit` | `WasteResultScreenController.cs` |
| `Waste_Office` contains tracker, UI, and VR bootstrap | Scene object names | `Assets/Project/Scenes/WasteModule/Waste_Office.unity` |
| Fire office auto-start is enabled | `autoStartOnPlay: 1` | `Assets/Project/Scenes/FireModule/FireModule_Office.unity` |
| VR vs PC follows porting asset | `WasteCollectionPlatform` | `Assets/Project/WasteRuntime/Login/WasteCollectionPlatform.cs` |
| Unity and XR stack | Shared project settings | `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json` |
