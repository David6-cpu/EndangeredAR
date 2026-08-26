# EndangeredAR AI Proxy

`server/dev_server.py` is the project-owned HTTP boundary between Unity and AI providers. It keeps the Moonshot key out of Unity and exposes two independent chat endpoints:

```text
POST /chat/local -> llama.cpp-compatible HTTP server
POST /chat       -> Moonshot -> existing Python rule fallback
```

`/chat/local` never calls Moonshot and never substitutes the Python rule answer. Its failures are explicit non-2xx responses so Unity can decide whether to try Cloud. `/chat` preserves the R1 provider behavior: Moonshot answers when configured and available; otherwise Python returns an application-owned fallback with HTTP 200.

Before either provider runs, R2 classifies the question and retrieves evidence from `content/animals/<animalId>.json`. Scientific facts are returned from the approved canonical answer; Local and Cloud receive identical evidence and cannot replace it with model memory. Unsupported facts and known-unknown values are answered deterministically without calling either model.

## 1. Configuration

Run from the repository root:

```bash
cp server/.env.example .env.local
```

`.env.local` is ignored by Git. Available variables:

```dotenv
MOONSHOT_API_KEY=
MOONSHOT_BASE_URL=https://api.moonshot.cn/v1
MOONSHOT_MODEL=moonshot-v1-8k
DEV_SERVER_HOST=127.0.0.1
LOCAL_LLM_BASE_URL=http://127.0.0.1:8080/v1
LOCAL_LLM_MODEL=
LOCAL_LLM_TIMEOUT=7
```

- `MOONSHOT_API_KEY`: optional for offline/local use; keep it only in `.env.local`.
- `DEV_SERVER_HOST`: proxy listen address. It defaults to `127.0.0.1` so cloud credentials are not exposed to the local network.
- `LOCAL_LLM_BASE_URL`: OpenAI-compatible base URL. The proxy appends `/chat/completions`.
- `LOCAL_LLM_MODEL`: optional model identifier. Leave empty for llama.cpp servers that do not require one.
- `LOCAL_LLM_TIMEOUT`: Python-to-local-model timeout in seconds; malformed values fall back to 7 and values are clamped to 1-60.

Do not add any cloud API key to Unity assets, scenes, C# files, or Git.

## 2. Start a local GGUF model

The verified R1.5 development baseline is:

- [Qwen2.5-1.5B-Instruct-GGUF](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF), official `Q4_K_M` quantization, Apache-2.0.
- [llama.cpp server](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md), installed with `brew install llama.cpp` on macOS.
- Model file: `.local-models/qwen2.5-1.5b-instruct-q4_k_m.gguf` (ignored by Git).
- Local SHA-256: `6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e`.

Download the GGUF from the official model repository, verify it, then start the OpenAI-compatible server in terminal A:

```bash
shasum -a 256 .local-models/qwen2.5-1.5b-instruct-q4_k_m.gguf
```

```bash
llama-server \
  -m .local-models/qwen2.5-1.5b-instruct-q4_k_m.gguf \
  --alias qwen2.5-1.5b-instruct-q4_k_m \
  --host 127.0.0.1 \
  --port 8080 \
  -c 4096 \
  -np 1 \
  -t 6 \
  -tb 6 \
  -ngl all \
  -fa on \
  --temp 0.7 \
  --top-p 0.8 \
  -n 220 \
  --metrics \
  --no-webui \
  --offline \
  --log-timestamps
```

The GGUF path must exist. R1 does not embed llama.cpp or the model in Unity, iOS, Android, or the App package.

The corresponding local environment values are:

```dotenv
LOCAL_LLM_BASE_URL=http://127.0.0.1:8080/v1
LOCAL_LLM_MODEL=qwen2.5-1.5b-instruct-q4_k_m
LOCAL_LLM_TIMEOUT=7
```

## 3. Start the Python proxy

In terminal B, from the repository root:

```bash
python3 server/dev_server.py
```

`dev_server.py` automatically loads `<repository-root>/.env.local`, resolving the repository root from the script location rather than the shell's current directory. Existing process environment variables take precedence over values in the file. The explicit `set -a; source .env.local; set +a` form is also valid, but it is not required for normal startup.

The proxy listens on `127.0.0.1:8000` by default; it calls the local model at the address configured by `LOCAL_LLM_BASE_URL`. For explicit physical-device testing on a trusted LAN, set `DEV_SERVER_HOST=0.0.0.0` temporarily and use the Mac's LAN address in Unity. Do not use the wildcard address on an untrusted network.

## 4. Verify the endpoints

Health:

```bash
curl -sS http://127.0.0.1:8000/health
```

Local llama.cpp path:

```bash
curl -sS -X POST http://127.0.0.1:8000/chat/local \
  -H 'Content-Type: application/json' \
  -d '{"animalId":"sensen","message":"森森，你平时吃什么？","history":[]}'
```

Development-only explicit Moonshot path:

```bash
curl -sS -X POST http://127.0.0.1:8000/chat \
  -H 'Content-Type: application/json' \
  -d '{"animalId":"sensen","message":"森森，你平时吃什么？","history":[]}'
```

A successful response includes `reply`, `source`, `routeReason`, `contentAuthority`, `languageGenerator`, `answerMode`, `evidenceStatus`, and `citations`. Direct `/chat/local` success requires a real llama.cpp completion and uses `source: local_llm`. Direct `/chat` succeeds only after a real Moonshot completion and uses `source: cloud_llm`; it is reserved for explicit Development testing. Neither endpoint returns Python rule text as a successful chat completion. `citations` are resolved from canonical source metadata by application code; providers cannot create accepted source IDs or URLs.

## 5. Grounded knowledge contract

The only hand-maintained Sensen knowledge document is `content/animals/sensen.json`. It contains:

- reviewed identity and taxonomy;
- facts with stable `factId`, topic, approved answer, aliases, evidence status, source IDs, confidence, and verification date;
- source records with title, organization, source type, URL, source date, project verification date, and applicable fact IDs.

Retrieval returns one of:

| `answerMode` | `evidenceStatus` | Behavior |
| --- | --- | --- |
| `grounded_fact` | `evidence_found` | give canonical evidence to the selected LLM, validate its wording, and attach canonical citations |
| `grounded_fact` | `insufficient_evidence` | require the selected LLM to state that evidence is unavailable; never invent a number or behavior |
| `social_chat` | `not_required` | allow the selected LLM to produce short role conversation without fabricated facts or citations |
| `off_domain` | `not_required` | give the selected LLM a system-policy boundary for a safe natural-language response |

The Unity knowledge profile is generated from the same JSON through `Endangered AR > Data > Rebuild Sensen Content`. It remains useful for retrieval, metadata validation, deterministic tests, and diagnostics, but it no longer has user-facing chat authority. Do not edit the generated knowledge asset as a separate source of truth.

## 6. Unity routing

Configure `EndangeredAR/Assets/Config/LocalAIConfig.asset`:

| RouteMode | Order |
| --- | --- |
| `LocalOnly` | `/chat/local`; failure becomes `system_status` and never calls Cloud or Unity chat fallback |
| `CloudOnly` | `/chat`; available only when explicitly selected in Editor or a Development Build |
| `LocalFirstCloudFallback` | retained for configuration compatibility but currently executes as fail-closed Local-only |

The checked-in and non-Development default is `LocalOnly`. Default budgets are 8 seconds for the local attempt and 38 seconds for the complete Provider route. A Local failure becomes an explicit `local_model_unavailable` system status; it does not start a Cloud request or synthesize a character reply.

Unity Editor configuration:

```text
LocalAIConfig.localServerUrl = http://127.0.0.1:8000
LocalApiConfig.baseUrl       = http://127.0.0.1:8000
```

Physical phone configuration:

```text
LocalAIConfig.localServerUrl = http://<MAC_LAN_IP>:8000
LocalApiConfig.baseUrl       = http://<MAC_LAN_IP>:8000
```

On a phone, `127.0.0.1` is the phone itself, not the Mac. Keep the phone and Mac on the same network and allow incoming connections to Python. The Python process on the Mac can still use `LOCAL_LLM_BASE_URL=http://127.0.0.1:8080/v1` to reach llama.cpp on that Mac.

## 7. Manual R1 cases

- **Case A:** run llama.cpp and Python with the default `LocalOnly`, then ask “森森，你平时吃什么？”. Unity must report `contentAuthority=canonical_knowledge`, `languageGenerator=local_llm`, `source=local_llm`, preserve citations, and keep Eat authorization in application metadata.
- **Case B:** stop llama.cpp but keep Python and the App running, then ask an open social question. Unity must report `source=system_status`, `errorCode=local_model_unavailable`, and must not call Cloud, Python rule chat, or Unity chat fallback.
- **Case C:** restart llama.cpp and resend the question. The same App session must recover to `source=local_llm` without changing Progress or reinstalling.
- **Case D:** explicitly select `CloudOnly` in a Development Build to compare Moonshot. A successful real model completion reports `source=cloud_llm`; a failure reports system status rather than a deterministic chat reply.

Unity logs the selected `source` and `routeReason`; raw technical errors are not shown in the chat UI.

## 8. Troubleshooting

- `local_llm_not_configured` (503): set `LOCAL_LLM_BASE_URL` and restart Python.
- `local_llm_invalid_configuration` (503): use an absolute `http://` or `https://` base URL with a valid host. Unity 的 `LocalAIConfig.localServerUrl` 还必须不带 query 或 fragment。
- `local_llm_timeout` (504): confirm llama.cpp is responsive or adjust `LOCAL_LLM_TIMEOUT`; Unity still enforces its own route budget.
- `local_llm_unavailable` / 502: confirm `llama-server` is running on port 8080 and the `/v1` prefix matches the server.
- Phone cannot connect: replace phone-side localhost with the Mac LAN IP, use the same port 8000, confirm both devices are on the same network, and check the macOS firewall.
- `local_model_unavailable` / 503: the adapter could not obtain a real llama.cpp completion. The product shows an explicit system status and does not fall back to another chat source.
- `ai_response_validation_failed` / 422: both the initial completion and one stricter retry violated the trusted-context response contract. The product shows a validation status and does not substitute fixed character text.

R2 deliberately does not implement vector databases, embeddings, streaming responses, native mobile inference, fine-tuning, or AI-driven animation/task mutation.

The full real-GGUF baseline, quality findings, failure injection results, and merge gate are recorded in [the R1.5 acceptance report](../EndangeredAR/docs/verification/2026-08-21-r1.5-real-gguf-acceptance.md).

The grounded 20-question quality fixture is `content/quality/sensen-r1.5-questions.json`; its regression test verifies classifications, refusal behavior, canonical citation coverage, and Local/Cloud evidence consistency.

## 9. Tests

```bash
python3 -m unittest discover -s server/tests -v
```
