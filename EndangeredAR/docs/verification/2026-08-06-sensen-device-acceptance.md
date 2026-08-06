# Sensen iPhone Device Acceptance

- Date: 2026-08-06
- Branch: `feature/multi-animal-foundation`
- Unity: 2022.3.62f3c1
- Xcode: 27.0
- Target: iPhone 17 Pro Max, iOS 27.0 Beta, portrait

## Automated Baseline

- [x] EditMode: 76 passed, 0 failed.
- [x] PlayMode: 7 passed, 0 failed.
- [x] Unity generated the iOS Xcode project successfully.
- [x] The Unity post-build step added the legacy `UIScene` lifecycle backport automatically.
- [x] Xcode compiled and signed the generated device project successfully.
- [x] `Info.plist` contains camera and local-network usage descriptions.
- [x] `Info.plist` contains the `UnityScene` scene configuration.
- [x] The generated iOS project supports portrait orientation only.
- [x] The minimum iOS deployment target is 15.0.

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
- [ ] Top content avoids the Dynamic Island and bottom navigation avoids the Home Indicator.
- [ ] Learning, Scan, and Profile navigation remains visible and clickable.
- [ ] Camera permission appears once and the preview fills its content area without stretching.
- [ ] Camera preview and scan instructions remain upright in portrait.
- [x] Manual recognition enters the Sensen interaction page.
- [x] The Sensen model appears after recognition.
- [ ] Sensen loads with the expected material and placement.
- [ ] One-finger rotation and two-finger pinch zoom both work.
- [ ] The model remains above the chat input and does not cover primary actions.
- [ ] Chat returns a backend response or a local fallback without blocking the page.
- [ ] The food mission completes once and awards points once.
- [ ] The knowledge card opens and PNG save creates a non-empty file.
- [ ] Relaunch preserves Sensen unlock, progress, and valid conversation history.

## Defects Found On Device

- [x] **Implementation fixed, device visual pending:** the rejected `bg-home-forest.png` asset is no longer applied to the home panel. A PlayMode regression test enforces this.
- [x] **Implementation fixed, device visual pending:** rounded sprite radii are clamped to the 128px source texture, preventing overlapping slice borders. A PlayMode regression test enforces this.
- [ ] Complete touch interaction checks for navigation, recognition, model gestures, chat, mission, card saving, and persistence.

## Result

Status: **In progress - signed launch, recognition, and model appearance passed; final device visual and touch workflow acceptance remain pending.**
