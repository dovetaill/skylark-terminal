# 2026-03-09 Right Sidebar Layout Bugfix TDD Spec

## 目的

本文件作为 `RightSidebar` 布局修复完成后的 TDD 交接输入，供下一阶段测试补强使用。范围仅覆盖以下改动：

- mode rail 由硬边框高亮调整为 ghost tile 轻量选中态
- `RightSidebar` 第二行从固定动作条升级为模式级 header slot
- `SFTP` 模式顶部导航迁移到单行 `CommandBar`
- 地址栏改为“默认紧凑、聚焦展开、失焦收起”

非目标：

- 不覆盖 `SshTerminalPane`
- 不覆盖 `RowStripedTerminalView`
- 不扩展 `SFTP` 上传/下载等业务能力

## 当前实现基线

### 核心类与视图

- `src/SkylarkTerminal/Models/RightPanelHeaderNode.cs`
  - 定义 `RightPanelHeaderNode`
  - 定义 `ActionStripRightPanelHeader`
  - 定义 `SftpCommandBarRightPanelHeader`

- `src/SkylarkTerminal/ViewModels/RightPanelModes/IRightPanelModeViewModel.cs`
  - 新增 `HeaderNode`
  - 保留 `ContentNode`
  - 保留 `Actions`

- `src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`
  - `ActiveRightMode`
  - `ActiveRightHeader`
  - `InitializeRightPanelModes()`
  - `ResolveActiveRightMode(RightToolsViewKind kind)`
  - `OnSelectedRightToolsViewChanged(RightToolsViewKind value)`
  - `BuildSftpModeActions()` 当前返回空集合，表示 `SFTP` 不再依赖通用动作条

- `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
  - `BackCommand`
  - `ForwardCommand`
  - `RefreshCommand`
  - `UpCommand`
  - `AddressCommitCommand`
  - `ExpandAddressEditorCommand`
  - `CollapseAddressEditorCommand`
  - `IsAddressEditorExpanded`
  - `IsAddressChipVisible`
  - `SyncAddressAndFlags()`
  - `OnIsAddressEditorExpandedChanged(bool value)`

- `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
  - `ContentControl Content="{Binding ActiveRightHeader}"`
  - `ActionStripRightPanelHeader` -> `ActionStripHeaderView`
  - `SftpCommandBarRightPanelHeader` -> `SftpCommandBarHeaderView`

- `src/SkylarkTerminal/Views/RightHeaders/ActionStripHeaderView.axaml`
  - 承载 `Snippets/History` 轻量动作条

- `src/SkylarkTerminal/Views/RightHeaders/SftpCommandBarHeaderView.axaml`
  - 单行 `FluentAvalonia CommandBar`
  - 地址栏 chip / editor 双状态容器

- `src/SkylarkTerminal/Views/RightHeaders/SftpCommandBarHeaderView.axaml.cs`
  - `OnAddressChipClick`
  - `OnAddressEditorLostFocus`
  - `OnAddressEditorKeyDown`
  - `TryGetSftpModeViewModel(out SftpModeViewModel vm)`

- `src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml`
  - 现在只承载文件列表内容，不再包含导航栏

- `src/SkylarkTerminal/Views/MainWindow.axaml`
  - `RightSidebarModeSelectedBackgroundBrush`
  - `RightSidebarModeSelectedBorderBrush`
  - `RightSidebarModeSelectedForegroundBrush`
  - `RightSidebarModeHoverBackgroundBrush`
  - `RightSidebarCommandButton`
  - `RightSidebarSftpCommandBar`
  - `RightSidebarAddressChip`
  - `RightSidebarAddressEditor`

## 已有自动化测试基线

当前已经覆盖的测试文件：

- `tests/SkylarkTerminal.Tests/RightPanelHeaderArchitectureTests.cs`
- `tests/SkylarkTerminal.Tests/RightSidebarHostViewTemplateTests.cs`
- `tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs`
- `tests/SkylarkTerminal.Tests/RightPanelModeActionsTests.cs`
- `tests/SkylarkTerminal.Tests/SftpAddressInteractionStateTests.cs`
- `tests/SkylarkTerminal.Tests/RightSidebarStyleConsistencyTests.cs`

这些测试目前主要是：

- ViewModel 行为测试
- XAML 模板/样式存在性测试
- 模式切换和 `SFTP` 动作来源约束测试

缺口仍然存在于真实 UI 交互链路，而不是结构存在性。

## 下一阶段建议优先补的 TDD 场景

### P0: ViewModel 级行为回归

1. `SftpModeViewModel` 在 `CollapseAddressEditorCommand` 执行后必须恢复 `AddressInput = CurrentPath`
2. `SftpModeViewModel` 在 `Back / Forward / Refresh / Up` 后必须同步：
   - `CurrentPath`
   - `AddressInput`
   - `CanGoBack`
   - `CanGoForward`
3. `MainWindowViewModel` 在 `Snippets -> History -> SFTP` 连续切换时，`ActiveRightHeader` 和 `ActiveRightMode` 必须保持同源

### P1: 视图交互测试

1. `OnAddressChipClick` 后必须：
   - 展开编辑态
   - 将焦点切到 `AddressEditor`
   - 选中文本
2. `OnAddressEditorKeyDown` 收到 `Esc` 后必须：
   - 收起编辑态
   - 保留当前导航路径
   - 焦点回到 chip
3. `OnAddressEditorLostFocus` 只应在真正离开地址编辑交互时收起，避免与命令按钮点击形成抖动

### P1: 模板职责测试

1. `RightSidebarHostView` 必须继续通过 header slot 渲染头部，而不是回退成宿主内联按钮
2. `SftpModeView` 不得重新引入第二条导航栏
3. `SFTP` mode 的 `Actions` 应始终为空，防止双源命令回归

### P2: 样式与视觉约束测试

1. `RightSidebarModeButton` 选中态应继续使用 `RightSidebarModeSelected*` token
2. `SftpCommandBarHeaderView` 必须继续挂接：
   - `RightSidebarSftpCommandBar`
   - `RightSidebarCommandButton`
   - `RightSidebarAddressChip`
   - `RightSidebarAddressEditor`
3. 若后续改动 `MainWindow.axaml`，应防止 mode rail 再回退到 `CornerRadius=0`

## 边缘情况清单

以下是下一阶段最值得测试的边缘情况：

1. `AddressEditor` 处于展开态时点击 `Refresh` / `Up` / `Back` / `Forward`
   - 需要确认不会出现地址先收起、再失焦、再触发错误命令的抖动链

2. `AddressEditor` 中输入非法路径后按 `Enter`
   - 当前行为由 `SftpNavigationService.TryResolveAddressInput` 决定
   - 需要补测试确认 UI 状态是否应收起，以及 `AddressInput` 是否回滚到实际路径

3. 侧栏宽度压缩到接近 340px 或更小时
   - `CommandBar` 可能进入 overflow
   - 需要测试 `Back / Forward / Refresh / Up` 仍可访问

4. 在地址编辑态切换模式
   - 从 `SFTP` 切回 `Snippets/History` 时，不应残留焦点状态或可见性异常

5. 主题切换
   - 暗色 / 亮色主题下 ghost tile 的前景色与 hover 底色仍需保持可读性

## 建议测试入口

建议下一阶段继续沿用当前测试项目：

- `tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj`

建议保留的验证命令：

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightPanel|FullyQualifiedName~RightSidebar|FullyQualifiedName~SftpMode|FullyQualifiedName~SftpAddress" -v minimal
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal
dotnet build SkylarkTerminal.slnx -v minimal
```

## 当前已确认的真实验证结果

基于本轮最终代码状态，已验证：

- 右栏相关定向测试：`8/8 PASS`
- 全量测试：`80/80 PASS`
- 构建：`Build succeeded. 0 Warning(s), 0 Error(s)`

## 建议的下一轮 TDD 拆分

1. 先补 `SftpModeViewModel` 命令状态同步测试
2. 再补 `SftpCommandBarHeaderView` 的焦点/失焦交互测试
3. 再补样式 token 与 class 粘连回归测试
4. 最后补窄宽度和主题切换下的 UI 回归测试
