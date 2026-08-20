# R1 Hybrid AI Routing Verification

Date: 2026-08-21
Branch: `codex/r1-hybrid-ai-routing`

## Scope

R1 adds a switchable Unity Provider boundary while preserving the existing chat UI, cloud proxy, animal-switch request isolation, conversation history, and Unity knowledge fallback.

```text
DemoAppController
  -> AIManager / AIRouter
     -> LocalLLMProvider -> Python /chat/local -> llama.cpp-compatible server
     -> CloudLLMProvider -> ChatApiClient -> Python /chat -> Moonshot or Python rule
     -> LocalKnowledgeProvider -> LocalKnowledgeChatService
  -> AIResponse -> existing chat transcript and model bubble
```

The response contract records both `source` and `routeReason`. R1 only displays `reply`; future fields such as action, emotion, citations, and mission context are not executed.

## Checked-in configuration

`Assets/Config/LocalAIConfig.asset` defaults to:

```text
RouteMode: CloudOnly
Local server URL: http://127.0.0.1:8000
Local timeout: 8 seconds
Total Provider budget: 38 seconds
```

The existing UI timeout guard remains 40 seconds. When Cloud is configured, its credentials must stay in the Git-ignored repository-root `.env.local` file loaded by Python and must never enter Unity.

## Automated verification

Run from the repository root.

Python:

```bash
python3 -m unittest discover -s server/tests -v
```

Unity EditMode:

```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics \
  -projectPath "$PWD/EndangeredAR" \
  -runTests -testPlatform EditMode \
  -testResults /tmp/endangeredar-r1-editmode.xml \
  -logFile /tmp/endangeredar-r1-editmode.log
```

Expected R1 baseline at the time of this document:

| Suite | Result |
| --- | ---: |
| Python | 16 / 16 passed |
| Unity EditMode | 124 / 124 passed |

## Executed HTTP checks

The following checks were run locally on 2026-08-21:

| Check | Result |
| --- | --- |
| `GET /health` | HTTP 200, `status=ok` |
| `/chat/local` without local configuration | HTTP 503, `local_llm_not_configured` |
| `/chat/local` through a temporary OpenAI-compatible test double | HTTP 200, `source=local_llm`, `routeReason=local_provider_succeeded` |
| `/chat/local` after stopping that test double | HTTP 502, explicit local Provider failure |
| `/chat` without Moonshot configuration | HTTP 200, `source=server_rule`, existing Python fallback preserved |

The temporary compatible service verifies the Python adapter boundary, not real model quality or performance. A real `llama-server` executable and GGUF were not present on this machine, so Cases A and D below still require real-model acceptance.

## Manual acceptance checklist

Before testing, start `llama-server` with a GGUF on `127.0.0.1:8080`, start `python3 server/dev_server.py`, and configure Unity to reach port 8000.

| Case | Setup | Expected result |
| --- | --- | --- |
| A | Local and Cloud available; `LocalFirstCloudFallback` | `source=local_llm`, `routeReason=local_first` |
| B | Stop llama.cpp; keep Python and Cloud available | `/chat` answers; `routeReason=local_first_cloud_fallback` |
| C | Local and Cloud proxy unavailable | Unity knowledge answers; `source=unity_knowledge` |
| D | `LocalOnly`; llama.cpp and Python running; Internet disconnected | Local answer succeeds and Cloud is never requested |

For Case B, `/chat` can legitimately return `source=server_rule` when Moonshot is unconfigured or unavailable because R1 deliberately preserves the old server fallback behavior.

## Device networking

- Unity Editor may use `http://127.0.0.1:8000` for both Local and Cloud proxy URLs.
- A physical phone must use `http://<MAC_LAN_IP>:8000` for both Unity URLs.
- Python on the Mac still uses `http://127.0.0.1:8080/v1` to reach llama.cpp.
- Production usage requires an HTTPS proxy; the development LAN setup is not a production deployment.

## R1 exclusions

RAG, embeddings, vector databases, streaming output, JNI/native mobile inference, llama.cpp inside Unity, fine-tuning, AI animation control, and AI task-state mutation are not implemented in R1.
