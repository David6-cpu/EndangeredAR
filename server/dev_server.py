import json
import math
import os
from dataclasses import dataclass
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Dict, List, Optional
from urllib import error, request
from urllib.parse import urlparse


ROOT = Path(__file__).resolve().parents[1]
ANIMALS_DIR = ROOT / "content" / "animals"
ENV_FILE = ROOT / ".env.local"
DEFAULT_MOONSHOT_BASE_URL = "https://api.moonshot.cn/v1"
DEFAULT_MOONSHOT_MODEL = "moonshot-v1-8k"
DEFAULT_LOCAL_LLM_TIMEOUT = 7.0
MAX_LOCAL_LLM_TIMEOUT = 60.0
MAX_HISTORY_MESSAGES = 20


@dataclass(frozen=True)
class ProviderResult:
    reply: Optional[str] = None
    error: Optional[str] = None


def load_local_env() -> None:
    if not ENV_FILE.exists():
        print("No .env.local found; chat will use the local fallback.", flush=True)
        return

    for raw_line in ENV_FILE.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        os.environ.setdefault(key.strip(), value.strip().strip('"').strip("'"))

    print("Loaded local chat configuration.", flush=True)


def load_json(path: Path) -> Dict:
    return json.loads(path.read_text(encoding="utf-8"))


def get_animal(animal_id: str) -> Optional[Dict]:
    safe_id = "".join(character for character in animal_id if character.isalnum() or character in "-_")
    path = ANIMALS_DIR / f"{safe_id}.json"
    return load_json(path) if safe_id and path.exists() else None


def list_animals() -> List[Dict]:
    return [load_json(path) for path in sorted(ANIMALS_DIR.glob("*.json"))]


def make_system_prompt(animal: Dict) -> str:
    name = animal.get("name", "濒危动物")
    nickname = animal.get("nickname", "动物朋友")
    foods = "、".join(animal.get("food", [])) or "森林中的天然食物"
    threats = "、".join(animal.get("threats", [])) or "栖息地破坏"
    actions = "、".join(animal.get("protectionActions", [])) or "保护栖息地、传播正确知识"
    personality = animal.get("personality", "活泼、温柔、好奇，有一点孩子气")
    return (
        f"你是濒危动物科普 App 中的角色“{nickname}”，物种是{name}。"
        f"你的性格是：{personality}。你的食物包括：{foods}。"
        f"你面临的威胁包括：{threats}。用户可以采取的保护行动包括：{actions}。"
        "请始终以角色第一人称用中文回答，像青少年朋友聊天，不要使用 AI 助手口吻。"
        "回答要自然、简短、准确，每次不超过 120 个汉字；合适时可主动问一个小问题或鼓励环保行动。"
        "拒绝危险、违法或伤害动物的请求；资料里没有答案时说明不确定，不要编造。"
    )


def make_rule_reply(animal: Dict, message: str) -> str:
    nickname = animal.get("nickname", "我")
    normalized = message.strip()
    if "吃" in normalized or "食物" in normalized:
        foods = "、".join(animal.get("food", [])) or "森林里的嫩叶和果实"
        return f"我是{nickname}，最喜欢森林里的{foods}啦！人类零食不适合我。你愿意帮我选一份健康食物吗？"
    if "保护" in normalized or "帮助" in normalized:
        actions = "、".join(animal.get("protectionActions", [])[:2]) or "保护森林栖息地"
        return f"谢谢你愿意帮助{nickname}！你可以从{actions}做起，把保护森林的知识告诉更多人。"
    if "住" in normalized or "栖息" in normalized:
        habitat = animal.get("habitat", "热带和亚热带森林")
        return f"{nickname}住在{habitat}。森林不只是我的家，也为许多生命提供食物和通道。"
    if "濒危" in normalized or "威胁" in normalized:
        threats = "、".join(animal.get("threats", [])) or "栖息地破坏"
        return f"让我担心的主要问题是{threats}。森林越来越零碎，我和伙伴就更难安全生活了。"
    return f"我是{nickname}，很高兴和你一起认识森林！你想先了解我的食物、家园，还是怎么保护我呢？"


def make_llm_messages(animal: Dict, message: str, history: List[Dict]) -> List[Dict]:
    clean_history = []
    for item in history or []:
        if not isinstance(item, dict):
            continue
        role = item.get("role")
        content = item.get("content")
        if role not in ("user", "assistant") or not isinstance(content, str) or not content.strip():
            continue
        clean_history.append({"role": role, "content": content.strip()})

    messages = [{"role": "system", "content": make_system_prompt(animal)}]
    messages.extend(clean_history[-MAX_HISTORY_MESSAGES:])
    messages.append({"role": "user", "content": message.strip()})
    return messages


def make_llm_payload(animal: Dict, message: str, history: List[Dict]) -> Dict:
    return {
        "model": os.environ.get("MOONSHOT_MODEL", DEFAULT_MOONSHOT_MODEL),
        "messages": make_llm_messages(animal, message, history),
        "temperature": 0.8,
        "max_completion_tokens": 220,
    }


def make_local_llm_payload(animal: Dict, message: str, history: List[Dict]) -> Dict:
    payload = {
        "messages": make_llm_messages(animal, message, history),
        "temperature": 0.8,
        "max_completion_tokens": 220,
    }
    model = os.environ.get("LOCAL_LLM_MODEL", "").strip()
    if model:
        payload["model"] = model
    return payload


def get_local_llm_timeout() -> float:
    raw_timeout = os.environ.get("LOCAL_LLM_TIMEOUT", "").strip()
    try:
        timeout = float(raw_timeout) if raw_timeout else DEFAULT_LOCAL_LLM_TIMEOUT
    except ValueError:
        return DEFAULT_LOCAL_LLM_TIMEOUT
    if not math.isfinite(timeout):
        return DEFAULT_LOCAL_LLM_TIMEOUT
    return min(max(timeout, 1.0), MAX_LOCAL_LLM_TIMEOUT)


def call_moonshot(animal: Dict, message: str, history: List[Dict]) -> Optional[str]:
    api_key = os.environ.get("MOONSHOT_API_KEY", "").strip()
    if not api_key:
        return None

    base_url = os.environ.get("MOONSHOT_BASE_URL", DEFAULT_MOONSHOT_BASE_URL).rstrip("/")
    payload = make_llm_payload(animal, message, history)
    data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
    http_request = request.Request(
        f"{base_url}/chat/completions",
        data=data,
        method="POST",
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
    )

    try:
        with request.urlopen(http_request, timeout=35) as response:
            result = json.loads(response.read().decode("utf-8"))
    except error.HTTPError as exc:
        print(f"Moonshot request failed with HTTP {exc.code}; using local fallback.", flush=True)
        return None
    except (OSError, json.JSONDecodeError) as exc:
        print(f"Moonshot request failed ({type(exc).__name__}); using local fallback.", flush=True)
        return None

    choices = result.get("choices") or []
    content = (choices[0].get("message") or {}).get("content") if choices else None
    return content.strip() if isinstance(content, str) and content.strip() else None


def call_local_llm(animal: Dict, message: str, history: List[Dict]) -> ProviderResult:
    base_url = os.environ.get("LOCAL_LLM_BASE_URL", "").strip().rstrip("/")
    if not base_url:
        return ProviderResult(error="local_llm_not_configured")
    try:
        parsed_base_url = urlparse(base_url)
    except ValueError:
        return ProviderResult(error="local_llm_invalid_configuration")
    if parsed_base_url.scheme not in ("http", "https") or not parsed_base_url.netloc:
        return ProviderResult(error="local_llm_invalid_configuration")

    data = json.dumps(make_local_llm_payload(animal, message, history), ensure_ascii=False).encode("utf-8")
    http_request = request.Request(
        f"{base_url}/chat/completions",
        data=data,
        method="POST",
        headers={"Content-Type": "application/json"},
    )

    try:
        with request.urlopen(http_request, timeout=get_local_llm_timeout()) as response:
            result = json.loads(response.read().decode("utf-8"))
    except TimeoutError:
        return ProviderResult(error="local_llm_timeout")
    except error.HTTPError:
        return ProviderResult(error="local_llm_provider_error")
    except error.URLError as exc:
        if isinstance(exc.reason, TimeoutError):
            return ProviderResult(error="local_llm_timeout")
        return ProviderResult(error="local_llm_unavailable")
    except OSError:
        return ProviderResult(error="local_llm_unavailable")
    except (UnicodeDecodeError, json.JSONDecodeError):
        return ProviderResult(error="local_llm_invalid_response")

    if not isinstance(result, dict):
        return ProviderResult(error="local_llm_invalid_response")
    choices = result.get("choices")
    if not isinstance(choices, list) or not choices or not isinstance(choices[0], dict):
        return ProviderResult(error="local_llm_invalid_response")
    response_message = choices[0].get("message")
    content = response_message.get("content") if isinstance(response_message, dict) else None
    if not isinstance(content, str) or not content.strip():
        return ProviderResult(error="local_llm_invalid_response")
    return ProviderResult(reply=content.strip())


def make_chat_response(animal: Dict, reply: str, source: str, route_reason: str) -> Dict:
    nickname = animal.get("nickname", "动物朋友")
    return {
        "animalId": animal["id"],
        "reply": reply,
        "suggestedQuestions": ["你平时吃什么？", "你为什么会濒危？", "我可以怎样保护你？"],
        "missionHint": f"可以去完成“帮{nickname}寻找食物”任务。",
        "source": source,
        "routeReason": route_reason,
    }


def local_error_status(error_code: str) -> int:
    if error_code in ("local_llm_not_configured", "local_llm_invalid_configuration"):
        return 503
    if error_code == "local_llm_timeout":
        return 504
    return 502


def process_chat_request(path: str, payload: Dict) -> tuple[Dict, int]:
    if path not in ("/chat", "/chat/local"):
        return {"error": "not_found"}, 404

    animal = get_animal(str(payload.get("animalId") or "sensen"))
    if animal is None:
        return {"error": "animal_not_found"}, 404

    message = str(payload.get("message") or "").strip()
    if not message:
        return {"error": "message_required"}, 400

    history = payload.get("history")
    if not isinstance(history, list):
        history = []

    if path == "/chat/local":
        local_result = call_local_llm(animal, message, history)
        if local_result.reply is None:
            return {"error": local_result.error}, local_error_status(local_result.error or "")
        return make_chat_response(
            animal,
            local_result.reply,
            "local_llm",
            "local_provider_succeeded",
        ), 200

    if path == "/chat":
        reply = call_moonshot(animal, message, history)
        if reply:
            return make_chat_response(animal, reply, "cloud_llm", "cloud_provider_succeeded"), 200
        return make_chat_response(
            animal,
            make_rule_reply(animal, message),
            "server_rule",
            "cloud_provider_unavailable_server_rule_fallback",
        ), 200

    return {"error": "not_found"}, 404


class Handler(BaseHTTPRequestHandler):
    def do_OPTIONS(self) -> None:
        self.send_json({})

    def do_GET(self) -> None:
        path = urlparse(self.path).path
        if path == "/health":
            self.send_json({"status": "ok"})
            return
        if path == "/animals":
            self.send_json(list_animals())
            return
        self.send_json({"error": "not_found"}, status=404)

    def do_POST(self) -> None:
        path = urlparse(self.path).path
        if path not in ("/chat", "/chat/local"):
            self.send_json({"error": "not_found"}, status=404)
            return

        try:
            length = int(self.headers.get("Content-Length", "0"))
            payload = json.loads(self.rfile.read(length).decode("utf-8") or "{}")
        except (ValueError, UnicodeDecodeError, json.JSONDecodeError):
            self.send_json({"error": "invalid_json"}, status=400)
            return

        response, status = process_chat_request(path, payload)
        self.send_json(response, status=status)

    def log_message(self, format_string: str, *args) -> None:
        print(f"{self.address_string()} - {format_string % args}", flush=True)

    def send_json(self, payload: Dict, status: int = 200) -> None:
        data = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Access-Control-Allow-Methods", "GET,POST,OPTIONS")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


def run() -> None:
    load_local_env()
    server = ThreadingHTTPServer(("0.0.0.0", 8000), Handler)
    print("Endangered AR chat proxy listening on http://0.0.0.0:8000", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    run()
