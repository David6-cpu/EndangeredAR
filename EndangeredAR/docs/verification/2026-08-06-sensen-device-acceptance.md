# Sensen iPhone Device Acceptance

- Date: 2026-08-06
- Branch: `feature/multi-animal-foundation`
- Unity: 2022.3.62f3c1
- Xcode: 27.0
- Target: iPhone 17 Pro Max, iOS 27.0 Beta, portrait

## Automated Baseline

- [x] Python backend: 4 passed, 0 failed.
- [x] EditMode: 83 passed, 0 failed.
- [x] PlayMode: 10 passed, 0 failed.
- [x] Unity generated the iOS Xcode project successfully.
- [x] The Unity post-build step added the legacy `UIScene` lifecycle backport automatically.
- [x] Xcode compiled and signed the generated device project successfully.
- [x] `Info.plist` contains camera and local-network usage descriptions.
- [x] `Info.plist` contains the `UnityScene` scene configuration.
- [x] The generated iOS project supports portrait orientation only.
- [x] The minimum iOS deployment target is 15.0.
- [x] The final build compiles and includes `glTF/PbrMetallicRoughness`.
- [x] The local proxy responds on `127.0.0.1:8000` and `192.168.2.147:8000`.
- [x] A proxy chat request returned a character-specific Moonshot response without exposing the provider key to Unity.

## Build Notes

- Xcode 27 rejects deployment targets below iOS 15.0. The Unity project was updated from 14.0 to 15.0.
- Unity 2022.3.62f3c1 predates Unity's iOS `UIScene` support and originally crashed at launch under the iOS 27 SDK. The tracked post-build step now backports the required native lifecycle glue on this editor version.
- The generated App Store icon set is missing the 1024x1024 marketing icon. This does not block a development-device build, but must be fixed before distribution.
- Unity 2022 emits deprecation warnings against the Xcode 27 SDK. The signed native build still completes successfully.

## Device Setup

- [x] Target iPhone connected, unlocked, and trusted.
- [x] Apple developer account signed in.
- [x] Automatic signing provisioned `com.yuanweijie.endangeredar`.
- [x] App installed on device through `devicectl`.

## Manual Device Checklist

- [x] App installs and opens without the previous `UIScene` launch crash.
- [x] Startup console reaches `sceneDidBecomeActive`, Metal initialization, and first-scene load without a red runtime error.
- [x] Top content avoids the Dynamic Island and bottom navigation avoids the Home Indicator.
- [x] Learning, Scan, and Profile navigation remains visible; Scan click output was observed in the device console.
- [ ] Camera permission appears once and the preview fills its content area without stretching.
- [ ] Camera preview and scan instructions remain upright in portrait.
- [x] Manual recognition enters the Sensen interaction page.
- [x] The Sensen model appears after recognition.
- [ ] Sensen loads with the expected material and placement.
- [x] The user confirmed model interaction, including the current rotation/zoom behavior, works on device.
- [ ] The model remains above the chat input and does not cover primary actions.
- [ ] Chat returns a backend response or a local fallback without blocking the page.
- [ ] The food mission completes once and awards points once.
- [ ] The knowledge card opens and PNG save creates a non-empty file.
- [ ] Relaunch preserves Sensen unlock, progress, and valid conversation history.

## Defects Found On Device

- [x] **Device verified:** the rejected `bg-home-forest.png` asset is no longer applied to the home panel, and the oversized bars and circles are absent on the iPhone home screen.
- [x] **Device verified:** runtime rounded textures now calculate their right and bottom corners from the correct mirrored centers. All three bottom navigation buttons render as complete, symmetric pills on iPhone.
- [x] **Automated and build verified:** camera preview orientation keeps the camera texture's original aspect ratio for 0, 90, and 270 degree rotations.
- [x] **Automated verified:** a persisted mission reward enters as a replay and does not display fresh reward copy before an answer.
- [x] **Automated verified:** the share-card capture surface excludes Save and Back controls and contains the character, summary, badge, and action sections.
- [x] **Device startup verified:** the final package starts without the previous missing glTF PBR shader error.
- [ ] Complete touch interaction checks for navigation, recognition, model gestures, chat, mission, card saving, and persistence.

## Result

Status: **In progress - the final signed package is installed; automated regressions and startup verification pass. Camera aspect, device chat, mission-answer timing, and the redesigned card still require the final touch workflow check.**
