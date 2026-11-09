# 第三方组件

- Tesseract .NET wrapper 5.2.0（Charles Weld）：Apache-2.0，https://github.com/charlesw/tesseract 。原生依赖 Tesseract 和 Leptonica 随 NuGet 包部署；Windows 需要 Microsoft Visual C++ 2015–2022 x64 Runtime。
- Tesseract OCR：Apache-2.0，https://github.com/tesseract-ocr/tesseract 。
- tessdata_fast 英文识别模型：Apache-2.0，https://github.com/tesseract-ocr/tessdata_fast 。模型版本及摘要见 `assets/tessdata/model-info.json`。
- Leptonica：BSD-style，https://github.com/DanBloomberg/leptonica 。
- .NET/WPF：Microsoft .NET，MIT 及其第三方声明；自包含发布中的运行库许可证随原始运行库保留。
- xUnit、Microsoft.NET.Test.Sdk、xUnit Visual Studio runner 仅用于开发测试，不随应用发布。
- 英译中模型：Argos `translate-en_zh-1_9`，原始 OPUS-MT 模型依据随包 README 为 CC-BY 4.0。作者 Jörg Tiedemann、Santhosh Thottingal；论文 “OPUS-MT — Building open translation services for the World”，EAMT 2020，Lisbon, Portugal。来源 https://argos-net.com/v1/translate-en_zh-1_9.argosmodel 及 https://github.com/Helsinki-NLP/OPUS-MT 。本项目抽取模型、词表和分词器，未修改权重；没有分发 Stanza 模型。保留原 README 与 CC-BY 4.0 全文，见 `licenses/`。
- CTranslate2 4.8.5：MIT，https://github.com/OpenNMT/CTranslate2 。
- SentencePiece 0.2.1：Apache-2.0，https://github.com/google/sentencepiece 。
- Python 3.13.17：Python Software Foundation License 及相关第三方许可，https://www.python.org/ 。使用项目私有嵌入包，不安装系统 Python。
- NumPy 2.2.6：BSD-3-Clause 及其 OpenBLAS 等二进制依赖许可，随包 `LICENSE.txt` 保留完整说明，https://numpy.org/ 。
- PyYAML 6.0.3：MIT，https://pyyaml.org/ 。

离线依赖精确 URL、版本和 SHA-256 见 `assets/offline/dependencies.json`（发布包中为 `offline/dependencies.json`）。依赖内的原始 `.dist-info` 许可与元数据随包保留。

完整许可证见 `licenses/`。本文件只记录依赖的授权，不替用户选择本项目自身的发布许可证。
