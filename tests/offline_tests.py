"""验证长输入不会在拆分时漏词；无需加载翻译模型。"""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch
from types import SimpleNamespace

spec = importlib.util.spec_from_file_location("worker", Path(__file__).parents[1] / "assets/offline/translate.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class WordTokenizer:
    def encode(self, text, out_type=str):
        return ["▁" + word for word in text.split()]

    def decode(self, pieces):
        return " ".join(piece.removeprefix("▁") for piece in pieces)


class ChunkingTests(unittest.TestCase):
    def test_long_text_keeps_every_word_in_order(self):
        words = [f"word{i}" for i in range(1200)]
        chunks = worker.token_chunks(" ".join(words), WordTokenizer())
        self.assertTrue(all(0 < len(chunk) <= 240 for chunk in chunks))
        self.assertEqual([piece for chunk in chunks for piece in chunk], ["▁" + word for word in words])

    def test_sentences_preserve_numbers_and_identifiers(self):
        chunks = worker.token_chunks("Step 1. Open foo_bar.exe! Keep version 2.4.", WordTokenizer())
        self.assertEqual([p for chunk in chunks for p in chunk], WordTokenizer().encode("Step 1. Open foo_bar.exe! Keep version 2.4."))

    def test_empty_line_does_not_make_empty_inference_batch(self):
        self.assertEqual([], worker.token_chunks("   ", WordTokenizer()))

    def test_model_spacing_marker_removed_without_changing_identifiers(self):
        self.assertEqual("第三步:复制 foo_bar.exe。", worker.clean_translation("▁第三步:复制 foo_bar.exe 。"))


class BatchTests(unittest.TestCase):
    def test_batch_keeps_region_boundaries_and_line_breaks(self):
        class EchoTranslator:
            def translate_batch(self, chunks, **options):
                return [SimpleNamespace(hypotheses=[chunk]) for chunk in chunks]

        texts = ["Save", "Step 1.\n\nOpen the file!", "Cancel", " ".join(f"word{i}" for i in range(900))]
        with patch.object(worker, "load_models", return_value=(WordTokenizer(), EchoTranslator())) as load:
            self.assertEqual(texts, worker.translate_many(texts, Path("unused")))
            load.assert_called_once()

    def test_legacy_and_batch_requests(self):
        self.assertEqual((["Save"], False), worker.parse_request({"text": "Save"}))
        self.assertEqual((["Save", "Cancel"], True), worker.parse_request({"texts": ["Save", "Cancel"]}))

    def test_invalid_batches_rejected_before_loading_model(self):
        for value in [[], None, {"texts": []}, {"texts": "Save"}, {"texts": ["Save", ""]},
                      {"texts": [1]}, {"texts": ["x"] * 513}, {"texts": ["x" * 6001, "y" * 6000]}]:
            with self.subTest(value=str(value)[:60]), self.assertRaises(ValueError):
                worker.parse_request(value)


if __name__ == "__main__":
    unittest.main()
