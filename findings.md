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

## 尚待确认
- OCR 最终采用可独立部署的本地引擎，还是提供有身份的包；需要兼顾免安装和体积。
- 真实翻译提供方/密钥未提供；采用配置界面和 OpenAI-compatible 适配器，不读取其他项目凭证。
- 多显示器混合 DPI、HDR 及受保护窗口需要实机环境单独验收。
