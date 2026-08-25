# Sensen Stable Baseline

- Unity: 2022.3.62f3c1
- Scene: Assets/Scenes/DemoScene.unity
- Baseline date: 2026-07-19

## Automated

- [x] Batch-mode compilation completes without C# errors.
- [x] Project opens without missing package errors.
- [x] 2026-07-22 EditMode suite: 75 passed, 0 failed.
- [x] 2026-07-22 PlayMode suite: 5 passed, 0 failed.
- [x] PlayMode vertical slice verifies startup services, isolated progress storage,
  first/repeat manual Sensen scan, gesture-controller retention, and unavailable-network local fallback.
- [x] Hidden model hosts defer GLB loading until activation, avoiding startup coroutine errors.

Clean-worktree verification command:

```bash
: "${UNITY:?Set UNITY to the Unity 2022.3.62f3c1 executable}"
TEST_RESULTS_DIR="${TEST_RESULTS_DIR:-$(mktemp -d)}"
mkdir -p "$TEST_RESULTS_DIR"
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PWD/EndangeredAR" \
  -logFile "$TEST_RESULTS_DIR/endangered-ar-clean-baseline-retry.log"
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

- [x] First scan unlocks Sensen and repeat scan does not increase the unlocked count in PlayMode.
- [ ] Relaunch persistence needs a human/device verification pass.
- [ ] Recent Sensen conversation restores without transient thinking or error text.
- [x] EditMode and PlayMode test reports have zero failures.

## Headless Test Boundary

- [x] PlayMode tests set `AnimalProgressService.RepositoryPathOverrideForTests` before loading `DemoScene` and assert the active path before selection. No real app progress file is used.
- [x] The PlayMode suite temporarily substitutes a missing in-memory GLB path because the bundled glTFast decoder crashed Unity 2022.3.62f3c1 in `-nographics` mode while decoding the imported GLB. This still exercises loader fallback and automatic gesture-controller setup without changing the asset or scene on disk.
- [ ] Real GLB texture loading, pinch/rotation, placement, camera orientation/aspect, Safe Area, PNG output, and clean Console logs remain device/Game View checks.
