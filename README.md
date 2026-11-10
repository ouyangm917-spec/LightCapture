# 轻截 / LightCapture

Windows 原生截图、步骤标注、离线英译中与区域 GIF。日常常驻托盘：**快捷键 → 框选 → 工具栏编辑 → Enter 复制 → 粘贴**。

## 直接使用

当前版本为 **0.4.0**，支持多张置顶贴图；框选时不再显示中央操作提示，Esc 仍可退出截图。

从 [GitHub Releases](https://github.com/ouyangm917-spec/LightCapture/releases/tag/v0.4.0) 下载附带离线语言包的完整 ZIP。应用图标源文件与生成记录见 `assets/icon/`。

解压完整的 `LightCapture-0.4.0-win-x64.zip`，运行 `LightCapture.exe`。保留同目录的 DLL、`tessdata`、`offline` 和 `licenses` 文件夹。发布包自带 .NET 运行库及离线英文识别、简体中文翻译模型，不需要安装 Python 或配置 API Key。更新前请自行从托盘退出旧版，再运行新版。

默认快捷键 `Alt+A`；若被占用，程序尝试 `Ctrl+Alt+A`。右击托盘图标可截图、打开设置或退出。如果两个快捷键都被占用，可先从托盘截图，再调整冲突应用的快捷键。再次运行程序也会唤起截图。

| 操作 | 使用方式 |
| --- | --- |
| 框选区域 | 按快捷键后拖动鼠标 |
| 移动工具栏 | 拖动左侧六点手柄或工具栏空白处；当前截图内保留手动位置 |
| 标注 | 矩形 R、椭圆 O、箭头 A、画笔 P、文字 T、马赛克 M |
| 步骤编号 | S，连续点击添加 1、2、3；删除中间编号时保留其余编号 |
| 移动/修改 | V，选中图元后拖动；工具栏改色、改粗细；文字双击编辑 |
| 删除 | Delete / Backspace |
| 撤销/重做 | Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z |
| 完成并复制 | Enter / Ctrl+C；然后在目标应用 Ctrl+V |
| 保存 PNG | 工具栏 ↓ / Ctrl+S |
| 钉住截图（0.4.0） | 工具栏图钉 / Ctrl+P；每次新增一张置顶贴图，可同时保留多张 |
| 操作贴图（0.4.0） | 拖动图片移动；右键复制 / Ctrl+C；悬停右上角 ×、右键关闭或 Esc 关闭当前张 |
| 贴图缩放（源码 0.4.1） | 鼠标移到贴图上，滚轮向上放大、向下缩小，范围 10%–400%；复制仍使用原图 |
| 取消 | Esc / 选区上右键 |
| 离线英译中 | 工具栏翻译图标；中文直接覆盖在当前截图的英文位置，再点切换原图/译图；Enter 复制当前图片 |
| GIF | 工具栏 GIF，录制当前选区；停止后在大预览页播放/定位，右侧修剪首尾、调整宽度/速度，底部导出 |

GIF 默认 15 fps、最长 60 秒、最大录制宽度 1280；实际帧间隔随机器负载变化，导出使用实际采样时间。按顺序写入磁盘缓存，上限 512 MiB。关闭预览后删除本次录制缓存；程序异常退出时可能保留缓存，可在程序退出后手动清理 `%LocalAppData%\LightCapture\recordings`。

工具栏沿用 HTML 原型的白色单排与绿色选中样式，默认选择工具；使用标注工具或选中标注后才显示颜色和粗细。重做按钮在有可重做操作时显示，快捷键始终可用。

工具栏可在当前截图内自由拖动，展开颜色、显示翻译状态不会把它拉回选区旁；靠近边缘时只做屏幕内约束。新截图仍自动放在选区附近。

GIF 预览区提供播放/暂停和播放位置条；右侧显示修剪范围、输出尺寸和速度。导出中显示进度并锁定参数，点击“取消并关闭”可取消导出并回收本次录制缓存。缩小窗口时设置区可以滚动，导出按钮保持可见。

翻译只处理当前选区的原始截图，识别英文位置后批量离线翻译，并在对应区域绘制中文；已有标注保留为独立图层，可继续修改。翻译中可取消，失败保留原图，不再打开独立翻译窗口。复制/保存使用当前显示的原图或译图加标注。

离线模型仅在翻译时加载，结束后释放子进程。首次启动翻译比单纯截图更耗时。文字背景依据周围颜色填充，纯色界面效果较好；复杂背景不能恢复被原文字遮挡的图像细节。长译文会缩小以适应原位置；小字、专业术语、复杂排版仍需人工判断。

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
pwsh -File scripts/package.ps1
```

源码固定 .NET SDK 10.0.403。离线依赖来源、版本、SHA-256 固定在 `assets/offline/dependencies.json`；英文 OCR 模型固定在 `assets/tessdata/model-info.json`。为保持源码仓库大小，翻译模型和嵌入运行库不提交到 Git，由 `setup-offline.ps1` 准备。

发布目录按项目版本区分，当前为 `artifacts/LightCapture-0.4.0-win-x64`，保留此前交付目录。已恢复依赖时可给构建脚本添加 `-NoRestore`。脚本使用单个 MSBuild 节点，避免同时启动大量构建进程；本地 SDK 与依赖缓存位于 `.tools`，供后续增量构建使用。

无前台窗口的原生验证：

```powershell
artifacts/LightCapture-0.4.0-win-x64/LightCapture.exe --self-test --output artifacts/verification-0.4.0
.tools/offline/python/python.exe -I -B tests/offline_tests.py
```

`--self-test` 仅生成合成图片和 GIF，执行本地 OCR/翻译及真实工具栏的离屏渲染，不创建可见窗口、不截取桌面、不注册快捷键、不操作剪贴板。需要手工验收时可自行使用 `--demo` 打开样本窗口；开发代理不会自动打开。

## 结构

- `src/LightCapture.Core`：图元/历史、像素几何、录制时间轴与 GIF 容器。
- `src/LightCapture.App`：WPF 工具栏、Windows 截图/托盘/热键、剪贴板、OCR、离线翻译与 GIF 预览。
- `assets/offline`：固定依赖清单、单次离线翻译进程。
- `tests`：有风险的逻辑及长文本拆分测试。
- `docs`：实施方案、验证记录和原始 HTML 原型。

第三方组件及模型归属见 `THIRD_PARTY_NOTICES.md` 和 `licenses/`。
