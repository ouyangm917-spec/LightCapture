"""本地单次翻译进程：从 stdin 读 JSON，向 stdout 写 JSON，不访问网络。"""

import json
import os
from pathlib import Path
import re
import sys


def token_chunks(text, tokenizer, limit=240):
    """按句和 token 边界拆分，避免引擎静默截掉长段落。"""
    sentences = re.split(r"(?<=[.!?])\s+", text.strip())
    chunks = []
    for sentence in sentences:
        tokens = tokenizer.encode(sentence, out_type=str)
        while tokens:
            split = min(limit, len(tokens))
            if split < len(tokens):
                # 尽量不在单词内部切开 SentencePiece 子词。
                for index in range(split - 1, max(0, split - 40), -1):
                    if tokens[index].startswith("▁"):
                        split = index
                        break
            chunks.append(tokens[:split])
            tokens = tokens[split:]
    return chunks


def load_models(model_root):
    import ctranslate2
    import sentencepiece

    tokenizer = sentencepiece.SentencePieceProcessor(model_file=str(model_root / "sentencepiece.model"))
    translator = ctranslate2.Translator(
        str(model_root / "model"), device="cpu", compute_type="int8",
        inter_threads=1, intra_threads=min(4, max(1, (os.cpu_count() or 2) // 2)),
    )
    return tokenizer, translator


def clean_translation(text):
    # 模型偶尔输出多余的 SentencePiece 空格标记；不改动标识符中的普通下划线。
    return re.sub(r"\s+([，。！？；：、）】》])", r"\1", text.replace("▁", " ")).strip()


def translate_text(text, tokenizer, translator):
    output = []
    for line in text.replace("\r\n", "\n").replace("\r", "\n").split("\n"):
        if not line.strip():
            output.append("")
            continue
        chunks = token_chunks(line, tokenizer)
        results = translator.translate_batch(
            chunks, beam_size=2, max_batch_size=4, max_input_length=0,
            max_decoding_length=512, repetition_penalty=1.05,
        )
        translated = []
        for result in results:
            pieces = result.hypotheses[0]
            if len(pieces) >= 512:
                raise ValueError("translation output limit reached")
            translated.append(tokenizer.decode(pieces).strip())
        output.append(clean_translation(" ".join(translated)))
    return "\n".join(output).strip()


def translate_many(texts, model_root):
    # 一张截图只加载一次模型；结果顺序与 OCR 区域严格一致。
    tokenizer, translator = load_models(model_root)
    return [translate_text(text, tokenizer, translator) for text in texts]


def parse_request(request):
    if not isinstance(request, dict):
        raise ValueError("invalid request")
    batch = "texts" in request
    texts = request.get("texts") if batch else [request.get("text")]
    if not isinstance(texts, list) or not 1 <= len(texts) <= 512:
        raise ValueError("invalid batch length")
    if any(not isinstance(text, str) or not text.strip() for text in texts):
        raise ValueError("invalid text")
    if sum(len(text) for text in texts) > 12_000:
        raise ValueError("invalid input length")
    return texts, batch


def main():
    sys.stdin.reconfigure(encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8")
    request = json.loads(sys.stdin.read(100_000))
    texts, batch = parse_request(request)
    model_root = Path(sys.argv[1]).resolve(strict=True)
    results = translate_many(texts, model_root)
    print(json.dumps({"translations": results} if batch else {"translation": results[0]}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        # stderr 不记录原文、用户路径或完整堆栈。
        print(type(error).__name__, file=sys.stderr)
        sys.exit(1)
