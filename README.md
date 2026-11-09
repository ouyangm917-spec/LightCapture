# 轻截 / LightCapture

Windows 原生截图、步骤标注、离线英译中与区域 GIF。日常常驻托盘：**快捷键 → 框选 → 工具栏编辑 → Enter 复制 → 粘贴**。

## 直接使用

解压完整的 `LightCapture-win-x64.zip`，运行 `LightCapture.exe`。保留同目录的 DLL、`tessdata`、`offline` 和 `licenses` 文件夹。发布包自带 .NET 运行库及离线英文识别、简体中文翻译模型，不需要安装 Python 或配置 API Key。

默认快捷键 `Alt+A`；若被占用，程序尝试 `Ctrl+Alt+A`。右击托盘图标可截图、打开设置或退出。如果两个快捷键都被占用，可先从托盘截图，再调整冲突应用的快捷键。再次运行程序也会唤起截图。

| 操作 | 使用方式 |
| --- | --- |
| 框选区域 | 按快捷键后拖动鼠标 |
| 标注 | 矩形 R、椭圆 O、箭头 A、画笔 P、文字 T、马赛克 M |
| 步骤编号 | S，连续点击添加 1、2、3；删除中间编号时保留其余编号 |
| 移动/修改 | V，选中图元后拖动；工具栏改色、改粗细；文字双击编辑 |
| 删除 | Delete / Backspace |
| 撤销/重做 | Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z |
| 完成并复制 | Enter / Ctrl+C；然后在目标应用 Ctrl+V |
| 保存 PNG | 工具栏 ↓ / Ctrl+S |
| 取消 | Esc / 选区上右键 |
| 离线英译中 | 工具栏“译”；原文可修正后重新翻译，译文可复制 |
| GIF | 工具栏 GIF，录制当前选区；点击停止或按截图快捷键；预览中修剪首尾、调整宽度/速度并导出 |

GIF 默认 15 fps、最长 60 秒、最大录制宽度 1280；实际帧间隔随机器负载变化，导出使用实际采样时间。按顺序写入磁盘缓存，上限 512 MiB。关闭预览后删除本次录制缓存；程序异常退出时可能保留缓存，可在程序退出后手动清理 `%LocalAppData%\LightCapture\recordings`。

离线模型仅在翻译时加载，结束后释放子进程。首次启动翻译比单纯截图更耗时。普通短句已实测；专业术语、复杂排版和长句的翻译仍需人工判断。

## 运行要求与当前验证边界

- Windows 10/11 x64；本机测试环境 build 26200.9711。
- OCR 与翻译的原生依赖需要 Microsoft Visual C++ 2015–2022 x64 运行库。本机已安装；未在干净 Windows 虚拟机上验证安装流程。[微软官方下载说明](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)
- 已通过核心逻辑测试和无界面的原生合成数据验证，详见 `docs/verification.md`。
- 应用户要求停止前台自动化：完整的快捷键→框选→编辑→粘贴，以及真实桌面区域录制的 GUI 验收未完成。不会在后台测试中自动操作用户键鼠或切换窗口。
- 多显示器混合 DPI、HDR、受保护视频/窗口尚未覆盖。受保护内容可能截为黑色；不绕过系统保护。
- 当前版本没有代码签名。请从你自己的构建或本次交付来源运行。

## 从源码构建

使用 PowerShell 7。首次准备需要网络；依赖保存在项目 `.tools`，不更改系统 SDK/PATH/Python。

```powershell
pwsh -File scripts/setup-sdk.ps1
pwsh -File scripts/setup-offline.ps1
pwsh -File scripts/build.ps1 -Task Build
pwsh -File scripts/build.ps1 -Task Test
pwsh -File scripts/build.ps1 -Task Publish
```

源码固定 .NET SDK 10.0.403。离线依赖来源、版本、SHA-256 固定在 `assets/offline/dependencies.json`；英文 OCR 模型固定在 `assets/tessdata/model-info.json`。为保持源码仓库大小，翻译模型和嵌入运行库不提交到 Git，由 `setup-offline.ps1` 准备。

发布目录为 `artifacts/LightCapture-win-x64`。已恢复依赖时可给构建脚本添加 `-NoRestore`。脚本使用单个 MSBuild 节点，避免同时启动大量构建进程。

无前台窗口的原生验证：

```powershell
artifacts/LightCapture-win-x64/LightCapture.exe --self-test --output artifacts/native-checks
.tools/offline/python/python.exe -I -B tests/offline_tests.py
```

`--self-test` 仅生成合成图片和 GIF，执行本地 OCR/翻译，不截取桌面、不注册快捷键、不操作剪贴板。需要手工验收时可自行使用 `--demo` 打开样本窗口；开发代理不会自动打开。

## 结构

- `src/LightCapture.Core`：图元/历史、像素几何、录制时间轴与 GIF 容器。
- `src/LightCapture.App`：WPF 工具栏、Windows 截图/托盘/热键、剪贴板、OCR、离线翻译与 GIF 预览。
- `assets/offline`：固定依赖清单、单次离线翻译进程。
- `tests`：有风险的逻辑及长文本拆分测试。
- `docs`：实施方案、验证记录和原始 HTML 原型。

第三方组件及模型归属见 `THIRD_PARTY_NOTICES.md` 和 `licenses/`。
