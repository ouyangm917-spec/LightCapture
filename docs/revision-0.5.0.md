# 0.5.0 当前用户安装版

## 需求与范围

提供正式安装程序，安装时能选择开机自启动。固定安装到 `%LocalAppData%\Programs\LightCapture`，支持开始菜单、可选桌面快捷方式、Windows 已安装的应用中卸载。保持现有截图、贴图、离线语言包、GIF 和内存优化。

安装器和卸载器使用 NSIS `RequestExecutionLevel user`；应用继续使用 `asInvoker`，不请求管理员权限。安装过程不启动应用、不结束用户进程，不修改系统安全策略、下载文件来源标记或旧目录权限。程序文件占用时要求用户自行退出后重试。

开机自启动默认勾选，升级保持当前开关选择；勾选安装后写入当前用户的 `Run\LightCapture`，命令指向固定安装位置并带 `--autostart`。取消勾选移除此应用启动值。卸载仅在启动命令仍指向本安装目录时删除，保留用户数据目录和未知文件。Windows 任务管理器禁用策略不会被重置。

## 已确认现场信息

检查时，当前用户的启动项仍指向下载目录中的 0.4.3。0.4.4 EXE 未签名，源码清单已为普通权限。未查到 LightCapture 路径的兼容性提权标记；用户截图中的 UAC 具体触发方式尚未确认，不能归因于源码要求管理员。

SmartScreen 与 UAC 不同。制作安装包不等于获得签名/信誉；当前没有签名证书，不承诺下载后首次运行安装器时不会提示。不会关闭 SmartScreen 或添加伪造信任。

## 构建与测试

NSIS 3.13 便携编译器仅解压至项目 `.tools`。`scripts/setup-installer.ps1` 固定官方下载地址及 SHA-256；`scripts/build-installer.ps1` 生成安装包及逐文件卸载清单，并核对发布元数据。

隔离测试版使用相同安装脚本，替换安装目录、快捷方式目录和注册表根；不接触真实启动项、桌面或开始菜单。生产安装器不支持运行时切换到测试模式。真实注销/登录及用户交互安装不在后台测试范围内。

参考：[NSIS 下载](https://nsis.sourceforge.io/Download)、[脚本与权限参数](https://nsis.sourceforge.io/Docs/Chapter4.html)、[Microsoft Run 启动项](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)。
