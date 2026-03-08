# Right Sidebar UI Redesign 设计方案（2026-03-08）

## 1. 最终选型

本次右侧栏重构采用：

`A1 + B2(吸收B1语义) + C2 + D1 + E1 + F2 + G1 + H1`

对应含义：

1. 保留 `MainContentGrid + GridSplitter` 右栏列模型。
2. 顶部模式区采用 icon-only ModeRail，并保留单选语义。
3. 模式状态采用 `IRightPanelModeViewModel` 子 VM 架构。
4. 移除旧 `Tools Panel (E)`/`Hide` 文本头部。
5. 动作区采用 `ModeActionDescriptor` 元数据渲染。
6. SFTP 导航采用独立 `SftpNavigationService`。
7. 右侧视觉 token 与左侧体系对齐。
8. SFTP 采用单栏 Explorer 结构（导航条 + 文件列表）。

## 2. 实施状态（已同步）

截至 2026-03-08，本方案已完成以下落地：

1. `RightSidebarHostView` 已接管右栏结构：ModeRail + DynamicActionBar + TransitioningContentControl。
2. 模式内容已拆分为独立视图：
   - `Views/RightModes/SnippetsModeView`
   - `Views/RightModes/HistoryModeView`
   - `Views/RightModes/SftpModeView`
3. `MainWindowViewModel` 已接入：
   - `RightPanelModes`
   - `ActiveRightMode`
   - 模式动作元数据（snippet/history/sftp）。
4. `SftpNavigationService` 已接入 DI，并完成：
   - `NavigateTo/GoBack/GoForward/GoUp/Refresh/TryResolveAddressInput`。
5. `SftpModeView` 已实现 Xftp 风格导航条：
   - `Back/Forward/Address/Refresh/Up`。
6. 样式与快捷键已落地：
   - `RightSidebarModeButton`
   - `RightSidebarActionButton`
   - `Ctrl+1/2/3` 模式切换。

## 3. 验证结论

1. 右侧相关增量测试已通过（RightPanel/RightSidebar/SftpMode）。
2. 全量测试已通过。
3. 解决方案构建通过（`dotnet build SkylarkTerminal.slnx`）。
