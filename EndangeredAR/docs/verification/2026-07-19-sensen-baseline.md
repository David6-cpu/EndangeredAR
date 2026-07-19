# Sensen Stable Baseline

- Unity: 2022.3.62f3c1
- Scene: Assets/Scenes/DemoScene.unity
- Baseline date: 2026-07-19

## Automated

- [x] Batch-mode compilation completes without C# errors.
- [x] Project opens without missing package errors.

Clean-worktree verification command:

```text
/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath /Users/yuanweijie/Documents/animalsAR/EndangeredAR -logFile /private/tmp/endangered-ar-clean-baseline-retry.log
```

Result: exit code `0`; log contains `Exiting batchmode successfully now!` and no C# compiler or package-resolution errors.

## Manual Regression

- [ ] App opens without red Console errors.
- [ ] Learning, Scan, and Profile buttons remain clickable.
- [ ] Camera scan page opens and manual recognition enters the model page.
- [ ] Sensen GLB loads with texture and remains rotatable and zoomable.
- [ ] Chat returns a backend response or local fallback without blocking the page.
- [ ] Food mission completes once and awards points once.
- [ ] Knowledge card opens and PNG save produces a non-empty file.

## Post-Migration

- [ ] First scan unlocks Sensen and a relaunch preserves the unlock.
- [ ] Recent Sensen conversation restores without transient thinking or error text.
- [ ] EditMode and PlayMode test reports have zero failures.
