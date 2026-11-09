"""验证长输入不会在拆分时漏词；无需加载翻译模型。"""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("worker", Path(__file__).parents[1] / "assets/offline/translate.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class WordTokenizer:
    def encode(self, text, out_type=str):
        return ["▁" + word for word in text.split()]


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


if __name__ == "__main__":
    unittest.main()
