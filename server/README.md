# EndangeredAR AI Proxy

`server/dev_server.py` is the project-owned HTTP boundary between Unity and AI providers. It keeps the Moonshot key out of Unity and exposes two independent chat endpoints:

```text
POST /chat/local -> llama.cpp-compatible HTTP server
POST /chat       -> Moonshot -> existing Python rule fallback
```

`/chat/local` never calls Moonshot and never substitutes the Python rule answer. Its failures are explicit non-2xx responses so Unity can decide whether to try Cloud. `/chat` preserves the pre-R1 behavior: Moonshot answers when configured and available; otherwise Python returns a short animal-specific rule answer with HTTP 200.

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
LOCAL_LLM_BASE_URL=http://127.0.0.1:8080/v1
LOCAL_LLM_MODEL=
LOCAL_LLM_TIMEOUT=7
```

- `MOONSHOT_API_KEY`: optional for offline/local use; keep it only in `.env.local`.
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

The proxy listens on `0.0.0.0:8000`; it calls the local model at the address configured by `LOCAL_LLM_BASE_URL`.

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

Existing Moonshot/Python-rule path:

```bash
curl -sS -X POST http://127.0.0.1:8000/chat \
  -H 'Content-Type: application/json' \
  -d '{"animalId":"sensen","message":"森森，你平时吃什么？","history":[]}'
```

A successful response includes `reply`, `source`, and `routeReason`. Direct `/chat/local` success uses `source: local_llm`. Direct `/chat` uses `source: cloud_llm` when Moonshot answers or `source: server_rule` when its built-in fallback answers.

## 5. Unity routing

Configure `EndangeredAR/Assets/Config/LocalAIConfig.asset`:

| RouteMode | Order |
| --- | --- |
| `CloudOnly` | `/chat` -> Unity `LocalKnowledgeChatService` |
| `LocalOnly` | `/chat/local` -> Unity `LocalKnowledgeChatService`; Cloud is never called |
| `LocalFirstCloudFallback` | `/chat/local` -> `/chat` -> Unity `LocalKnowledgeChatService` |

The checked-in default remains `CloudOnly`. Default budgets are 8 seconds for the local attempt and 38 seconds for the complete Provider route. The existing chat UI guard is 40 seconds, so a failed local attempt does not start a new full 40-second Cloud wait.

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

## 6. Manual R1 cases

- **Case A:** run llama.cpp and Python, configure Moonshot, choose `LocalFirstCloudFallback`. Ask “森森，你平时吃什么？”. Unity should log `source=local_llm` and `routeReason=local_first`.
- **Case B:** stop llama.cpp but keep Python running. In `LocalFirstCloudFallback`, Unity should continue through `/chat`; the source is `cloud_llm` when Moonshot succeeds, otherwise `server_rule` by existing `/chat` behavior.
- **Case C:** make both Python HTTP paths unavailable, for example by stopping Python. Unity should return its `LocalKnowledgeChatService` answer with `source=unity_knowledge`.
- **Case D:** run llama.cpp and Python, choose `LocalOnly`, then disconnect Internet. Chat should still answer locally and must not access Cloud.

Unity logs the selected `source` and `routeReason`; raw technical errors are not shown in the chat UI.

## 7. Troubleshooting

- `local_llm_not_configured` (503): set `LOCAL_LLM_BASE_URL` and restart Python.
- `local_llm_invalid_configuration` (503): use an absolute `http://` or `https://` base URL with a valid host. Unity 的 `LocalAIConfig.localServerUrl` 还必须不带 query 或 fragment。
- `local_llm_timeout` (504): confirm llama.cpp is responsive or adjust `LOCAL_LLM_TIMEOUT`; Unity still enforces its own route budget.
- `local_llm_unavailable` / 502: confirm `llama-server` is running on port 8080 and the `/v1` prefix matches the server.
- Phone cannot connect: replace phone-side localhost with the Mac LAN IP, use the same port 8000, confirm both devices are on the same network, and check the macOS firewall.
- `/chat` returns `server_rule`: Moonshot is unconfigured or unavailable; this is the intended backwards-compatible fallback.

R1 does not implement RAG, embeddings, vector databases, streaming responses, native mobile inference, fine-tuning, or AI-driven animation/task mutation.

The full real-GGUF baseline, quality findings, failure injection results, and merge gate are recorded in [the R1.5 acceptance report](../EndangeredAR/docs/verification/2026-08-21-r1.5-real-gguf-acceptance.md).

## 8. Tests

```bash
python3 -m unittest discover -s server/tests -v
```
