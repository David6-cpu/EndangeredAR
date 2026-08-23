# Animals AR Cleanup Verification

Date: 2026-08-24

## Git Baseline

- Cleanup branch: `codex/animals-ar-project-cleanup`
- Pre-cleanup branch: `codex/r2-grounded-animal-knowledge`
- Pre-cleanup SHA: `9d5343aabc78c406adedc5723296d90bdf077b3f`
- Remote: `origin https://github.com/David6-cpu/EndangeredAR.git`
- Worktree state before cleanup: clean, no untracked files reported by `git status --short --branch`
- No checkpoint commit was needed before branch creation because the worktree was clean.

## Current Architecture

The Unity project root is `EndangeredAR/`. The formal startup scene is `Assets/Scenes/DemoScene.unity`, and `ProjectSettings/EditorBuildSettings.asset` contains exactly one enabled scene: `Assets/Scenes/DemoScene.unity`.

The current product chain is:

1. `DemoScene` loads `DemoAppController`, `AnimalCatalogService`, `AnimalProgressService`, `AnimalExperienceController`, `MissionController`, `AIManager`, `ChatApiClient`, `LocalKnowledgeChatService`, and `ARImageScanController`.
2. The scan page uses iOS camera preview and custom marker detection through `ARImageScanController`; the stable branch does not currently enable AR Foundation image tracking packages.
3. Successful scan/select calls flow into `AnimalExperienceController`, which resolves Sensen through `AnimalCatalogService`, unlocks progress through `AnimalProgressService`, configures `MissionController`, and configures the model host.
4. Sensen runtime assets are loaded from `Assets/Resources/Animals/Sensen.asset`, `SensenKnowledge.asset`, `SensenMission.asset`, and `Assets/StreamingAssets/Models/Sensen/sensen.glb`.
5. UI images are loaded dynamically with `Resources.Load<Texture2D>($"UI/{assetKey}")`, so `Assets/Resources/UI/**` is a runtime dependency and was not deleted.
6. AI chat calls Unity's configured Python proxy through `ChatApiClient` and preserves the server-side Moonshot boundary; local fallback uses committed Sensen knowledge.

## Cleanup Classification

### A. Explicitly Retained

- `EndangeredAR/Assets/Scenes/DemoScene.unity`
- `EndangeredAR/ProjectSettings/EditorBuildSettings.asset`
- `EndangeredAR/Assets/Resources/Animals/Sensen.asset`
- `EndangeredAR/Assets/Resources/Animals/SensenKnowledge.asset`
- `EndangeredAR/Assets/Resources/Animals/SensenMission.asset`
- `EndangeredAR/Assets/StreamingAssets/Models/Sensen/sensen.glb`
- `EndangeredAR/Assets/StreamingAssets/Models/Sensen/sensen_basecolor.png`
- `EndangeredAR/Assets/Config/SensenPlaceholder.mat`
- `EndangeredAR/Assets/Resources/UI/**`
- `EndangeredAR/Assets/Resources/Markers/sensen_marker.png`
- Runtime scripts under `EndangeredAR/Assets/Scripts/**`
- Editor tools under `EndangeredAR/Assets/Editor/**`
- XR settings under `EndangeredAR/Assets/XR/**` and `ProjectSettings/XR*.asset`
- Tests under `EndangeredAR/Assets/Tests/**`
- Python proxy and canonical content under `server/**` and `content/**`
- Formal docs under `EndangeredAR/docs/**`

### B. Deleted High-Confidence Garbage or Obsolete Assets

- `EndangeredAR/Assets/StreamingAssets/Models/Animal02/`
  - Reason: 77 MB `animal_02.glb` was not referenced by `AnimalDefinition`, scene YAML, code, tests, Addressables, Resources, or README current flow. Current MVP is Sensen-only.
- `EndangeredAR/Assets/StreamingAssets/Models/Animal03.meta`
  - Reason: empty folder marker with no files or references.
- `EndangeredAR/Assets/Art/**`
  - Reason: 43 PNGs were byte-identical duplicates of `Assets/Resources/UI/**`; runtime loads the `Resources/UI` copies by string path. The additional `ui-assets-preview.png` was not referenced by scene, code, docs, or ProjectSettings.
- `EndangeredAR/Assets/Markers/sensen_marker.png`
  - Reason: duplicate non-Resources marker copy with no GUID references. `Assets/Resources/Markers/sensen_marker.png` remains.
- `EndangeredAR/Assets/SensenImageLibrary.asset`
  - Reason: empty XR reference image library asset with no scene, ProjectSettings, or code references. Stable branch currently uses custom camera marker scanning.
- Local untracked generated files:
  - `.DS_Store`
  - `EndangeredAR/.DS_Store`
  - `EndangeredAR/Library/`
  - `EndangeredAR/Builds/`
  - `EndangeredAR/Logs/`
  - `EndangeredAR/UserSettings/`
  - Reason: all are ignored by `.gitignore`; none were tracked by Git.

### C. Refactor or Merge Candidates Not Changed

- `DemoAppController.cs` remains a large dynamic UI controller with repeated UI construction helpers. It is current runtime code and covered by PlayMode tests, so it was not refactored in this cleanup.
- `EndangeredARDemoSceneBuilder.cs` can rebuild a scene but README warns not to run it on the reviewed `DemoScene`. It remains useful for future generation and migration tests, so it was not deleted.
- `SensenGlbLoader.cs` is obsolete but preserved because `DemoScene` serializes its GUID and the class derives from `AnimalModelLoader`.
- Root `.superpowers/sdd/progress.md` was retained because it records project progress, commit history, and verification evidence.

### D. Uncertain or Deferred

- XR simulation assets report `m_Script: {fileID: 0}` in static text scanning. These are package-backed simulation settings and require Unity Editor/package import to distinguish package serialization from a real missing script.
- Addressables configuration is present but not actively used by current code. It was retained because ProjectSettings references it and package removal would be a separate dependency decision.
- AR Foundation is not enabled in current package manifest, despite the broader product direction mentioning AR Foundation. This cleanup preserves current stable behavior and does not reintroduce AR packages.
- Historical docs still mention the prior `Animal02` large asset in past verification notes. Those were preserved as historical records, while README current boundary was updated.
- Device-only checks remain manual: camera orientation/aspect, real GLB material rendering, pinch/rotation, PNG save, persistence after relaunch, and iOS Console.

## Metrics

| Metric | Before | After |
| --- | ---: | ---: |
| Git tracked files | 469 | 365 |
| `EndangeredAR/Assets` file count | 406 | 302 |
| C# script count | 51 | 51 |
| Unity scene count | 1 | 1 |
| Prefab count | 0 | 0 |
| Package manifest dependency count | 38 | 38 |
| `EndangeredAR/Assets` size | 137M | 51M |
| Workspace size | 4.3G | 1.1G |
| Unity generated `Library` size | 1.9G | removed locally |
| Unity generated `Builds` size | 1.2G | removed locally |

## Verification Commands and Results

### Baseline

- `git status --short --branch`
  - Result: clean branch before cleanup.
- `git rev-parse HEAD`
  - Result: `9d5343aabc78c406adedc5723296d90bdf077b3f`.
- `python3 -m unittest discover -s server/tests -v`
  - Result before cleanup: 58 tests passed.

### Post-Cleanup

- `rg -n "Animal02|animal_02|Assets/Art|SensenImageLibrary|Assets/Markers|second animal model resource|第二动物模型资源" EndangeredAR/Assets README.md EndangeredAR/docs || true`
  - Result: no runtime/code/README references; only this cleanup plan and historical verification docs mention deleted assets.
- `find EndangeredAR/Assets -type f -name '*.meta' ...`
  - Result: no orphan `.meta` files.
- `find EndangeredAR/Assets -type f ! -name '*.meta' ...`
  - Result: no assets missing `.meta` files.
- `find EndangeredAR/Assets -type d -empty -print`
  - Result: no empty asset directories after removing the local `Animal03/` folder.
- `python3 -m unittest discover -s server/tests -v`
  - Result after deletion: 58 tests passed.
- Secret scan excluding ignored `.env.local`, `.local-models`, Library, Builds, and `.git`
  - Result: no live secrets found. Matches were only documentation examples of earlier scan commands.
- `git check-ignore -v .env.local .local-models/qwen2.5-1.5b-instruct-q4_k_m.gguf .DS_Store`
  - Result: `.env.local`, `.local-models`, and `.DS_Store` are ignored by `.gitignore`.

### Unity Environment

- Attempted command:
  - `/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /Users/yuanweijie/Documents/animalsAR/EndangeredAR -runTests -testPlatform EditMode -testResults /tmp/endangeredar-cleanup-baseline-editmode.xml -logFile /tmp/endangeredar-cleanup-baseline-editmode.log`
- Result:
  - Failed before project load: Unity executable was not present at that path.
- Follow-up checks:
  - `find /Applications/Unity/Hub/Editor -maxdepth 4 -type f -name Unity -print`
  - `mdfind 'kMDItemFSName == "Unity" && kMDItemContentType == "public.unix-executable"'`
  - Result: no Unity executable found in this environment.

Because Unity Editor is unavailable here, C# compilation, Unity Console, EditMode, PlayMode, Build Settings import validation, missing-script UI inspection, material/Animator inspection, and iOS build validation must be performed in Unity on a machine with Unity `2022.3.62f3c1` installed.

### Independent Review

- Reviewer checked diff `9d5343aabc78c406adedc5723296d90bdf077b3f..340fbf81`.
- Result: no findings.
- Review focus: deleted asset GUID references, asset/meta pairing, Build Settings/package references, runtime `Resources/UI` and `Resources/Markers` preservation, and README consistency.
- Residual risk from review: no fresh Unity compile/import/test evidence.

## Packages

No packages were removed or upgraded. `manifest.json` remains at 38 dependencies. This avoids changing platform or package resolution behavior as part of asset cleanup.

## Git Commits

- `340fbf81 chore: remove obsolete animals ar assets`
  - Deletes high-confidence obsolete/duplicate assets and updates README current boundary.
- Documentation commit: created after this report is committed.

## Merge Recommendation

This branch is suitable for review after Unity Editor validation is run on a Unity-installed machine. Merge only after:

1. Unity `2022.3.62f3c1` opens `EndangeredAR/` without new Console errors.
2. EditMode tests pass.
3. PlayMode tests pass.
4. `DemoScene` shows no missing scripts or missing references.
5. Sensen model loads from `Assets/StreamingAssets/Models/Sensen/sensen.glb`.
6. Scan, model display, rotation/scale, chat, mission, learning center, user center, and PNG card flows are smoke-tested.
