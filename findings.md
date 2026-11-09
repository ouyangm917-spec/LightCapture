# 技术核验

## 本机事实
- Windows build 26200.9711；显示版本 25H2。
- 全局 SDK：8.0.422；WindowsDesktop runtime 8.0.28。
- `D:\Dome\My_Projects\LightCapture` 原先不存在，已新建；相邻项目未修改。
- 父目录未发现适用 AGENTS.md；按本次用户提供的规则执行。

## 官方资料
- .NET 10 为 LTS，支持至 2028-11-14；.NET 8/9 支持至 2026-11-10。来源：https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core 。本次不沿用原材料中“.NET 8/9 都是 LTS”的表述。
- 下载元数据：https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json 。需校验 SDK 归档 SHA-512。
- Windows.Media.Ocr 对桌面应用的正式支持要求包身份：https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr 。不可承诺任意免安装 EXE 都能受支持地使用该 API。
- .NET 6+ WinRT 引用应使用 Windows-specific TFM，不再使用旧 Microsoft.Windows.SDK.Contracts 方案：https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-apis-desktop-apps 。
- WPF GifBitmapEncoder 支持多帧，但不支持 global/frame metadata；不能依赖该类写入帧 delay 和循环信息：https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.gifbitmapencoder?view=windowsdesktop-10.0 。
- GIF NETSCAPE 无限循环块：`21 FF 0B NETSCAPE2.0 03 01 00 00 00`；delay 单位 10 ms。微软官方读取示例：https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/Win7Samples/multimedia/wic/wicanimatedgif/WicAnimatedGif.cpp 。

## 已落实
- OCR 使用 Tesseract 5.2.0 wrapper 和 tessdata_fast 4.1.0 英文模型，原生识别样本通过。
- 用户选择默认离线翻译：Argos en→zh 1.9 模型，目标 token 为 `cmn_Hans`；CTranslate2 4.8.5 / SentencePiece 0.2.1，按需启动私有 Python 3.13.17 进程。没有在线翻译客户端和 API 配置。
- 模型来源：https://github.com/argosopentech/argospm-index 。实际下载 70,743,021 bytes；依赖和模型一起解压约 207.4 MiB。
- Python 发行文件：https://www.python.org/downloads/windows/ ，摘要取官方 Sigstore 发布元数据；EXE Authenticode 签名有效。
- WPF/GDI+ 独立解码测试和实际离线模型测试通过；合成帧完成录制落盘、修剪、缩放、变速导出与缓存回收。

## 尚待实机验收
- 用户要求停止前台自动化后已结束测试进程，不再操作鼠标、键盘或焦点；完整真实 GUI 操作链尚未验收。
- 多显示器混合 DPI、HDR 及受保护窗口需要实机环境单独验收。
