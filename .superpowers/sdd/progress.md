# Multi-Animal Foundation Progress

## Task 1 - Stable Sensen Baseline

- Status: complete
- Base commit: `230119ed`
- Baseline commit: `b4fd8e91`
- Rollback tag: `sensen-stable-baseline`
- Branch: `feature/multi-animal-foundation`
- Verification: clean-worktree Unity 2022.3.62f3c1 import and batch compilation exited `0` with no C# or package-resolution errors.
- Security: direct LLM mode disabled; local API key field empty; repository credential scan clean.
- Review note: the first clean import lost its Unity child-process handoff at `ScriptCompilationBuildProgram`; running that Unity-supplied frontend once regenerated the DAG, and the retry completed successfully. This was a toolchain startup issue, not a source error.

## Task 2 - Runtime and EditMode Test Assemblies

- Status: complete
- Commits: `71845c67..6c26c85b`
- Verification: Unity EditMode XML reports 1 total, 1 passed, 0 failed.
- Review: approved after replacing duplicate explicit TestRunner references with `optionalUnityReferences: ["TestAssemblies"]` and clarifying that this task is a characterization-test scaffold.

## Task 3 - Animal Content Contracts

- Status: complete
- Commits: `9b9fb8e..b6a3ab3d`
- Verification: focused contract tests 10/10; full EditMode 11/11.
- Review: approved after adding edge-case coverage for case-insensitive matching, null/blank data, mission validation, and defensive copies.
- Maintenance: Unity test asset metadata normalized in `186ec665`.

## Task 4 - Validated Animal Catalog

- Status: complete
- Commits: `6f0f150..92cdcb23`
- Verification: focused catalog tests 11/11; full EditMode 22/22.
- Review: approved after adding direct coverage for incomplete content, whitespace lookup, idempotent initialization, one-time issue logging, default selection, fallback, and lazy lookup.
- Maintenance: stale Addressables linker output removed in `a7457d64` after repeated deterministic Unity deletion.

## Task 5 - Versioned Local Animal Progress

- Status: complete
- Commits: `b0275d7..ffdf2bf4`
- Verification: focused progress tests 7/7 on two consecutive runs; full EditMode 29/29.
- Review: approved after fixing explicit repository path precedence across `Awake` initialization and proving temp path A-to-B switching without real app data access.

## Task 6 - Audited Sensen Content Assets

- Status: complete
- Commits: `e63e9b3..3df22227`
- Verification: focused asset tests 3/3; full EditMode 32/32; two builder runs preserved all three asset GUIDs.
- Review: approved. Minor residual: GUID stability is verified procedurally and by `LoadOrCreate`, but not yet an automated test.

## Task 7 - Definition-Driven Mission Controller

- Status: complete
- Commits: `552b1dc..98467e7b`
- Verification: mission tests 7/7; legacy smoke 1/1; full EditMode 39/39.
- Review: approved after preserving same-mission completion and adding a bounded generic Resources fallback reachable only through obsolete wrappers until Task 11.

## Task 8 - Animal-Specific Local Fallback

- Status: complete
- Commits: `f7295b5d..5c0972ae`
- Verification: focused fallback tests 10/10; full EditMode 49/49.
- Review: approved after enforcing reachable profile content, deterministic duplicate-safe legacy selection, explicit/null profile isolation, and removing an accidentally committed task report.

## Task 9 - Generic Bundled GLB Loading

- Status: complete (automated gate); interactive scene verification carried to final Play Mode/device verification.
- Commits: `923c7827`, `3d5cb7e9`.
- Verification: required loader tests 5/5; full EditMode 54/54 from a source-identical temporary project after the original AssetDatabase/licensing IPC path was unavailable.
- Review: approved after fixing the nonblank missing-file branch so it always restores fallback renderers and adding direct load-failure plus Retry/stale-root coverage.
- Compatibility: the existing `SensenGlbLoader` GUID and serialized field names remain intact; the generic loader owns only `Animal GLB Runtime Root` and never writes the experience host Transform.
- Carried verification: after scene integration, visually confirm Sensen texture/material repair, fallback-hide timing, rotation, pinch, final placement, and inherited private-field scene deserialization in Play Mode/device build.

## Task 10 - Animal Selection and Unlock Coordination

- Status: complete.
- Commits: `059a9ba2`, `b2f865cc`.
- Verification: reconstructed missing-type RED 0/6; initial focused 6/6 and full 60/60; review regression RED 6/8; final focused 8/8 and full EditMode 62/62.
- Review: approved after making `CurrentProgress` a fresh defensive per-animal read and resetting mission ownership when different animals share the same mission ID.
- Behavior: `Prepare` never unlocks; only `SelectFromScan` unlocks; catalog selection rejects locked animals; successful selection restores the selected animal's mission/conversation state and moves only the experience host position.

## Task 11 - Migrate the Stable Sensen Demo to Animal Services

- Status: complete (automated gate); interactive Game View/device verification carried to Task 13.
- Commits: `5fec5ca2`, `284034eb`.
- Migration: `DemoScene` gained exactly three non-UI service roots; RectTransforms remain `41` and Canvas count remains `1`. A second migration run was byte-for-byte idempotent.
- Verification: initial migration fixture `10/10`, review-fix fixture `11/11`, final full EditMode `73/73` with no compiler or runtime errors in the batch logs.
- Review: approved after binding pending chat completions to animal ID/generation and changing marker resolution from substring matching to exact normalized matching.
- Behavior: startup prepares without unlocking; successful scan is the only unlock path; locked catalog selection is rejected; per-animal mission, progress, knowledge, model, and conversation state now flow through shared services.
- Carried verification: manually exercise scan, model back, all four mission choices, fallback chat, profile update, GLB texture/material, pinch/rotation, and final model placement in Game View/device testing.

## Task 12 - Backend-Only LLM Access

- Status: complete.
- Commit: `5105f578`.
- Verification: security fixture RED `0/2`, GREEN `2/2`; final full EditMode `75/75`; required repository credential/provider scan returned no matches.
- Review: approved. Remaining Minor test gaps are behavioral wire-contract coverage and automating the repository-wide provider scan.
- Security boundary: the Unity client now contains only the configured backend base URL and POST `/chat` request; direct Kimi/Moonshot paths, provider DTOs, Bearer headers, client key/model fields, serialized provider settings, and editor `.env.local` import were removed.
- Compatibility: both `SendMessage` overloads and `{animalId,message,history}` plus response DTOs remain intact; transport/configuration errors still flow into the selected animal's local knowledge fallback.

## Task 13 - PlayMode Vertical Slice Verification

- Status: automated gate complete; device/Game View checklist remains open.
- Commits: `2bb17d7a`, `90a399e1`.
- Verification: final full EditMode `75/75`; final PlayMode `5/5`; logs contain no C# compilation, missing-script, null-reference, or failed-test errors.
- Review: approved after the unavailable-network test was changed to drive the real scene input field and send-button binding through `ChatApiClient` failure into the local knowledge fallback.
- Isolation: every PlayMode test injects a unique temporary progress path before `DemoScene` loads and asserts that active path before any unlock or save.
- Runtime fix: `AnimalModelLoader` now defers a configured load while its host is inactive and resumes on `OnEnable`, preventing startup coroutine errors without changing scene hierarchy.
- Headless boundary: the tests substitute a missing in-memory GLB path so loader fallback and gesture setup are exercised without invoking glTFast on `NullGfxDevice`; the asset and scene are restored/unchanged on disk.
- Manual gate: iPhone 17 Pro Max Safe Area, camera orientation/aspect, real GLB texture/placement, pinch/rotation, mission UI, PNG output, relaunch persistence, conversation restoration, and clean device Console still require human verification.
