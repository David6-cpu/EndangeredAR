# Task 3 Report: Unity Providers, Configuration, and Composition Root

## Status

DONE

## Files

- `EndangeredAR/Assets/Scripts/AI/AIConfig.cs`
- `EndangeredAR/Assets/Scripts/AI/AIManager.cs`
- `EndangeredAR/Assets/Scripts/AI/CloudLLMProvider.cs`
- `EndangeredAR/Assets/Scripts/AI/LocalLLMProvider.cs`
- `EndangeredAR/Assets/Scripts/AI/LocalKnowledgeProvider.cs`
- Matching Unity `.meta` files for all new AI scripts.
- `EndangeredAR/Assets/Scripts/API/ChatApiClient.cs`
- `EndangeredAR/Assets/Tests/EditMode/AIProviderTests.cs`
- `EndangeredAR/Assets/Tests/EditMode/AIProviderTests.cs.meta`
- `EndangeredAR/Assets/Tests/EditMode/ApiSecurityTests.cs`

## RED Evidence

Command:

```bash
"/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/yuanweijie/Documents/animalsAR/EndangeredAR" \
  -runTests -testPlatform EditMode \
  -testFilter EndangeredAR.Tests.EditMode.AIProviderTests \
  -testResults /private/tmp/task-3-red-aiprovider.xml \
  -logFile /private/tmp/task-3-red-aiprovider.log
```

Result: exit code 1. Unity stopped at compilation because `AIConfig`, `AIManager`, the three provider types, `ChatResponse.source`, `ChatResponse.routeReason`, and `ChatApiClient.ToUnityTimeoutSeconds` did not exist. No tests executed.

## GREEN Evidence

- `AIProviderTests`: `/private/tmp/task-3-final-aiprovider.xml` reports 8 total, 8 passed, 0 failed, 0 inconclusive, 0 skipped.
- `ApiSecurityTests`: `/private/tmp/task-3-green-apisecurity.xml` reports 3 total, 3 passed, 0 failed, 0 inconclusive, 0 skipped.
- Full EditMode: `/private/tmp/task-3-final-editmode-all.xml` reports 110 total, 110 passed, 0 failed, 0 inconclusive, 0 skipped.
- `git diff --check` passed.

## Implementation

- Added AI configuration defaults: `CloudOnly`, local URL `http://127.0.0.1:8000`, 8-second local budget, and 38-second total budget.
- Added cloud, local HTTP, and Unity knowledge providers. Cloud delegates exclusively to `ChatApiClient`; local calls only `/chat/local`; knowledge remains the fallback.
- `ChatApiClient` retains both existing overloads at 35 seconds and adds a timeout overload. Both HTTP clients poll `UnityWebRequestAsyncOperation` and yield only `null` while pending.
- Cloud manually advances and disposes the inner client enumerator, so router cancellation disposes the request. Providers preserve `source`; `AIRouter` continues to assign final `routeReason`.
- Added stable, port-free tests for config, timeout rounding, local configuration and JSON mapping, knowledge mapping, manager degradation, cloud adapter reuse, and Unity credential boundary checks.

## Commit

`feat: add Unity local and cloud AI providers`

## Self-Review

- Scope is limited to the approved Task 3 scripts, tests, metadata, `ChatApiClient`, and this report. No scene, UI, AR, RAG, task, or animation files changed.
- `LocalOnly` cannot invoke cloud because `AIRouter` already excludes cloud in that mode; an invalid local URL becomes `local_configuration_error` and reaches the knowledge fallback.
- No provider key, authorization header, bearer credential, Moonshot reference, or direct OpenAI-compatible endpoint was introduced into Unity.

## Concerns

No in-scope concerns. HTTP behavior is intentionally verified through URL/JSON/error/timeout helpers rather than a live local port, as required. Task 4 remains responsible for scene and `DemoAppController` integration.
