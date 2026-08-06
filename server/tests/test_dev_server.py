import unittest

from server import dev_server


SENSEN = {
    "id": "sensen",
    "name": "缨冠灰叶猴",
    "nickname": "森森",
    "food": ["嫩叶", "果实", "花朵"],
    "threats": ["栖息地破坏", "非法捕猎"],
    "protectionActions": ["保护森林栖息地", "传播正确保护知识"],
}


class DevServerTests(unittest.TestCase):
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


if __name__ == "__main__":
    unittest.main()
