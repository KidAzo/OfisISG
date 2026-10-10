# Office VR Training Platform

Internal overview for a later public “Office VR Training” page. It summarizes only what the three module case studies verified in this project. It is not a substitute for those case studies.

The individual write-ups are:

- `01_Office_Hazard_Hunt.md`
- `02_Office_Fire_Training.md`
- `03_Office_Waste_Collection.md`

---

## What the platform is

The Unity project contains three training modules that share an office setting, a Hub launch path, and a PC/VR split:

| Module | Hub id | What the trainee practices |
|---|---|---|
| Office Hazard Hunt | `office-hazard` | Finding unsafe conditions already placed in the office |
| Office Fire Training | Fire login from `Office_Boot` | Responding to one chosen fire and evacuating |
| Office Waste Collection | `office-waste` | Picking up waste and sorting it into bins |

Each module has its own login, its own session result, and its own way of ending. They are not one continuous story. A trainee enters one module, completes that exercise, and can return to the Hub.

No adoption numbers, completion rates, or customer names were found in the project. The platform value that can be stated is the capability: three practice loops in one office stack, in Turkish and English, on desktop and in VR.

---

## What they share

**Hub delivery.** Each module can be started as Addressable content. Hazard and waste each have a boot scene that implements `IModuleBootstrap`, checks a module id, and loads a login scene group. Fire uses `OfficeGameModulesBootstrapper` on `Office_Boot` and loads the fire login. All three can call `IModuleExitHandler.RequestModuleExit` to leave back to the Hub. A shared `FireServiceInstaller` registers the scene loader and input manager.

**Scene loading.** `SceneLoader` loads named scene groups through Addressables. Catalog health checks run before hazard, waste, and fire groups. Hazard’s office can also load as a built-in scene when it is in the player build.

**PC and VR from one mode switch.** `AppMode` is PC or XR. Hub launches pass a platform id; `vr` and `xr` select the VR presentation and write that into the porting assets. Android treats an active XR device as VR. The project’s OpenXR Android settings enable Meta Quest Support for Quest, Quest 2, Quest Pro, Quest 3, and Quest 3S.

**Player and input.** Desktop movement is a character controller with keyboard and mouse. VR uses the XR Interaction Toolkit XR Origin. After an Addressable load, input is re-synced because ScriptableObject events can be duplicated. Grip and trigger actions are shared input events; each module decides what they mean.

**Language.** Turkish and English are both implemented on every login and on every result screen. Hazard swaps ScriptableObject content and voice banks. Fire localizes objective and mistake labels and sign materials. Waste localizes item names, bin names, and explanation text.

**Results stay explainable.** Every module ends on a screen that lists what happened, not only a number. Hazard lists found and missed hazards. Fire lists completed objectives, missing objectives, and mistakes. Waste lists the chosen bin, the correct bin, and items left in the room. Each can write a local CSV. Each login can show a device-local score list. A shared server-side training record for all three modules was not found.

**Engine.** Unity `6000.3.11f1`, Universal Render Pipeline, Addressables, Input System, OpenXR, XR Interaction Toolkit, UI Toolkit, and the shared audio system.

---

## How they differ

**Hazard Hunt** is a search. Forty authored hazards are already in the office. The trainee confirms the ones they see. Most hazards then animate from the unsafe state to a corrected one. The score is the percentage found. The trainee decides when to stop. There is no time limit and no penalty for looking at the wrong object. The office scene is `Office`, separate from the fire office scene.

**Fire Training** is a procedure. The trainee chooses Server Room, Archive Room, or Kitchen-Cafe. Only that scenario runs. The steps include noticing smoke, using the response that room expects, crouching in smoke, holding the stair handrail, and reaching an outdoor assembly area. Water on the fire is a recorded mistake. The result passes only when required steps are complete and no mistake was recorded. Gameplay uses `FireModule_Office`, additive stair scenes, and an outdoor scene.

**Waste Collection** is a sort. The trainee picks up an item, then chooses a bin from a menu. The correct bin comes from a name catalog, with exceptions for caps, cartons, electronics, masks, and wet wipes. Feedback is immediate, often with a spoken explanation. Uncollected items are marked not found. A Hub launch uses `Waste_Office`. A standalone launch loads `FireModule_Office`, where fire auto-start is also enabled. That overlap should be confirmed before a standalone waste demo is treated as waste-only.

---

## Shared technology, in practice

| Layer | Used by |
|---|---|
| Addressables scene groups and catalog checks | All three |
| `FireServiceInstaller`, scene loader, input manager | All three |
| PC character controller and XR Origin | All three |
| OpenXR and the Quest device list | Project XR settings used by the VR path |
| UI Toolkit for login and results | All three, with uGUI still used on the hazard report |
| Local CSV export | All three, different files and folders |
| Device leaderboard | All three, different stores |
| Extinguisher, pin pull, spray | Fire |
| Evacuation splines, stair scenes, outdoor scene | Fire |
| Hazard data assets and toggle/rotation corrections | Hazard |
| Waste definitions and bin catalog | Waste |

---

## Hub integration

The Hub is the launcher and the place a module returns to. It is not, in the code reviewed here, a single grade book for all three exercises.

- Hazard stores the Hub module id and version, loads its own login, and returns from the report.
- Fire returns from the result quit button when an exit handler is registered. Otherwise that button quits the application.
- Waste returns from an explicit Hub button on a Hub launch. Its login does not import a Hub account; the trainee still types a name and id.

Hazard and waste reject a launch whose module id does not match. Fire’s bootstrapper logs the module id and loads the fire login for the Fire Training setting.

---

## Reusable systems worth naming on a platform page

These are safe to describe as platform pieces, because more than one module depends on them:

- Module boot scene plus Addressable scene groups
- PC/XR porting, with Meta Quest Support enabled in the OpenXR Android settings
- Shared rig, locomotion, and input rebind after Addressable loads
- Bilingual login and result pattern
- Local session export and device score list
- Hub enter and Hub exit

These should stay attached to one module:

- Hazard confirmation, hazard catalog, and found/missed report
- Fire scenario state machines, extinguisher and blanket tools, evacuation
- Waste pickup, bin menu, and classification table

---

## Platform value that the project supports

An organization can offer three different office drills without three different engines: recognize hazards, practice a fire response, and sort waste. Each drill can run in Turkish or English and in desktop or VR. Each one ends with a record the trainee can read. The Hub can start a module and take the trainee back when it is over.

What the project does not support as a claim: a measured improvement in safety outcomes, a named client, a user count, or one admin database that already stores all three results. Those need confirmation outside this repository.

---

## Evidence

The statements above are condensed from the three case studies. File-level evidence is in each case study’s Evidence Appendix. Do not publish credentials, companion-PC addresses, test profile values, or employee identifiers from those notes.
