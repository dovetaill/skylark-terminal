# 2026-03-09 RightSidebar Layout Bugfix2 TDD Spec

## 目的

本文件作为 `RightSidebar Layout Bugfix2` 落地后的 TDD 交接输入，供下一阶段补强自动化测试使用。范围覆盖：

- mode rail 语义图标与中文 tooltip metadata
- `SFTP` 模式级加载状态机与地址交互
- `SFTP` 固定 `Grid` toolbar 与 `More` pure-action flyout
- `SFTP` 内容区状态模板与双层 row 文件列表

非目标：

- 不覆盖 `SshTerminalPane`
- 不覆盖 `RowStripedTerminalView`
- 不扩展真实上传/下载/重命名/权限等 `SFTP` 业务操作

## 当前实现基线

### 核心模型与状态

- `src/SkylarkTerminal/Models/RightModeIconKey.cs`
  - mode rail 语义 icon key
- `src/SkylarkTerminal/Models/RightModeIconCatalog.cs`
  - icon key 到 glyph 映射
- `src/SkylarkTerminal/Models/RightToolsModeItem.cs`
  - `TitleZh`
  - `TooltipZh`
  - `IconKey`
  - `Glyph`
- `src/SkylarkTerminal/Models/ModeActionDescriptor.cs`
  - `LabelZh`
  - `TooltipZh`
- `src/SkylarkTerminal/Models/SftpToolbarActionDescriptor.cs`
  - `LabelZh`
  - `TooltipZh`
- `src/SkylarkTerminal/Models/SftpPanelLoadState.cs`
  - `Idle`
  - `Loading`
  - `Loaded`
  - `Empty`
  - `Error`
- `src/SkylarkTerminal/Models/RemoteFileNode.cs`
  - `IconGlyph`
  - `KindLabelZh`
  - `SizeLabel`

### 核心 ViewModel

- `src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`
  - `RightToolsModeItems`
  - `ShowSftpTools()`
  - `ActivateSftpModeAsync()`
  - `InitializeRightPanelModes()`
  - `ActiveRightMode`
  - `ActiveRightHeader`
- `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
  - `Items`
  - `LoadState`
  - `ErrorMessage`
  - `ActivateAsync(string connectionId)`
  - `CommitAddressAsync()`
  - `LeadingCommands`
  - `TrailingCommands`
  - `MoreCommands`
  - `BackCommand`
  - `ForwardCommand`
  - `RefreshCommand`
  - `UpCommand`
  - `ExpandAddressEditorCommand`
  - `CollapseAddressEditorCommand`
  - `IsIdleState / IsLoadingState / IsLoadedState / IsEmptyState / IsErrorState`

### 核心视图

- `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
  - mode rail tooltip 绑定
  - `SftpToolbarRightPanelHeader -> SftpToolbarHeaderView`
- `src/SkylarkTerminal/Views/RightHeaders/ActionStripHeaderView.axaml`
  - 绑定 `TooltipZh`
- `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml`
  - 固定 `Grid` toolbar
  - 地址 chip/editor 固定在 header 中部
  - `Button.Flyout` 只承载纯动作
- `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml.cs`
  - `OnAddressChipClick`
  - `OnAddressEditorLostFocus`
  - `OnAddressEditorKeyDown`
  - `TryGetSftpModeViewModel(out SftpModeViewModel vm)`
- `src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml`
  - `Idle / Loading / Empty / Error / Loaded` 状态区域
  - dual-row item template
- `src/SkylarkTerminal/Views/MainWindow.axaml`
  - `RightSidebarSftpRowHoverBackgroundBrush`
  - `RightSidebarSftpSecondaryForegroundBrush`
  - `RightSidebarSftpStateSurfaceBrush`
  - `RightSidebarSftpStateBorderBrush`

## 已有自动化测试基线

当前已覆盖：

- `tests/SkylarkTerminal.Tests/RightSidebarModeMetadataTests.cs`
- `tests/SkylarkTerminal.Tests/RightToolsModeSwitchTests.cs`
- `tests/SkylarkTerminal.Tests/SftpModeActivationStateTests.cs`
- `tests/SkylarkTerminal.Tests/SftpAddressInteractionStateTests.cs`
- `tests/SkylarkTerminal.Tests/RightSidebarLocalizationMetadataTests.cs`
- `tests/SkylarkTerminal.Tests/RightPanelModeActionsTests.cs`
- `tests/SkylarkTerminal.Tests/RightPanelHeaderArchitectureTests.cs`
- `tests/SkylarkTerminal.Tests/RightSidebarHostViewTemplateTests.cs`
- `tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs`
- `tests/SkylarkTerminal.Tests/SftpModeStateTemplateTests.cs`
- `tests/SkylarkTerminal.Tests/RightModeViewsBindingTests.cs`
- `tests/SkylarkTerminal.Tests/RightSidebarStyleConsistencyTests.cs`

这些测试目前主要覆盖：

- ViewModel 状态流转
- 命令 metadata 与中文文案来源
- header/mode host XAML 模板契约
- `SFTP` 内容区状态模板与样式 token 存在性

## 下一阶段优先补强的 TDD 场景

### P0

1. `SftpModeViewModel` 在 `ActivateAsync` 重复调用时不应重复累积 `Items`
2. `SftpModeViewModel` 在 `Back / Forward / Up / Refresh` 后应稳定更新 `LoadState`
3. `MainWindowViewModel` 通过 mode rail 切换到 `SFTP` 时，应只触发一次激活逻辑

### P1

1. `SftpToolbarHeaderView` 中 `More` flyout 必须不包含 `TextBox`
2. 地址编辑态下点击外部区域时，应收起 editor 且恢复 chip
3. `Esc` 关闭 editor 后，焦点应回到 `AddressChipButton`
4. 从 `SFTP` 切回 `Snippets/History` 时，不应残留 editor 可见状态

### P1

1. `SFTP` 内容区在 `Empty` 状态下不应渲染文件列表
2. `SFTP` 内容区在 `Error` 状态下应显示 `ErrorMessage` 与 `RefreshCommand`
3. dual-row item template 应稳定显示：
   - 第一行：icon + `Name`
   - 第二行：`KindLabelZh` + `SizeLabel` + `FullPath`

### P2

1. 深色/浅色主题下 `RightSidebarSftpSecondaryForegroundBrush` 可读性
2. 窄宽度下固定 toolbar 不应把地址输入框放入 flyout
3. `More` flyout dismiss 后不应出现 overlay 层级异常

## 边缘情况清单

1. `ActivateAsync` 时服务返回空目录
2. `ActivateAsync` 时服务抛异常
3. 地址输入为空、只含空格、相对路径、重复路径
4. `Refresh` 在 `Error` 状态下重试成功
5. `BackCommand` / `ForwardCommand` 在根路径或历史为空时的行为
6. `RemoteFileNode.SizeLabel` 在 `B / KB / MB / GB` 边界值上的格式化

## 推荐验证命令

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightPanel|FullyQualifiedName~RightSidebar|FullyQualifiedName~RightToolsMode|FullyQualifiedName~SftpMode|FullyQualifiedName~SftpAddress" -v minimal
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal
dotnet build SkylarkTerminal.slnx -v minimal
```

## 当前已确认的真实结果

- 定向右栏回归：`15/15 PASS`
- 全量测试：`85/85 PASS`
- 构建：`Build succeeded, 0 Warning(s), 0 Error(s)`

## 相关输出文档

- `docs/plans/2026-03-09-right-sidebar-layout-bugfix2-design.md`
- `docs/plans/2026-03-09-right-sidebar-layout-bugfix2-implementation-plan.md`
