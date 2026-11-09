# LightCapture 0.3.0 交付

程序：`D:\Dome\My_Projects\LightCapture\artifacts\LightCapture-0.3.0-win-x64\LightCapture.exe`。

完整压缩包：`C:\Users\ikunm\Documents\Codex\2026-11-09\g\outputs\LightCapture-0.3.0-win-x64.zip`。

先从托盘退出旧版，再运行新版。开发代理没有替用户启动或退出程序。

## 本轮修改
- 工具栏可拖动左侧六点手柄或空白处；按钮仍正常点击，手动位置不会因展开样式或翻译状态而回弹。
- GIF 预览改为大预览、右侧参数、底部操作；统一滑块和下拉框样式，支持播放定位，保留修剪、宽度、速度与循环导出。
- 导出中显示进度并锁定参数，可取消并关闭；小窗口设置区可滚动，导出按钮始终可见。
- 截图内翻译、步骤编号、离线语言包继续保留。

## 验证与完整性
- 20 项核心、7 项 Python、12 项原生后台检查通过；Release 发布成功。
- 双轴独立审查未发现确认问题。已查看真实 WPF 控件离屏图，并验证滑块与滚动条的内存路由事件。
- 未操作真实桌面、键鼠、剪贴板或用户窗口；实际系统鼠标拖动和混合 DPI 仍未前台验收。
- 发布代码：`4259d5c2bdc55e43cc7deacd27e6dd9cd6ed4bb0`。
- ZIP：200,411,141 bytes；SHA-256 `de73edec175604180887d9b0aff0fb4dcb0f30bfb6e8706dafa721888ada70aa`。
- 1514 个归档文件与发布目录的 SHA-256 全部一致；旧版保留。
