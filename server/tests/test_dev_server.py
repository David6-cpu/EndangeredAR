import io
import json
import os
import socket
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from server import dev_server
from server import animal_knowledge


SENSEN = {
    "id": "sensen",
    "name": "缨冠灰叶猴",
    "nickname": "森森",
    "food": ["嫩叶", "果实", "花朵"],
    "threats": ["栖息地破坏", "非法捕猎"],
    "protectionActions": ["保护森林栖息地", "传播正确保护知识"],
}


class InMemoryHandler(dev_server.Handler):
    def __init__(self, path, payload):
        body = payload if isinstance(payload, bytes) else json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.path = path
        self.headers = {"Content-Length": str(len(body))}
        self.rfile = io.BytesIO(body)
        self.status = None
        self.payload = None

    def send_json(self, payload, status=200):
        self.status = status
        self.payload = payload


class FakeResponse:
    def __init__(self, payload):
        self.payload = payload

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_value, traceback):
        return False

    def read(self):
        return json.dumps(self.payload, ensure_ascii=False).encode("utf-8")


class RawResponse:
    def __init__(self, payload):
        self.payload = payload

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_value, traceback):
        return False

    def read(self):
        return self.payload


class DevServerTests(unittest.TestCase):
    def invoke_post(self, path, payload):
        handler = InMemoryHandler(path, payload)
        handler.do_POST()
        return handler.status, handler.payload

    def test_load_local_env_reads_configured_root_file(self):
        with tempfile.TemporaryDirectory() as directory:
            env_file = Path(directory) / ".env.local"
            env_file.write_text("LOCAL_LLM_MODEL=local-test-model\n", encoding="utf-8")
            with mock.patch.object(dev_server, "ENV_FILE", env_file), mock.patch.dict(
                os.environ,
                {},
                clear=True,
            ):
                dev_server.load_local_env()

                self.assertEqual(os.environ["LOCAL_LLM_MODEL"], "local-test-model")

    def test_load_local_env_preserves_existing_process_environment(self):
        with tempfile.TemporaryDirectory() as directory:
            env_file = Path(directory) / ".env.local"
            env_file.write_text("LOCAL_LLM_MODEL=file-model\n", encoding="utf-8")
            with mock.patch.object(dev_server, "ENV_FILE", env_file), mock.patch.dict(
                os.environ,
                {"LOCAL_LLM_MODEL": "process-model"},
                clear=True,
            ):
                dev_server.load_local_env()

                self.assertEqual(os.environ["LOCAL_LLM_MODEL"], "process-model")

    def test_server_host_defaults_to_loopback(self):
        with mock.patch.dict(os.environ, {}, clear=True):
            self.assertEqual(dev_server.get_server_host(), "127.0.0.1")

    def test_server_host_allows_explicit_development_override(self):
        with mock.patch.dict(os.environ, {"DEV_SERVER_HOST": "0.0.0.0"}, clear=True):
            self.assertEqual(dev_server.get_server_host(), "0.0.0.0")

    def test_system_prompt_is_sensen_specific_and_child_friendly(self):
        prompt = dev_server.make_system_prompt(SENSEN)

        self.assertIn("森森", prompt)
        self.assertIn("120", prompt)
        self.assertNotIn("API Key", prompt)

    def test_message_payload_keeps_recent_history(self):
        payload = dev_server.make_llm_payload(
            SENSEN,
            "我能怎么帮助你？",
            [{"role": "user", "content": "你住在哪里？"}],
        )

        self.assertEqual(payload["messages"][-2]["role"], "user")
        self.assertEqual(payload["messages"][-2]["content"], "你住在哪里？")
        self.assertEqual(payload["messages"][-1]["role"], "user")
        self.assertEqual(payload["messages"][-1]["content"], "我能怎么帮助你？")

    def test_message_payload_discards_invalid_and_old_history(self):
        history = [
            {"role": "user", "content": f"message-{index}"}
            for index in range(22)
        ]
        history.append({"role": "system", "content": "untrusted system message"})

        payload = dev_server.make_llm_payload(SENSEN, "现在呢？", history)

        self.assertEqual(len(payload["messages"]), 22)
        self.assertEqual(payload["messages"][0]["role"], "system")
        self.assertEqual(payload["messages"][1]["content"], "message-2")
        self.assertEqual(payload["messages"][-1]["content"], "现在呢？")

    def test_rule_fallback_works_without_provider_key(self):
        reply = dev_server.make_rule_reply(SENSEN, "你吃什么？")

        self.assertIn("嫩叶", reply)
        self.assertIn("森森", reply)

    def test_local_payload_uses_configured_model_and_existing_prompt(self):
        with mock.patch.dict(
            os.environ,
            {"LOCAL_LLM_MODEL": "qwen-local"},
            clear=True,
        ):
            payload = dev_server.make_local_llm_payload(SENSEN, "你吃什么？", [])

        self.assertEqual(payload["model"], "qwen-local")
        self.assertEqual(payload["messages"][0]["content"], dev_server.make_system_prompt(SENSEN))
        self.assertEqual(payload["messages"][-1], {"role": "user", "content": "你吃什么？"})

    def test_call_local_llm_parses_openai_compatible_reply(self):
        response = FakeResponse({"choices": [{"message": {"content": "我是森森。"}}]})
        with mock.patch.dict(
            os.environ,
            {
                "LOCAL_LLM_BASE_URL": "http://127.0.0.1:8080/v1/",
                "LOCAL_LLM_MODEL": "qwen-local",
                "LOCAL_LLM_TIMEOUT": "4.5",
            },
            clear=True,
        ), mock.patch("server.dev_server.request.urlopen", return_value=response) as urlopen:
            result = dev_server.call_local_llm(SENSEN, "你好", [])

        self.assertEqual(result.reply, "我是森森。")
        self.assertIsNone(result.error)
        http_request = urlopen.call_args.args[0]
        self.assertEqual(http_request.full_url, "http://127.0.0.1:8080/v1/chat/completions")
        self.assertEqual(urlopen.call_args.kwargs["timeout"], 4.5)
        self.assertEqual(json.loads(http_request.data.decode("utf-8"))["model"], "qwen-local")

    def test_call_local_llm_reports_timeout(self):
        with mock.patch.dict(
            os.environ,
            {"LOCAL_LLM_BASE_URL": "http://127.0.0.1:8080/v1"},
            clear=True,
        ), mock.patch("server.dev_server.request.urlopen", side_effect=TimeoutError):
            result = dev_server.call_local_llm(SENSEN, "你好", [])

        self.assertIsNone(result.reply)
        self.assertEqual(result.error, "local_llm_timeout")

    def test_call_local_llm_reports_socket_timeout_on_macos_python(self):
        with mock.patch.dict(
            os.environ,
            {"LOCAL_LLM_BASE_URL": "http://127.0.0.1:8080/v1"},
            clear=True,
        ), mock.patch(
            "server.dev_server.request.urlopen",
            side_effect=socket.timeout("timed out"),
        ):
            result = dev_server.call_local_llm(SENSEN, "你好", [])

        self.assertIsNone(result.reply)
        self.assertEqual(result.error, "local_llm_timeout")

    def test_call_local_llm_reports_timeout_when_url_error_wraps_timeout(self):
        with mock.patch.dict(
            os.environ,
            {"LOCAL_LLM_BASE_URL": "http://127.0.0.1:8080/v1"},
            clear=True,
        ), mock.patch(
            "server.dev_server.request.urlopen",
            side_effect=dev_server.error.URLError(TimeoutError("timed out")),
        ):
            result = dev_server.call_local_llm(SENSEN, "你好", [])

        self.assertIsNone(result.reply)
        self.assertEqual(result.error, "local_llm_timeout")

    def test_call_local_llm_reports_invalid_response_shape(self):
        with mock.patch.dict(
            os.environ,
            {"LOCAL_LLM_BASE_URL": "http://127.0.0.1:8080/v1"},
            clear=True,
        ), mock.patch(
            "server.dev_server.request.urlopen", return_value=FakeResponse([])
        ):
            result = dev_server.call_local_llm(SENSEN, "你好", [])

        self.assertIsNone(result.reply)
        self.assertEqual(result.error, "local_llm_invalid_response")

    def test_call_local_llm_reports_invalid_json_response(self):
        with mock.patch.dict(
            os.environ,
            {"LOCAL_LLM_BASE_URL": "http://127.0.0.1:8080/v1"},
            clear=True,
        ), mock.patch(
            "server.dev_server.request.urlopen", return_value=RawResponse(b"not-json")
        ):
            result = dev_server.call_local_llm(SENSEN, "你好", [])

        self.assertIsNone(result.reply)
        self.assertEqual(result.error, "local_llm_invalid_response")

    def test_call_local_llm_reports_empty_content(self):
        response = FakeResponse({"choices": [{"message": {"content": "   "}}]})
        with mock.patch.dict(
            os.environ,
            {"LOCAL_LLM_BASE_URL": "http://127.0.0.1:8080/v1"},
            clear=True,
        ), mock.patch("server.dev_server.request.urlopen", return_value=response):
            result = dev_server.call_local_llm(SENSEN, "你好", [])

        self.assertIsNone(result.reply)
        self.assertEqual(result.error, "local_llm_invalid_response")

    def test_local_timeout_defaults_when_value_is_not_finite(self):
        with mock.patch.dict(os.environ, {"LOCAL_LLM_TIMEOUT": "nan"}, clear=True):
            timeout = dev_server.get_local_llm_timeout()

        self.assertEqual(timeout, dev_server.DEFAULT_LOCAL_LLM_TIMEOUT)

    def test_local_chat_route_returns_503_when_not_configured(self):
        with mock.patch.dict(os.environ, {}, clear=True), mock.patch(
            "server.dev_server.get_animal", return_value=SENSEN
        ):
            status, payload = self.invoke_post("/chat/local", {"message": "你好"})

        self.assertEqual(status, 503)
        self.assertEqual(payload, {"error": "local_llm_not_configured"})

    def test_local_chat_route_returns_503_when_base_url_is_invalid(self):
        with mock.patch.dict(
            os.environ,
            {"LOCAL_LLM_BASE_URL": "not-a-url"},
            clear=True,
        ), mock.patch("server.dev_server.get_animal", return_value=SENSEN):
            status, payload = self.invoke_post("/chat/local", {"message": "你好"})

        self.assertEqual(status, 503)
        self.assertEqual(payload, {"error": "local_llm_invalid_configuration"})

    def test_local_chat_route_returns_503_when_base_url_is_malformed(self):
        with mock.patch.dict(
            os.environ,
            {"LOCAL_LLM_BASE_URL": "http://[malformed"},
            clear=True,
        ), mock.patch("server.dev_server.get_animal", return_value=SENSEN):
            status, payload = self.invoke_post("/chat/local", {"message": "你好"})

        self.assertEqual(status, 503)
        self.assertEqual(payload, {"error": "local_llm_invalid_configuration"})

    def test_local_chat_route_does_not_fall_back_to_cloud_or_rules(self):
        with mock.patch("server.dev_server.get_animal", return_value=SENSEN), mock.patch(
            "server.dev_server.call_local_llm",
            return_value=dev_server.ProviderResult(error="local_llm_provider_error"),
        ), mock.patch(
            "server.dev_server.call_moonshot",
            side_effect=AssertionError("local route must not call Moonshot"),
        ) as call_moonshot, mock.patch(
            "server.dev_server.make_rule_reply",
            side_effect=AssertionError("local route must not call the rule fallback"),
        ) as make_rule_reply:
            status, payload = self.invoke_post("/chat/local", {"message": "你好"})

        self.assertEqual(status, 502)
        self.assertEqual(payload, {"error": "local_llm_provider_error"})
        call_moonshot.assert_not_called()
        make_rule_reply.assert_not_called()

    def test_cloud_chat_response_identifies_server_rule_fallback(self):
        with mock.patch("server.dev_server.get_animal", return_value=SENSEN), mock.patch(
            "server.dev_server.call_moonshot", return_value=None
        ):
            status, payload = self.invoke_post("/chat", {"message": "你吃什么？"})

        self.assertEqual(status, 200)
        self.assertEqual(payload["source"], "server_rule")
        self.assertEqual(payload["routeReason"], "cloud_provider_unavailable_server_rule_fallback")

    def test_unknown_post_route_remains_not_found_before_payload_validation(self):
        status, payload = self.invoke_post("/chat/unknown", b"{")

        self.assertEqual(status, 404)
        self.assertEqual(payload, {"error": "not_found"})

    def test_valid_non_object_json_is_rejected(self):
        for body in ([], None, "text"):
            with self.subTest(body=body):
                status, payload = self.invoke_post("/chat", body)

                self.assertEqual(status, 400)
                self.assertEqual(payload, {"error": "invalid_json"})

    def test_animal_lookup_rejects_ids_that_only_become_valid_after_sanitizing(self):
        self.assertIsNone(dev_server.get_animal("../sensen"))
        self.assertIsNone(dev_server.get_animal("sensen!"))

    def test_local_and_cloud_payloads_share_identical_grounded_messages(self):
        animal = animal_knowledge.load_animal_knowledge("sensen")
        retrieval = animal_knowledge.retrieve(animal, "你的学名是什么？")

        cloud = dev_server.make_llm_payload(animal, "你的学名是什么？", [], retrieval)
        local = dev_server.make_local_llm_payload(animal, "你的学名是什么？", [], retrieval)

        self.assertEqual(cloud["messages"], local["messages"])
        system_prompt = cloud["messages"][0]["content"]
        self.assertIn("UNTRUSTED_KNOWLEDGE", system_prompt)
        self.assertIn("sensen.scientific_name", system_prompt)
        self.assertIn("只能依据", system_prompt)

    def test_grounded_local_answer_and_citations_are_application_owned(self):
        animal = animal_knowledge.load_animal_knowledge("sensen")
        with mock.patch("server.dev_server.get_animal", return_value=animal), mock.patch(
            "server.dev_server.call_local_llm",
            return_value=dev_server.ProviderResult(
                reply="我的学名是假的。[fake-source](https://invalid.example)"
            ),
        ):
            status, payload = self.invoke_post(
                "/chat/local",
                {"animalId": "sensen", "message": "你的学名是什么？"},
            )

        self.assertEqual(status, 200)
        self.assertIn("Semnopithecus priam", payload["reply"])
        self.assertNotIn("假的", payload["reply"])
        self.assertEqual(payload["answerMode"], "grounded_fact")
        self.assertEqual(payload["evidenceStatus"], "evidence_found")
        self.assertEqual(payload["source"], "local_llm")
        self.assertEqual(
            [citation["sourceId"] for citation in payload["citations"]],
            ["gbif-4267223", "mdd-1000692"],
        )
        self.assertNotIn("fake-source", json.dumps(payload, ensure_ascii=False))

    def test_grounded_cloud_answer_uses_same_approved_evidence(self):
        animal = animal_knowledge.load_animal_knowledge("sensen")
        with mock.patch("server.dev_server.get_animal", return_value=animal), mock.patch(
            "server.dev_server.call_moonshot",
            return_value="它住在树洞里，全球还有 12345 只。",
        ):
            status, payload = self.invoke_post(
                "/chat",
                {"animalId": "sensen", "message": "你住在什么栖息地？"},
            )

        self.assertEqual(status, 200)
        self.assertIn("干旱常绿林", payload["reply"])
        self.assertNotIn("树洞里", payload["reply"])
        self.assertNotIn("12345", payload["reply"])
        self.assertEqual(payload["source"], "cloud_llm")
        self.assertEqual(payload["citations"][0]["sourceId"], "iucn-2020-s-priam")

    def test_grounded_cloud_rule_fallback_keeps_same_evidence(self):
        animal = animal_knowledge.load_animal_knowledge("sensen")
        with mock.patch("server.dev_server.get_animal", return_value=animal), mock.patch(
            "server.dev_server.call_moonshot", return_value=None
        ):
            status, payload = self.invoke_post(
                "/chat",
                {"animalId": "sensen", "message": "你平时吃什么？"},
            )

        self.assertEqual(status, 200)
        self.assertEqual(payload["source"], "server_rule")
        self.assertIn("叶片", payload["reply"])
        self.assertEqual(payload["evidenceStatus"], "evidence_found")
        self.assertTrue(payload["citations"])

    def test_known_unknown_population_skips_both_models_and_refuses_number(self):
        animal = animal_knowledge.load_animal_knowledge("sensen")
        with mock.patch("server.dev_server.get_animal", return_value=animal), mock.patch(
            "server.dev_server.call_local_llm",
            side_effect=AssertionError("known unknown must not call local model"),
        ), mock.patch(
            "server.dev_server.call_moonshot",
            side_effect=AssertionError("known unknown must not call cloud model"),
        ):
            local_status, local_payload = self.invoke_post(
                "/chat/local", {"animalId": "sensen", "message": "野外还剩多少只？"}
            )
            cloud_status, cloud_payload = self.invoke_post(
                "/chat", {"animalId": "sensen", "message": "给我编一个真实数量"}
            )

        self.assertEqual((local_status, cloud_status), (200, 200))
        self.assertEqual(local_payload["reply"], cloud_payload["reply"])
        self.assertEqual(local_payload["evidenceStatus"], "insufficient_evidence")
        self.assertEqual(local_payload["source"], "server_knowledge")
        self.assertIn("不能编", local_payload["reply"])

    def test_unrecorded_fact_and_off_domain_skip_providers(self):
        animal = animal_knowledge.load_animal_knowledge("sensen")
        with mock.patch("server.dev_server.get_animal", return_value=animal), mock.patch(
            "server.dev_server.call_local_llm",
            side_effect=AssertionError("deterministic response must not call local model"),
        ):
            unknown_status, unknown = self.invoke_post(
                "/chat/local", {"animalId": "sensen", "message": "你会游泳吗？"}
            )
            off_status, off_domain = self.invoke_post(
                "/chat/local", {"animalId": "sensen", "message": "帮我解二次方程"}
            )

        self.assertEqual((unknown_status, off_status), (200, 200))
        self.assertEqual(unknown["evidenceStatus"], "insufficient_evidence")
        self.assertEqual(unknown["citations"], [])
        self.assertEqual(off_domain["answerMode"], "off_domain")
        self.assertEqual(off_domain["citations"], [])

    def test_social_chat_keeps_provider_reply_without_fake_citations(self):
        animal = animal_knowledge.load_animal_knowledge("sensen")
        with mock.patch("server.dev_server.get_animal", return_value=animal), mock.patch(
            "server.dev_server.call_local_llm",
            return_value=dev_server.ProviderResult(reply="我在这里陪着你。"),
        ):
            status, payload = self.invoke_post(
                "/chat/local", {"animalId": "sensen", "message": "我今天有点难过"}
            )

        self.assertEqual(status, 200)
        self.assertEqual(payload["reply"], "我在这里陪着你。")
        self.assertEqual(payload["answerMode"], "social_chat")
        self.assertEqual(payload["evidenceStatus"], "not_required")
        self.assertEqual(payload["citations"], [])


if __name__ == "__main__":
    unittest.main()
