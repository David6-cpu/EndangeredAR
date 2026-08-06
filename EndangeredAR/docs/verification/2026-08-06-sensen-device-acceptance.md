# Sensen iPhone Device Acceptance

- Date: 2026-08-06
- Branch: `feature/multi-animal-foundation`
- Unity: 2022.3.62f3c1
- Xcode: 27.0
- Target: iPhone 17 Pro Max, portrait

## Automated Baseline

- [x] EditMode: 75 passed, 0 failed.
- [x] PlayMode: 5 passed, 0 failed.
- [x] Unity generated the iOS Xcode project successfully.
- [x] Xcode compiled the device project with code signing disabled.
- [x] `Info.plist` contains camera and local-network usage descriptions.
- [x] The generated iOS project supports portrait orientation only.
- [x] The minimum iOS deployment target is 15.0.

## Build Notes

- Xcode 27 rejects deployment targets below iOS 15.0. The Unity project was updated from 14.0 to 15.0.
- The generated App Store icon set is missing the 1024x1024 marketing icon. This does not block a development-device build, but must be fixed before distribution.
- Unity 2022 emits deprecation warnings against the Xcode 27 SDK. The unsigned native build still completes successfully.

## Current Device Blockers

- [ ] Connect and unlock the target iPhone. Xcode currently reports the known phones as offline.
- [ ] Trust this Mac on the iPhone if prompted.
- [ ] Sign in to the Apple developer account in Xcode Settings > Accounts.
- [ ] Select a development team for the `Unity-iPhone` target and allow Xcode to create a provisioning profile for `com.yuanweijie.endangeredar`.

## Manual Device Checklist

- [ ] App installs and opens without a crash.
- [ ] No red runtime errors appear in the Xcode device console.
- [ ] Top content avoids the Dynamic Island and bottom navigation avoids the Home Indicator.
- [ ] Learning, Scan, and Profile navigation remains visible and clickable.
- [ ] Camera permission appears once and the preview fills its content area without stretching.
- [ ] Camera preview and scan instructions remain upright in portrait.
- [ ] Manual recognition enters the Sensen interaction page.
- [ ] Sensen loads with the expected material and placement.
- [ ] One-finger rotation and two-finger pinch zoom both work.
- [ ] The model remains above the chat input and does not cover primary actions.
- [ ] Chat returns a backend response or a local fallback without blocking the page.
- [ ] The food mission completes once and awards points once.
- [ ] The knowledge card opens and PNG save creates a non-empty file.
- [ ] Relaunch preserves Sensen unlock, progress, and valid conversation history.

## Result

Status: **In progress - automated and native-build gates passed; signed device run pending.**
