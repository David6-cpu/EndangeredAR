# Animals AR Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove high-confidence obsolete Animals AR files while preserving the reviewed Sensen vertical slice.

**Architecture:** Keep the current `DemoScene`-centered runtime intact. Treat `Resources.Load`, `StreamingAssets`, scene GUIDs, ScriptableObject references, and Editor menu tools as valid dependency paths; delete only assets with no static, serialized, or documented runtime dependency.

**Tech Stack:** Unity 2022.3.62f3c1, Unity UI, glTFast, C# NUnit/EditMode/PlayMode, Python unittest backend.

**Spec:** User request pasted at `/Users/yuanweijie/.codex/attachments/ac7c0545-8955-4175-b2ee-105301cd77f9/pasted-text.txt`.

## Global Constraints

- Preserve `EndangeredAR/Assets/Scenes/DemoScene.unity` and the Build Settings entry `Assets/Scenes/DemoScene.unity`.
- Preserve Sensen runtime content: `Assets/Resources/Animals/Sensen.asset`, `SensenKnowledge.asset`, `SensenMission.asset`, `Assets/StreamingAssets/Models/Sensen/sensen.glb`, and `sensen_basecolor.png`.
- Preserve `SensenGlbLoader.cs` because `DemoScene` still serializes its GUID and it derives from `AnimalModelLoader`.
- Preserve `SensenPlaceholder.mat` because `DemoScene` references it as the fallback material.
- Preserve `Assets/Resources/UI/**` because `DemoAppController.LoadUiSprite()` loads these by string path with `Resources.Load<Texture2D>($"UI/{assetKey}")`.
- Preserve XR and ProjectSettings assets even though the stable branch does not enable AR Foundation packages, because platform settings and future AR work use them.
- Do not upgrade Unity, packages, runtime architecture, or product flow.
- Do not delete anything with uncertain Inspector, `Resources`, `StreamingAssets`, Addressables, Shader, Animator, or platform dependency.

---

### Task 1: Baseline Inventory

**Files:**
- Read: `.gitignore`
- Read: `README.md`
- Read: `EndangeredAR/ProjectSettings/EditorBuildSettings.asset`
- Read: `EndangeredAR/Packages/manifest.json`
- Read: `EndangeredAR/Assets/Scenes/DemoScene.unity`
- Read: `EndangeredAR/Assets/Resources/Animals/Sensen.asset`

**Interfaces:**
- Consumes: current Git worktree and Unity project files.
- Produces: cleanup candidate list grouped as retain, delete, refactor, and uncertain.

- [x] **Step 1: Record Git baseline**

Run: `git status --short --branch && git rev-parse HEAD && git remote -v`
Expected: clean worktree on `codex/animals-ar-project-cleanup` with baseline SHA recorded.

- [x] **Step 2: Record Unity baseline**

Run: read `ProjectVersion.txt`, `EditorBuildSettings.asset`, and `manifest.json`.
Expected: Unity `2022.3.62f3c1`, one enabled build scene, and package list recorded.

- [x] **Step 3: Record size and file counts**

Run: `git ls-files | wc -l`, `find EndangeredAR/Assets -type f | wc -l`, script/scene/prefab/package counts, and `du -sh`.
Expected: pre-cleanup metrics recorded for final comparison.

- [x] **Step 4: Verify dynamic dependency paths**

Run: `rg -n "Resources\\.Load|Addressables|StreamingAssets|SceneManager|UnityWebRequest|Bearer|apiKey" EndangeredAR/Assets server content README.md`.
Expected: `Resources/Animals`, `Resources/UI`, `StreamingAssets/Models/Sensen`, server proxy configuration, and current scene load paths identified.

### Task 2: Remove Obsolete Versioned Assets

**Files:**
- Delete: `EndangeredAR/Assets/StreamingAssets/Models/Animal02/`
- Delete: `EndangeredAR/Assets/StreamingAssets/Models/Animal03.meta`
- Delete: `EndangeredAR/Assets/Art/`
- Delete: `EndangeredAR/Assets/Markers/`
- Delete: `EndangeredAR/Assets/SensenImageLibrary.asset`
- Delete: `EndangeredAR/Assets/SensenImageLibrary.asset.meta`
- Modify: `README.md`

**Interfaces:**
- Consumes: Task 1 dependency scan showing no references to these deleted assets.
- Produces: a smaller versioned Assets tree with only the shipped Sensen model and runtime-loaded UI copies.

- [x] **Step 1: Delete second-animal model candidate**

Run: `git rm -r EndangeredAR/Assets/StreamingAssets/Models/Animal02 EndangeredAR/Assets/StreamingAssets/Models/Animal03.meta`
Expected: removes the unconfigured 77 MB `animal_02.glb` and empty `Animal03` folder marker while preserving `Models/Sensen`.

- [x] **Step 2: Delete duplicate non-runtime UI source tree**

Run: `git rm -r EndangeredAR/Assets/Art`
Expected: removes byte-identical duplicates of `Assets/Resources/UI` plus the unreferenced preview image.

- [x] **Step 3: Delete duplicate marker and unused image library**

Run: `git rm -r EndangeredAR/Assets/Markers EndangeredAR/Assets/SensenImageLibrary.asset EndangeredAR/Assets/SensenImageLibrary.asset.meta`
Expected: removes non-Resources marker copy and empty XR reference image library while preserving `Assets/Resources/Markers/sensen_marker.png`.

- [x] **Step 4: Update README boundary**

Change the current boundary note from "second animal model resource is in the repository" to "second animal is not shipped in this cleaned branch".
Expected: README no longer claims the removed `Animal02` model is present.

- [x] **Step 5: Verify after deletion**

Run: `git diff --stat`, `rg -n "Animal02|animal_02|Assets/Art|SensenImageLibrary|Assets/Markers" EndangeredAR/Assets README.md`, and Python tests.
Expected: no runtime references remain; backend tests still pass.

- [x] **Step 6: Commit**

Run: `git add -A && git commit -m "chore: remove obsolete animals ar assets"`.
Expected: first cleanup commit contains only high-confidence versioned asset deletions and README adjustment.

### Task 3: Remove Local Generated Workspace Files

**Files:**
- Delete from working tree only: `.DS_Store`
- Delete from working tree only: `EndangeredAR/.DS_Store`
- Delete from working tree only: `EndangeredAR/Library/`
- Delete from working tree only: `EndangeredAR/Builds/`
- Delete from working tree only: `EndangeredAR/Logs/`
- Delete from working tree only: `EndangeredAR/UserSettings/`
- Preserve: `.env.local`
- Preserve: `.local-models/`

**Interfaces:**
- Consumes: `.gitignore` entries proving these are untracked ignored local files.
- Produces: smaller local checkout without changing Git history for ignored files.

- [x] **Step 1: Delete ignored generated Unity folders and Finder metadata**

Run: `rm -rf .DS_Store EndangeredAR/.DS_Store EndangeredAR/Library EndangeredAR/Builds EndangeredAR/Logs EndangeredAR/UserSettings`
Expected: removes local generated files only; `git status --ignored --short` still does not show unexpected tracked deletion.

- [x] **Step 2: Preserve secrets and local model cache**

Run: `git check-ignore .env.local .local-models/qwen2.5-1.5b-instruct-q4_k_m.gguf`.
Expected: both remain ignored and untracked; no secret or local GGUF enters Git.

### Task 4: Final Verification and Report

**Files:**
- Create: `EndangeredAR/docs/verification/2026-08-24-animals-ar-cleanup.md`

**Interfaces:**
- Consumes: post-cleanup metrics, test logs, and Git diff.
- Produces: final cleanup evidence and merge recommendation.

- [x] **Step 1: Run verification**

Run: Python unittest, Unity EditMode if Unity executable exists, Unity PlayMode if Unity executable exists, secret scan, missing meta scan, and final `git status`.
Expected: automated results and environment limits recorded without overstating Unity validation.

- [x] **Step 2: Write report**

Create `EndangeredAR/docs/verification/2026-08-24-animals-ar-cleanup.md` with before/after metrics, deleted files, reasons, retained modules, uncertain items, commands, and commit SHAs.
Expected: report covers the user's final reporting checklist.

- [ ] **Step 3: Commit report**

Run: `git add EndangeredAR/docs/verification/2026-08-24-animals-ar-cleanup.md && git commit -m "docs: document animals ar cleanup"`.
Expected: documentation commit is separate from asset deletion commit.
