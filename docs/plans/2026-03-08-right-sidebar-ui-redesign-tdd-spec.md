# Right Sidebar UI Redesign - TDD Handoff Spec

## 1. 目标与范围

本交接文档用于下一阶段测试强化，覆盖右侧栏重构后的关键行为：

1. ModeRail + 动态动作区 + 模式内容容器化。
2. SFTP 导航服务状态机（路径、历史栈、地址输入）。
3. 右侧视觉 token 与快捷键切换行为。

## 2. 核心类与职责

1. `MainWindowViewModel`
   - 维护 `RightPanelModes`、`ActiveRightMode`。
   - 负责构造各模式动作元数据（`BuildSnippetModeActions/BuildHistoryModeActions/BuildSftpModeActions`）。
   - 承载右侧模式切换命令（`ShowSnippetsToolsCommand/ShowHistoryToolsCommand/ShowSftpToolsCommand`）。
2. `IRightPanelModeViewModel`
   - 统一模式协议：`Kind/Title/Glyph/ContentNode/Actions`。
3. `SnippetsModeViewModel` / `HistoryModeViewModel` / `SftpModeViewModel`
   - 模式级元数据与动作入口。
   - `SftpModeViewModel` 额外封装导航命令与地址提交（`AddressCommitCommand`）。
4. `ModeActionDescriptor`
   - 动作区渲染元数据：`Id/Glyph/Tooltip/Command/IsToggle`。
5. `ISftpNavigationService` / `SftpNavigationService`
   - 路径状态与栈行为：`NavigateTo/GoBack/GoForward/GoUp/Refresh/TryResolveAddressInput`。
6. `RightSidebarHostView` + `Views/RightModes/*`
   - 右侧容器组合与内容视图分离。
7. `MainWindow`
   - 右栏列宽联动与自动收起。
   - `Ctrl+1/2/3` 快捷键模式切换。

## 3. 关键方法测试清单

1. `MainWindowViewModel.InitializeRightPanelModes()`
   - 验证三模式初始化顺序、`ActiveRightMode` 默认值与回退行为。
2. `MainWindowViewModel.ResolveActiveRightMode(...)`
   - 未匹配模式时回退逻辑（首项或 fallback）正确。
3. `MainWindowViewModel.Build*ModeActions()`
   - `Id` 稳定且命令可执行，动作数量符合预期。
4. `SftpNavigationService.NavigateTo/GoBack/GoForward/GoUp`
   - 历史栈与当前路径同步。
5. `SftpNavigationService.TryResolveAddressInput(...)`
   - 空输入、相对路径、反斜杠、尾斜杠归一化。
6. `SftpModeViewModel.AddressCommitCommand`
   - 提交后 `AddressInput`、`CurrentPath`、`CanGoBack/CanGoForward` 同步。
7. `MainWindow.OnWindowKeyDown(...)`
   - `Ctrl+1/2/3` 与 NumPad 对应键切换模式。

## 4. 边缘场景（Edge Cases）

1. 在根路径 `/` 执行 `GoUp()` 不应离开根路径。
2. 连续 `NavigateTo` 相同路径不应污染历史栈。
3. `GoBack()` 到头后再次执行应保持稳定（不抛异常、路径不变）。
4. `GoForward()` 到头后再次执行应保持稳定。
5. 地址输入为 `"var/log/"` 或 `"\\var\\log\\"` 时应归一化为 `/var/log`。
6. 右侧栏隐藏状态下触发 `Ctrl+1/2/3` 后应保持模式状态一致且可正常展开。
7. 快速切换模式时 `ActiveRightMode.Actions` 不应出现空集合/错误集合闪烁。
8. `SftpModeView` 回车提交地址时，不应影响其它全局快捷键处理。

## 5. 建议新增测试

1. `SftpNavigationServicePathNormalizationTests`
   - 覆盖相对路径、空白路径、尾斜杠、反斜杠输入。
2. `SftpNavigationServiceBoundaryTests`
   - 覆盖 back/forward 边界与根目录 `GoUp`。
3. `SftpModeViewModelNavigationSyncTests`
   - 校验命令执行后 UI 绑定字段同步。
4. `MainWindowShortcutModeSwitchTests`
   - 校验 `Ctrl+1/2/3` 和 `Ctrl+NumPad1/2/3`。
5. `RightSidebarHostActionBindingTests`
   - 校验动作区绑定来源稳定为 `ActiveRightMode.Actions`。
6. `RightModeViewDataContextBridgeTests`
   - 校验 `RightModes` 视图通过宿主 DataContext 正确读取 `SnippetItems/HistoryItems/SftpItems`。

## 6. 回归基线命令

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj \
  --filter "FullyQualifiedName~RightPanel|FullyQualifiedName~RightSidebar|FullyQualifiedName~SftpMode" -v minimal

dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal

dotnet build SkylarkTerminal.slnx -v minimal
```
