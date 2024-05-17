# Udon Vehicle Synchronization

[![Total downloads](https://img.shields.io/github/downloads/Guribo/UdonVehicleSync/total?style=flat-square&logo=appveyor)](https://github.com/Guribo/UdonVehicleSync/releases)

A predictive, smooth synchronization implementation for non-kinematic RigidBodies.

**Highly experimental and to be considered unstable as it is very much subject to change!**

![Preview](.Readme/UVS_Preview.gif)

## Features

1. Automatic respawn if below certain height
2. Freely adjustable send rate, smoothing and responsiveness
3. **VERY** accurate timing across different network and player conditions
   1. Positional error at supersonic speeds is almost negligible
   2. Time drift reduced to be almost invisible
   3. No rubber banding in un-obstructed flight 
      1. *Note that we are still dealing with prediction, so overshoot on rapid velocity changes is still present, but minimized*
4. Dynamic send rate based on current movement and its prediction
   1. *Sender is determining if and when it needs to send the next packet based on the last packet it sent*
5. Dynamically adjustable prediction amount that allows reducing prediction and going back into a replay mode
   1. **WORK IN PROGRESS** *currently not smooth at all*
   2. Good option maybe for spectators

## Future development

1. Find a way to allow e.g. midair contact between sender and receivers without Unity physics freaking out
2. Performance improvements (Udon 2?)
3. Simplify setup
4. Implement a kind of local simulation of collision to prevent overshoot and increase immersion

## Installation

1. Install VRChat World SDK 3.6
2. Install CyanPlayerObjectPool: https://cyanlaser.github.io/CyanPlayerObjectPool/
3. Install TLP UdonVehicleSync: https://guribo.github.io/TLP/

## Setup

1. Add the following prefabs **once** to your scene:
   1. `TLP_Essentials`
   2. `TLP_SyncOrigin`
2. For any RigidBody that shall be synchronized add the prefab `TLP_UdonVehicleSync_WithSettingsTweaker` to the scene (does not need to be attached to the object)
3. Unpack the prefab (**not completely!**) and drag the contained `SettingsTweaker` GameObject somewhere so that it is no longer a child to the prefab or the Rigidbody
4. Go through each object of the `TLP_UdonVehicleSync_WithSettingsTweaker` prefab and set references:
   1. `TLP_UdonVehicleSync_WithSettingsTweaker/Sender`:
      1. `NetworkTime` = `TLP_NetworkTime` prefab in the scene
   2. `TLP_UdonVehicleSync_WithSettingsTweaker/Sender/VelocityProvider`:
      1. `RelativeTo` = `TLP_SyncOrigin` (Transform of the prefab in the scene)
      2. `ToTrack` = RigidBody of the parent (object that shall be synchronized)
   3. `TLP_UdonVehicleSync_WithSettingsTweaker/Receiver`:
      1. `NetworkTime` = `TLP_NetworkTime` prefab in the scene
5. Ensure that ownership of the `Receiver` GameObject is transferred correctly when another player takes control!

# Execution order info

The `owner` reads and serializes the transform position in `PostLateUpdate()`.
Other players deserialize the data and apply it in `Update()`.

This means that ideally the `owner` will/should modify the transform position in either `FixedUpdate()`,
`Update()` or `LateUpdate()` before it is sent to other players.

## Versioning

This package is versioned using [Semantic Version](https://semver.org/).

The used pattern MAJOR.MINOR.PATCH indicates:

1. MAJOR version: incompatible API changes occurred
    - Implication: after updating backup, check and update your scenes/scripts as needed
2. MINOR version: new functionality has been added in a backward compatible manner
    - Implication: after updating check and update your usages if needed
3. PATCH version: backward compatible bug fixes were implemented
    - Implication: after updating remove potential workarounds you added

## Changelog

All notable changes to this project will be documented in this file.

### [2.1.0] - 2024-05-17

#### 🚀 Features

- *(PredictingSync)* Add OnRespawn event to listen to, add to prefab as well

#### ⚙️ Miscellaneous Tasks

- *(Prefabs)* Tweak default sender settings to be more likely to send updates when moving slowly when dynamic send rate is enabled, disable debug trails by default

### [2.0.1] - 2024-05-16

#### 🐛 Bug Fixes

- *(DemoScene)* Fix missing material on ground plane

### [2.0.0] - 2024-05-16

#### 🚀 Features

- Fix prediction reduction slider not working in client sim
- Prevent datalist access to invalid indices
- [**breaking**] Major overhaul of all prediction and sync logic

#### 🚜 Refactor

- Cleanup

#### ⚙️ Miscellaneous Tasks

- Add release pipeline

### [1.1.0] - 2023-11-08

#### 🚀 Features

- Add prediction reduction

### [1.0.0] - 2023-11-03

#### 🚀 Features

- Update exporter
- Update Readme

### [0.0.0] - 2023-10-03

#### 🚀 Features

- Initial commit
- Add vehicle sync, update leader board (break it too)
- Break more stuff
- Initial (failed) version of better-tracking pickups
- Jitterfree pickups
- Add gamemode, update vr components, test improvements, add serialization retry to base behaviour
- Add logging of all logs in frame to profiler
- Move to TLP namespace
- Update base behaviour
- Fix up scenes and broken event callbacks
- Display data in leaderboard entry
- Update to support changes in Mathfs
- Reduce type spam in logs, add execution order to logs
- Add comparer creation, update exectionorders, move pooleable code to base behaviour
- Update UVU exporter and readme
- Add usage of new velocity provider and NetworkTime
- Create a script that spawn objects at a given location at a fixed server time
- Create helper scripts that enforce master ownership and non-master ownership
- Finally close to 100% smooth and accurate without any position smoooothing <3
- Perfectly smooth (like 99.999%)
- Compensate for different frame rates (not completely, but good enough)
- Truly smoother
- Improve time sync (remaining issue is related to fixed update duration?)
- As smooth as possible I think
- Extract accurate timing code into a parent class, add smooth error correction, add teleportation
- Rotation smoothing
- Add ingame tweaking menu
- Add prefabs and save changes
- Turn linear acceleration into a turn for improved turning
- Uploaded
- Create exporter
- Add missing assets to exporter
- Create demo world with multiple cars, add fixed update with kinematic move, remove dead code
- Update assets
- Add sacc sync adapter
- Update
- Make 1 car 4WD
- Update assembly definitions after vrc sdk updates

#### 🚜 Refactor

- Update namespaces
- Cleanup and more test coverage
- Use Tlpbasebehaviour
- Create testcase
- Update namespaces

#### 🧪 Testing

- Ensure logasserts are on by default
- Test calculating sender game time and game time difference
- Add more cases
- Develop sender time estimation algorithm that changes the time on average at most 0.333ms
- Move code to testpuppet

#### ⚙️ Miscellaneous Tasks

- Add issue template

<!-- generated by git-cliff -->
