# R1 Hybrid AI Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add selectable local/cloud AI providers and a Python llama.cpp adapter while preserving the current cloud proxy, Unity fallback, chat UI, and animal-switch isolation.

**Architecture:** `DemoAppController` delegates one unified request to `AIManager`; `AIRouter` tries injected local/cloud/knowledge providers according to `AIConfig`. Python keeps `/chat` compatible and adds an explicit-failure `/chat/local` adapter for an OpenAI-compatible llama.cpp server.

**Tech Stack:** Unity 2022.3 C#, Unity coroutines and `UnityWebRequest`, ScriptableObject configuration, Python 3 standard library HTTP server, `unittest`, NUnit EditMode tests.

**Spec:** `EndangeredAR/docs/superpowers/specs/2026-08-20-hybrid-local-cloud-ai-service-design.md`

## Global Constraints

- Do not modify AR, model, catalog, profile, mission behavior, package dependencies, or visual layout.
- Keep `/chat`, `ChatApiClient`, `ChatRequestState`, and `LocalKnowledgeChatService` backward compatible.
- Keep cloud credentials and provider authorization out of Unity.
- Default routing mode is `CloudOnly`.
- Default local timeout is 8 seconds; default total route budget is 38 seconds; visible UI timeout remains 40 seconds.
- `/chat/local` never calls cloud and never substitutes Python rule fallback.
- Do not implement RAG, embeddings, streaming, native inference, animation actions, emotion execution, or task mutation.
- Use test-first RED/GREEN cycles and commit each independently testable task.

---

### Task 1: Python llama.cpp Adapter

**Files:**
- Modify: `server/dev_server.py`
- Modify: `server/tests/test_dev_server.py`
- Modify: `server/.env.example`

**Interfaces:**
- Produces: `POST /chat/local` with the existing `{animalId,message,history}` request shape.
- Produces: additive `source` and `routeReason` fields on `/chat` and `/chat/local` success responses.
- Consumes: `LOCAL_LLM_BASE_URL`, `LOCAL_LLM_MODEL`, and `LOCAL_LLM_TIMEOUT`.

- [ ] **Step 1: Write failing Python tests**

Add tests that patch environment and `urllib.request.urlopen` to assert:

```python
def test_local_payload_uses_configured_model_and_existing_prompt(self): ...
def test_call_local_llm_parses_openai_compatible_reply(self): ...
def test_call_local_llm_reports_timeout(self): ...
def test_local_chat_route_returns_503_when_not_configured(self): ...
def test_cloud_chat_response_identifies_server_rule_fallback(self): ...
```

Use an in-memory handler invocation helper for route status/body tests; do not open a real port.

- [ ] **Step 2: Run the tests and verify RED**

Run:

```bash
python3 -m unittest discover -s server/tests -v
```

Expected: new tests fail because local adapter functions and route behavior do not exist.

- [ ] **Step 3: Implement the minimal adapter**

Add a provider result type or equivalent stable tuple, environment parsing, OpenAI-compatible request construction, local response parsing, and `/chat/local` dispatch. Keep `call_moonshot` and `/chat` behavior compatible; only add source metadata.

- [ ] **Step 4: Run Python tests and verify GREEN**

Run the same `unittest` command. Expected: all old and new tests pass.

- [ ] **Step 5: Commit**

```bash
git add server/dev_server.py server/tests/test_dev_server.py server/.env.example
git commit -m "feat: add local llama cpp chat adapter"
```

### Task 2: Unity AI Contracts and Deterministic Router

**Files:**
- Create: `EndangeredAR/Assets/Scripts/AI/AIContracts.cs`
- Create: `EndangeredAR/Assets/Scripts/AI/AIRouter.cs`
- Create: matching Unity `.meta` files
- Create: `EndangeredAR/Assets/Tests/EditMode/AIRouterTests.cs`
- Create: matching test `.meta` file

**Interfaces:**
- Produces: `AIRouteMode`, `AIRequest`, `AIResponse`, `AIProviderError`, `IAIProvider` exactly as defined in the spec.
- Produces: `AIRouter.Route(AIRequest request, AIRouteMode mode, float localTimeoutSeconds, float totalTimeoutSeconds, Action<AIResponse> onSuccess, Action<AIProviderError> onError)`.
- Consumes: injected `IAIProvider local`, `cloud`, and `knowledge` providers plus an injected realtime clock for deterministic budget tests.

- [ ] **Step 1: Write failing router tests**

Create fake providers that record call count and supplied timeout. Cover CloudOnly, LocalOnly, local-first success, local-to-cloud fallback, both HTTP providers to knowledge fallback, response source/reason, and remaining-budget behavior.

- [ ] **Step 2: Run targeted Unity tests and verify RED**

Run Unity EditMode with `-testFilter EndangeredAR.Tests.EditMode.AIRouterTests`. Expected: compilation/test failure because contracts and router do not exist.

- [ ] **Step 3: Implement contracts and router**

Implement only route sequencing, response normalization, reason composition, elapsed-time subtraction, and final error delivery. Do not add HTTP, UI, RAG, task, or animation logic.

- [ ] **Step 4: Run targeted and full Unity tests**

Run targeted tests, then all EditMode tests. Expected: all pass and original 83 remain green.

- [ ] **Step 5: Commit**

```bash
git add EndangeredAR/Assets/Scripts/AI EndangeredAR/Assets/Tests/EditMode/AIRouterTests.cs EndangeredAR/Assets/Tests/EditMode/AIRouterTests.cs.meta
git commit -m "feat: add deterministic AI provider router"
```

### Task 3: Unity Providers, Configuration, and Composition Root

**Files:**
- Create: `EndangeredAR/Assets/Scripts/AI/AIConfig.cs`
- Create: `EndangeredAR/Assets/Scripts/AI/AIManager.cs`
- Create: `EndangeredAR/Assets/Scripts/AI/CloudLLMProvider.cs`
- Create: `EndangeredAR/Assets/Scripts/AI/LocalLLMProvider.cs`
- Create: `EndangeredAR/Assets/Scripts/AI/LocalKnowledgeProvider.cs`
- Create: matching Unity `.meta` files
- Modify: `EndangeredAR/Assets/Scripts/API/ChatApiClient.cs`
- Create: `EndangeredAR/Assets/Tests/EditMode/AIProviderTests.cs`
- Create: matching test `.meta` file
- Modify: `EndangeredAR/Assets/Tests/EditMode/ApiSecurityTests.cs`

**Interfaces:**
- Consumes: Task 2 contracts and router.
- Produces: `AIManager.Send(AIRequest request, Action<AIResponse> onSuccess, Action<AIProviderError> onError)` coroutine.
- Produces: `ChatApiClient.SendMessage(..., float timeoutSeconds, ...)` while retaining existing overloads.
- Produces: `AIConfig` defaults `CloudOnly`, `http://127.0.0.1:8000`, 8 seconds local, 38 seconds total.

- [ ] **Step 1: Write failing provider/config/security tests**

Assert default configuration, cloud adapter reuse, LocalOnly missing URL behavior, local response JSON mapping, knowledge mapping, timeout propagation, and absence of cloud keys/direct Moonshot endpoints in Unity.

- [ ] **Step 2: Run targeted Unity tests and verify RED**

Run `AIProviderTests` and `ApiSecurityTests`. Expected: failures for missing types and overloads.

- [ ] **Step 3: Implement minimal providers and AIManager**

Cloud delegates to `ChatApiClient`; local posts to `/chat/local`; knowledge calls the existing service; manager constructs and runs the router. Invalid or missing config must not throw and must retain a knowledge fallback path.

- [ ] **Step 4: Run targeted and full Unity tests**

Expected: targeted tests and all EditMode tests pass.

- [ ] **Step 5: Commit**

```bash
git add EndangeredAR/Assets/Scripts/AI EndangeredAR/Assets/Scripts/API/ChatApiClient.cs EndangeredAR/Assets/Tests/EditMode/AIProviderTests.cs EndangeredAR/Assets/Tests/EditMode/AIProviderTests.cs.meta EndangeredAR/Assets/Tests/EditMode/ApiSecurityTests.cs
git commit -m "feat: add Unity local and cloud AI providers"
```

### Task 4: Minimal Demo Integration and Safe Scene Migration

**Files:**
- Modify: `EndangeredAR/Assets/Scripts/UI/DemoAppController.cs`
- Modify: `EndangeredAR/Assets/Editor/AnimalArchitectureSceneMigrator.cs`
- Create: `EndangeredAR/Assets/Config/LocalAIConfig.asset`
- Create: `EndangeredAR/Assets/Config/LocalAIConfig.asset.meta`
- Modify: `EndangeredAR/Assets/Scenes/DemoScene.unity` through the migrator only
- Modify: `EndangeredAR/Assets/Tests/EditMode/DemoAnimalMigrationTests.cs`

**Interfaces:**
- Consumes: `AIManager.Send` and unified `AIResponse`.
- Preserves: `ChatRequestState`, 40-second UI guard, transcript/history persistence, thinking line, and model feedback.
- Produces: serialized `DemoAppController.aiManager` reference and one scene `AIManager` configured with current `ChatApiClient` and `LocalKnowledgeChatService`.

- [ ] **Step 1: Write failing integration and scene-wiring tests**

Assert exactly one `AIManager`, non-null config/provider references, Demo controller reference, default `CloudOnly`, and unchanged RectTransform/Canvas baselines. Retain the existing animal-switch test.

- [ ] **Step 2: Run targeted tests and verify RED**

Expected: missing scene AI manager/reference failures.

- [ ] **Step 3: Make the minimal controller change**

Replace only the direct `ChatApiClient.SendMessage` decision inside `AskLocal` with an `AIRequest` and `AIManager.Send`. Rename completion helpers only where necessary to accept `AIResponse`; preserve ticket validation and all UI behavior. Keep a final direct `BuildFallbackReply` guard if `AIManager` itself is absent.

- [ ] **Step 4: Extend and run the safe migrator**

Extend `AnimalArchitectureSceneMigrator`, then execute:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/yuanweijie/Documents/animalsAR/EndangeredAR \
  -executeMethod EndangeredAR.Editor.AnimalArchitectureSceneMigrator.MigrateDemoScene \
  -logFile /private/tmp/r1-ai-scene-migration.log \
  -quit
```

Do not execute `BuildDemoScene`.

- [ ] **Step 5: Run targeted and full Unity tests**

Expected: scene tests pass, stale animal responses remain rejected, and full suite passes.

- [ ] **Step 6: Commit**

```bash
git add EndangeredAR/Assets/Scripts/UI/DemoAppController.cs EndangeredAR/Assets/Editor/AnimalArchitectureSceneMigrator.cs EndangeredAR/Assets/Config/LocalAIConfig.asset EndangeredAR/Assets/Config/LocalAIConfig.asset.meta EndangeredAR/Assets/Scenes/DemoScene.unity EndangeredAR/Assets/Tests/EditMode/DemoAnimalMigrationTests.cs
git commit -m "feat: route demo chat through AI manager"
```

### Task 5: Operating Documentation and Final Verification

**Files:**
- Modify: `server/README.md`
- Modify: `README.md`
- Create: `EndangeredAR/docs/verification/2026-08-20-r1-hybrid-ai-routing.md`
- Create: matching documentation `.meta` only if Unity imports the verification file under `Assets` (not expected here)

**Interfaces:**
- Documents: llama.cpp compatible server startup, Python proxy startup, three route modes, Editor localhost versus phone LAN configuration, and four manual cases.

- [ ] **Step 1: Document exact startup and configuration commands**

Use a llama.cpp server command shaped as:

```bash
llama-server -m /absolute/path/to/model.gguf --host 127.0.0.1 --port 8080
```

Document `.env.local`, Python proxy launch, `LocalAIConfig.asset`, and `LocalApiConfig.asset`. Do not claim a model is bundled.

- [ ] **Step 2: Run fresh automated verification**

Run:

```bash
python3 -m unittest discover -s server/tests -v
/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath /Users/yuanweijie/Documents/animalsAR/EndangeredAR \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/r1-final-editmode.xml \
  -logFile /private/tmp/r1-final-unity.log
```

Record exact totals and failures.

- [ ] **Step 3: Run manual HTTP route checks**

Start the proxy with isolated test environment values and use `curl` to exercise `/chat/local` configured/unconfigured behavior and unchanged `/chat` rule behavior. If no local GGUF/llama.cpp server exists, mark local-model Case A/D as blocked and do not fabricate results.

- [ ] **Step 4: Perform scope and secret scans**

Run:

```bash
git diff feature/multi-animal-foundation...HEAD -- EndangeredAR/Assets/Scripts/AR EndangeredAR/Assets/Scripts/Models EndangeredAR/Assets/Scripts/Missions EndangeredAR/Assets/Scripts/Progress EndangeredAR/Packages
rg -n "MOONSHOT_API_KEY|Authorization|Bearer|api.moonshot.cn" EndangeredAR/Assets/Scripts EndangeredAR/Assets/Config
```

Expected: no unrelated diffs and no cloud credentials/provider endpoint in Unity.

- [ ] **Step 5: Commit documentation and evidence**

```bash
git add README.md server/README.md EndangeredAR/docs/verification/2026-08-20-r1-hybrid-ai-routing.md EndangeredAR/docs/superpowers/specs/2026-08-20-hybrid-local-cloud-ai-service-design.md EndangeredAR/docs/superpowers/plans/2026-08-20-r1-hybrid-ai-routing-implementation-plan.md
git commit -m "docs: record R1 hybrid AI routing"
```

- [ ] **Step 6: Request final whole-branch review**

Review the complete branch against the spec, resolve load-bearing findings, rerun affected tests, and stop before any R2 work.
