# GodotSteam for GDExtension | 社区版

适用于 [Godot 引擎](https://godotengine.org) 和 [Valve 的 Steam](https://store.steampowered.com) 的工具生态系统。支持 Windows、Linux 和 Mac 平台。

## 其他版本

| 标准模块 | 标准插件 | 服务器模块 | 服务器插件 | 示例 |
| --- | --- | --- | --- | --- |
| [Godot 2.x](https://codeberg.org/godotsteam/godotsteam/src/branch/godot2) | [GDNative](https://codeberg.org/godotsteam/godotsteam/src/branch/gdnative) | [Server 3.x](https://codeberg.org/godotsteam/godotsteam-server/src/branch/godot3) | [GDNative](https://codeberg.org/godotsteam/godotsteam-server/src/branch/gdnative) | [Skillet](https://codeberg.org/godotsteam/skillet) |
| [Godot 3.x](https://codeberg.org/godotsteam/godotsteam/src/branch/godot3) | [GDExtension](https://codeberg.org/godotsteam/godotsteam/src/branch/gdextension) | [Server 4.x](https://codeberg.org/godotsteam/godotsteam-server/src/branch/godot4) | [GDExtension](https://codeberg.org/godotsteam/godotsteam-server/src/branch/gdextension) | [Skillet UGC Editor](https://codeberg.org/godotsteam/skillet/src/branch/ugc_editor) |
| [Godot 4.x](https://codeberg.org/godotsteam/godotsteam/src/branch/godot4) | --- | --- | --- | --- |
| [MultiplayerPeer](https://codeberg.org/godotsteam/multiplayerpeer)| --- | --- | --- | --- |

## 文档

[文档在此处](https://godotsteam.com/)。您也可以在 Godot 引擎内查看搜索帮助部分。[首先，可以尝试查看我们的 Steam 初始化教程。](https://godotsteam.com/tutorials/initializing/) 还有更多教程正在制作中。您也可以[在此处查看与 Godot 和 Steam 相关的其他视频、文本、额外工具、插件等资源。](https://godotsteam.com/resources/external/)

欢迎在 [Stoat 服务器](https https://stt.gg/9DxQ3Dcd) 或 [Libera Chat 的 IRC 频道](irc://irc.libera.chat/#godotsteam) 上与我们交流 GodotSteam 或寻求帮助。

## 捐赠

提交 Pull Request 是帮助项目最好的方式，但您也可以通过 [Github Sponsors](https://github.com/sponsors/Gramps) 或 [LiberaPay](https://liberapay.com/godotsteam/donate) 进行捐赠！[您可以在此处了解更多关于捐赠者福利的信息。](https://godotsteam.com/contribute/donations/) [您也可以在此处查看我们所有了不起的捐赠者。](https://godotsteam.com/contribute/donors/)

## 当前构建版本

您可以[在此处下载此仓库的预编译版本](https://codeberg.org/godotsteam/godotsteam/releases)。

**版本 4.17.1 变更**

- 更改：SCsub 和 config.py 以支持 ARM64 和 Android
- 修复：disconnect_peer 中的潜在崩溃问题；感谢 ***bearlikelion***

[您可以在此处阅读更多变更日志。](https://godotsteam.com/changelog/gdextension/)

## 兼容性

虽然很少见，但有时 Steamworks SDK 更新会破坏与旧版 GodotSteam 的兼容性。任何兼容性中断都会在下方注明。较新的 API 文件（dll、so、dylib）_应该_ 仍适用于旧版本。

| Steamworks SDK 版本 | GodotSteam 版本 |
| --- | --- |
| 1.63 或更新版本 | 4.17 |
| 1.62 | 4.14 或 4.16.2 |
| 1.61 | 4.12 到 4.13 |
| 1.60 | 4.6 到 4.11 |
| 1.59 | 4.6 到 4.8 |
| 1.58a 或更旧版本 | 4.5.4 或更旧版本 |

引入兼容性中断的 GodotSteam 版本：

| GodotSteam 版本 | 中断的兼容性 |
| --- | --- |
| 4.8 | 移除了网络身份系统，替换为 Steam ID |
| 4.9 | sendMessages 返回一个数组 |
| 4.11 | 移除了 setLeaderboardDetailsMax |
| 4.13 | getItemDefinitionProperty 返回字典，html_needs_paint 键 'bgra' 更改为 'rbga' |
| 4.14 | 移除了 steamInit 和 steamInitEx 中统计请求的第一个参数，steamInit 返回预期的布尔值 |
| 4.16 | 各种小的中断点，请参阅 [4.16 变更日志了解详情](https://godotsteam.com/changelog/godot4/) |
| 4.17 | 使用 Steam SDK 1.63 的 Windows 项目旨在与 Linux / Steam Deck 上的 Proton 11 或 Experimental 配合使用。 |

## 已知问题

- 4.4 的 GDExtension **不** 兼容 4.3.x 或更低版本。请检查您使用的版本。
- 覆盖层在编辑器中无法工作，但在导出并上传到 Steam 的项目中可以工作。这似乎是 Vulkan 目前的限制。

## 快速指南

有关如何构建 GDExtension 版本 GodotSteam 的完整说明，[请参考我们文档中的 'How-To GDExtension' 部分。](https://godotsteam.com/howto/gdextension/) 它将包含最新信息。

或者，您可以直接[从我们的发布页面下载预编译版本](https://codeberg.org/godotsteam/godotsteam/releases) 或 [从 Godot 资源库下载](https://godotengine.org/asset-library/asset/2445)，跳过自行编译的步骤！

## 使用方法

不要将 GDExtension 版本的 GodotSteam 与任何模块版本一起使用，无论是我们的预编译版本还是您自己编译的版本。它们彼此不兼容。

使用 GDExtension 版本导出时，请使用普通的 Godot 引擎模板，而不是我们的 GodotSteam 模板，否则会出现很多问题。

## 许可证

MIT 许可证