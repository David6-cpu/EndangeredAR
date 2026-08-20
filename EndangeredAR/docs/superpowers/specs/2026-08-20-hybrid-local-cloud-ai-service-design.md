# R1 Hybrid Local/Cloud AI Service Design

## Objective

Add a reversible provider boundary to the existing Sensen chat flow without changing AR, 3D, mission, catalog, profile, or visual behavior. R1 must support `CloudOnly`, `LocalOnly`, and `LocalFirstCloudFallback`, while preserving the current cloud proxy and Unity knowledge fallback.

## Current Baseline

- Branch baseline: `feature/multi-animal-foundation` at `8068701f`.
- Python tests: 4 passed.
- Unity EditMode tests: 83 passed.
- Current request chain:

```text
DemoAppController.AskLocal
  -> ChatRequestState.Begin
  -> ChatApiClient.SendMessage
  -> Python POST /chat
  -> Moonshot or Python make_rule_reply
  -> DemoAppController.FinishCloudAnswer
  -> LocalKnowledgeChatService only when the Unity request fails or times out
```

The existing `ChatRequestState` ticket remains the authority for whether a delayed response may update the active animal UI and conversation.

## Scope

R1 includes:

- A minimal Unity AI contract shared by all providers.
- A cloud adapter around the existing `ChatApiClient`.
- A local provider that calls the Python proxy at `/chat/local`.
- A Unity knowledge adapter around `LocalKnowledgeChatService`.
- Routing for the three requested modes.
- A single request budget with a short local attempt and cloud use of the remaining budget.
- Python adaptation from `/chat/local` to an OpenAI-compatible llama.cpp server.
- Configuration, tests, and operating documentation.

R1 explicitly excludes RAG, embeddings, vector databases, streaming, native mobile inference, JNI, llama.cpp inside Unity, fine-tuning, animation actions, emotion execution, and task mutation.

## Design Decisions

### 1. Keep cloud credentials behind Python

Unity continues to call only project-controlled Python endpoints. `ChatApiClient` remains the only cloud-proxy HTTP client and receives no provider keys, provider URLs, authorization headers, or model credentials.

### 2. Use a light Unity router

The Unity path becomes:

```text
Chat UI
  -> DemoAppController (ticket and presentation lifecycle)
  -> AIManager
  -> AIRouter
       -> LocalLLMProvider -> Python /chat/local -> llama.cpp-compatible server
       -> CloudLLMProvider -> existing ChatApiClient -> Python /chat -> Moonshot/server rule
       -> LocalKnowledgeProvider -> existing LocalKnowledgeChatService
  -> unified AIResponse
  -> existing chat transcript, persistence, and model bubble
```

`DemoAppController` retains input validation, the thinking state, `ChatRequestTicket`, transcript persistence, and stale-result rejection. It no longer chooses cloud versus local itself.

### 3. Preserve current cloud semantics

`POST /chat` remains backward compatible. When Moonshot is unavailable, it still returns the Python rule response with HTTP 200. The response gains `source` and `routeReason`, but existing clients can ignore those additive fields.

### 4. Make local failure explicit

`POST /chat/local` never uses Moonshot and never substitutes the Python rule reply. Missing configuration, connection failure, timeout, invalid JSON, or an empty model answer returns a non-2xx response with a stable error code. This distinction is required for `LocalFirstCloudFallback` to decide whether to try cloud.

### 5. Keep the Unity rule fallback inside the route

`LocalKnowledgeProvider` adapts the existing `LocalKnowledgeChatService` to `IAIProvider`. It is always the final route step and returns a unified response with `source = "unity_knowledge"`. This keeps the chat functional even when both HTTP paths are unavailable and makes source logging consistent.

## Unity Contracts

Namespace: `EndangeredAR.AI`.

```csharp
public enum AIRouteMode
{
    CloudOnly,
    LocalOnly,
    LocalFirstCloudFallback
}

[Serializable]
public sealed class AIRequest
{
    public string requestId;
    public string animalId;
    public string message;
    public ChatMessage[] history;
    [NonSerialized] public AnimalKnowledgeProfile knowledgeProfile;
}

[Serializable]
public sealed class AIResponse
{
    public string animalId;
    public string reply;
    public string source;
    public string routeReason;
    public string[] suggestedQuestions;
    public string missionHint;
    public string action;
    public string emotion;
    public string[] citations;
}

public sealed class AIProviderError
{
    public string Code { get; }
    public string Message { get; }
    public bool IsTimeout { get; }
}

public interface IAIProvider
{
    string ProviderId { get; }
    IEnumerator Send(
        AIRequest request,
        float timeoutSeconds,
        Action<AIResponse> onSuccess,
        Action<AIProviderError> onError);
}
```

The future fields are data-only in R1. No R1 code executes `action`, `emotion`, `citations`, or task-related behavior.

## Unity Components

### AIConfig

`AIConfig` is a `ScriptableObject` with:

- `routeMode`, default `CloudOnly` to preserve current behavior.
- `localServerUrl`, default `http://127.0.0.1:8000` for Editor use.
- `localTimeoutSeconds`, default 8 seconds.
- `totalTimeoutSeconds`, default 38 seconds.

The existing `ApiConfig.baseUrl` remains the cloud proxy URL. This avoids duplicating or migrating the stable cloud configuration. On a phone, `localServerUrl` must be changed to the Mac's LAN address; the design never assumes phone localhost reaches the computer.

### ChatApiClient

Keep both existing overloads. Add an overload that accepts `timeoutSeconds` and maps it to `UnityWebRequest.timeout`. Existing callers retain the current 35-second behavior.

`ChatResponse` gains additive `source` and `routeReason` fields.

### CloudLLMProvider

Wrap `ChatApiClient`; do not duplicate HTTP code. Convert `ChatResponse` to `AIResponse`, and convert client errors to `AIProviderError`.

### LocalLLMProvider

Use `UnityWebRequest` to post the same request payload to `{localServerUrl}/chat/local`. It has no cloud credentials and enforces the timeout supplied by `AIRouter`.

### LocalKnowledgeProvider

Call `LocalKnowledgeChatService.Answer(request.knowledgeProfile, request.message)`. It returns `AIResponse` and does not perform I/O.

### AIRouter

The router is a plain C# class with provider dependencies injected through its constructor, allowing route behavior to be tested without network access.

- `CloudOnly`: cloud, then Unity knowledge.
- `LocalOnly`: local, then Unity knowledge. Cloud is never invoked.
- `LocalFirstCloudFallback`: local, then cloud, then Unity knowledge.

The router sets a stable `routeReason` describing the selected mode and fallback path. It does not expose raw URLs, stack traces, or credentials.

### AIManager

`AIManager` is the MonoBehaviour composition root. It reads `AIConfig`, constructs the three providers and router, and starts the route coroutine. Missing `AIConfig`, missing `ChatApiClient`, or invalid local URL degrade to available providers and ultimately Unity knowledge rather than throwing.

## Time Budget

The visible request timeout remains 40 seconds in `DemoAppController`. `AIConfig.totalTimeoutSeconds` defaults to 38 seconds, leaving a two-second safety margin for coroutine scheduling and UI completion.

For `LocalFirstCloudFallback`:

1. Local receives `min(localTimeoutSeconds, remainingTotalBudget)`; default 8 seconds.
2. AIRouter subtracts actual elapsed realtime.
3. Cloud receives only the remaining total budget.
4. If no HTTP budget remains, AIRouter immediately uses Unity knowledge.

No provider gets a fresh full 40-second timeout after an earlier provider attempt.

## Python Adapter

New environment variables:

```text
LOCAL_LLM_BASE_URL=http://127.0.0.1:8080/v1
LOCAL_LLM_MODEL=
LOCAL_LLM_TIMEOUT=7
```

`LOCAL_LLM_BASE_URL` is required for `/chat/local`. `LOCAL_LLM_MODEL` is optional because llama.cpp deployments may not require a model alias. `LOCAL_LLM_TIMEOUT` is parsed as a bounded positive number and defaults safely when malformed.

The adapter calls `{LOCAL_LLM_BASE_URL}/chat/completions` with the existing animal system prompt and recent history. It parses the OpenAI-compatible `choices[0].message.content` response.

Response behavior:

- Success: HTTP 200, `source = "local_llm"`, `routeReason = "local_provider_succeeded"`.
- Not configured: HTTP 503, `error = "local_llm_not_configured"`.
- Timeout: HTTP 504, `error = "local_llm_timeout"`.
- Connection/provider/parse failure: HTTP 502 or 503 with a stable local error code.

The existing `/chat` adds:

- `source = "cloud_llm"` when Moonshot answers.
- `source = "server_rule"` when the existing Python fallback answers.

## Request Isolation

`AIManager` callbacks remain inside the existing `ChatRequestTicket` closure. `DemoAppController` calls `FinishAIAnswer`, which must first execute `chatRequestState.TryComplete(ticket, CurrentAnimalId)`. Switching animals invalidates the ticket exactly as before. Provider callbacks for the old animal may finish, but cannot update the new animal transcript, history, or UI.

## Scene Migration

Extend the existing safe `AnimalArchitectureSceneMigrator` rather than rebuilding `DemoScene`:

- Create or reuse one root `AI Manager` GameObject.
- Add one `AIManager` component.
- Assign `AIConfig`, `ChatApiClient`, and `LocalKnowledgeChatService`.
- Assign `DemoAppController.aiManager`.
- Preserve the RectTransform and Canvas hierarchy baselines.

Do not run `EndangeredARDemoSceneBuilder.BuildDemoScene` on the reviewed scene.

## Tests

Python tests cover local payload construction, local success, configuration absence, timeout/error mapping, and unchanged cloud-rule fallback.

Unity EditMode tests use deterministic fake providers and cover:

1. `CloudOnly` success.
2. `LocalOnly` success and zero cloud calls.
3. Local-first local success.
4. Local failure followed by cloud success.
5. Local and cloud failure followed by Unity knowledge.
6. Source and route reason values.
7. Local timeout/failure fallback without resetting the total budget.
8. Missing config degrades without throwing.
9. Existing animal-switch ticket rejection.
10. Scene references and security boundaries.

## Manual Acceptance

- Case A: llama.cpp and cloud proxy available, `LocalFirstCloudFallback`; answer source is local.
- Case B: llama.cpp stopped; same mode falls back to existing cloud proxy.
- Case C: local and cloud unavailable; Unity knowledge still answers.
- Case D: `LocalOnly`, internet unavailable but local model available; chat completes and cloud is not accessed.

If llama.cpp is not installed or no GGUF model is available on the development machine, automated adapter tests and the three fallback cases are still executed; Case A and D are reported as blocked rather than falsely claimed.

## Risks and Controls

- Phone localhost points to the phone: document Editor versus LAN configuration.
- Small local model may be slow or weak in Chinese: use an 8-second local attempt and cloud fallback.
- Python `/chat` masks Moonshot failure with server rules: expose `source` while preserving HTTP behavior.
- Sequential provider latency: enforce one 38-second budget.
- Stale callbacks: retain `ChatRequestState` completion gate.
- Configuration mistakes: validate and degrade to Unity knowledge.
- Credentials: continue security tests prohibiting provider secrets and direct provider endpoints in Unity.

