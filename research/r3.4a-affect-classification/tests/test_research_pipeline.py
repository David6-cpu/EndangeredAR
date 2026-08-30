from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest

import numpy as np
import torch

from r34a_affect.data import load_gold, remove_contamination
from r34a_affect.evaluation import evaluate_greeting_gate, tune_greeting_gate
from r34a_affect.exporting import export_and_verify
from r34a_affect.models import DualHeadBiLSTM, DualHeadTextCNN
from r34a_affect.schema import AffectExample
from r34a_affect.tokenizer import CharacterTokenizer, SPECIAL_TOKENS


def example(
    source_id: str,
    user: str,
    reply: str,
    dialogue: str = "Neutral",
    emotion: str = "Neutral",
) -> AffectExample:
    return AffectExample(source_id, source_id, user, reply, dialogue, emotion)


class CharacterTokenizerTests(unittest.TestCase):
    def test_special_token_ids_are_fixed(self) -> None:
        tokenizer = CharacterTokenizer.build(
            [example("one", "用户", "回复")], 64, 12, "user_reply_pair"
        )
        self.assertEqual(
            {token: index for index, token in enumerate(SPECIAL_TOKENS)},
            {token: tokenizer.token_to_id[token] for token in SPECIAL_TOKENS},
        )

    def test_reply_only_vocab_does_not_read_user_text(self) -> None:
        tokenizer = CharacterTokenizer.build(
            [example("one", "独有用户字", "普通回复")], 64, 12, "reply_only"
        )
        self.assertNotIn("独", tokenizer.token_to_id)
        self.assertIn("回", tokenizer.token_to_id)

    def test_pair_truncation_preserves_reply_and_user_tail(self) -> None:
        tokenizer = CharacterTokenizer.build(
            [example("one", "甲乙丙丁戊", "回复")], 64, 8, "user_reply_pair"
        )
        encoded = tokenizer.encode("甲乙丙丁戊", "回复", "user_reply_pair")
        self.assertEqual(8, len(encoded))
        self.assertEqual(tokenizer.token_to_id["<USER>"], int(encoded[0]))
        self.assertEqual(tokenizer.token_to_id["丙"], int(encoded[1]))
        self.assertEqual(tokenizer.token_to_id["丁"], int(encoded[2]))
        self.assertEqual(tokenizer.token_to_id["戊"], int(encoded[3]))
        self.assertEqual(tokenizer.token_to_id["<ASSISTANT>"], int(encoded[5]))
        self.assertEqual(tokenizer.token_to_id["回"], int(encoded[6]))

    def test_pair_with_long_reply_drops_user_before_reply(self) -> None:
        tokenizer = CharacterTokenizer.build(
            [example("one", "用户", "甲乙丙丁戊己庚辛")], 64, 6, "user_reply_pair"
        )
        encoded = tokenizer.encode("用户", "甲乙丙丁戊己庚辛", "user_reply_pair")
        self.assertEqual(tokenizer.token_to_id["<ASSISTANT>"], int(encoded[0]))
        self.assertEqual(tokenizer.token_to_id["甲"], int(encoded[1]))
        self.assertNotIn(tokenizer.token_to_id["<USER>"], encoded.tolist())

    def test_unknown_character_maps_to_unk(self) -> None:
        tokenizer = CharacterTokenizer.build(
            [example("one", "", "已知")], 64, 6, "reply_only"
        )
        encoded = tokenizer.encode("", "陌", "reply_only")
        self.assertEqual(tokenizer.unk_id, int(encoded[1]))


class DataBoundaryTests(unittest.TestCase):
    def test_contamination_filter_protects_gold_and_held_out_splits(self) -> None:
        gold = [example("gold", "你好", "欢迎回来")]
        splits = {
            "train": [
                example("train-overlap-gold", "别的", "欢迎回来！"),
                example("train-overlap-dev", "甲", "共享文本"),
                example("train-safe", "甲", "训练安全文本"),
            ],
            "dev": [example("dev", "乙", "共享文本"), example("dev-safe", "乙", "开发安全文本")],
            "test": [example("test", "丙", "测试安全文本")],
        }
        filtered, removed = remove_contamination(splits, gold)
        self.assertEqual(["train-safe"], [item.source_id for item in filtered["train"]])
        self.assertEqual(2, removed["train"])
        self.assertEqual(0, removed["dev"])

    def test_gold_rejects_non_project_source(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gold.jsonl"
            path.write_text(
                json.dumps(
                    {
                        "id": "bad",
                        "user": "x",
                        "reply": "y",
                        "dialogue_act": "Neutral",
                        "emotion_tone": "Neutral",
                        "source": "device_log",
                    }
                ),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "unapproved source"):
                load_gold(path)


class ModelContractTests(unittest.TestCase):
    def test_dual_head_shapes(self) -> None:
        inputs = torch.ones((2, 12), dtype=torch.long)
        for model in (
            DualHeadTextCNN(32, 6, 8, embedding_dimension=8, channels_per_kernel=4),
            DualHeadBiLSTM(32, 6, 8, embedding_dimension=8, hidden_dimension=4),
        ):
            dialogue, emotion = model(inputs)
            self.assertEqual((2, 6), tuple(dialogue.shape))
            self.assertEqual((2, 8), tuple(emotion.shape))

    def test_textcnn_and_bilstm_export_with_logit_parity(self) -> None:
        sample = np.ones((1, 12), dtype=np.int64)
        with tempfile.TemporaryDirectory() as directory:
            for name, model in (
                ("textcnn", DualHeadTextCNN(32, 6, 8, embedding_dimension=8, channels_per_kernel=4)),
                ("bilstm", DualHeadBiLSTM(32, 6, 8, embedding_dimension=8, hidden_dimension=4)),
            ):
                result = export_and_verify(
                    model,
                    sample,
                    Path(directory) / f"{name}.onnx",
                    17,
                    ("dialogue_logits", "emotion_logits"),
                )
                self.assertTrue(result["parityPassed"])
                self.assertLessEqual(result["maxAbsoluteLogitError"], 1e-4)

    def test_greeting_gate_prefers_precision(self) -> None:
        class_names = ("Neutral", "Greeting", "Other")
        labels = np.asarray([1, 1, 1, 0, 0, 2])
        logits = np.asarray(
            [
                [0.0, 5.0, 0.0],
                [0.0, 4.0, 0.0],
                [0.0, 3.0, 0.0],
                [0.0, 0.2, 0.0],
                [3.0, 0.0, 0.0],
                [0.0, 0.0, 3.0],
            ]
        )
        gate = tune_greeting_gate(logits, labels, class_names, 1.0)
        result = evaluate_greeting_gate(logits, labels, class_names, 1.0, gate)
        self.assertEqual(1.0, result["precision"])
        self.assertEqual(0, result["falsePositive"])


if __name__ == "__main__":
    unittest.main()
