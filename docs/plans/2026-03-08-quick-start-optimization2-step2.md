# Quick Start 起始页优化2 - TDD 交接文档（Step 2）

- 日期：2026-03-08
- 上一阶段文档：`docs/plans/2026-03-08-quick-start-optimization2-step1.md`
- 目标：为下一阶段 `test-driven-development` 提供可直接落地的测试输入与边界清单

## 1. 本次已落地范围（对应 Step1 决策）

1. Quick Start -> Hosts 定位链路已落地：
   - 显示资产面板
   - 切换到 Hosts pane
   - 解析目标 Host（当前会话优先，最近连接次之）
   - 树形模式：展开祖先 + 选中
   - 平铺模式：选中 + `ScrollIntoView`
   - 目标节点 1.2s transient highlight（保留最终选中）
2. 终端 token 色板体系已落地（Dark/Light 双模式）。
3. Light 主题终端暗底问题已修复（透明/非透明均不再使用暗色终端底）。
4. 内部 fork 边界已建立并接入本地项目引用。
5. 渲染层增强已落地：
   - 行级相邻色 stripe（Even/Odd）
   - ANSI palette remap（ThemeOptions）
   - 无 ANSI 时 fallback 高亮（L1 全局 + L2 shell 感知）

## 2. 核心类与关键方法

### 2.1 Hosts 定位链路

- `MainWindowViewModel`
  - `LocateHostFromQuickStart()`
  - `ActivateHostsAssetsPane()`
  - `ResolveQuickStartLocateTarget()`
  - `ResolveConnectionNodeBySelectedTab()`
  - `ResolveConnectionNodeByConfig(ConnectionConfig)`
  - `TryLocateTargetInTree(ConnectionNode)`
  - `FindConnectionNodeInCurrentHostsTree(ConnectionNode)`
  - `ExpandTreeAncestors(AssetNode)`
  - `TryCollectAncestorFolders(...)`
  - `TryLocateTargetInFlatList(ConnectionNode)`
  - `FindConnectionNodeInCurrentFlatHosts(ConnectionNode)`
  - `StartQuickLocateHighlight(AssetNode)`
  - `RunQuickLocateHighlightAsync(AssetNode, CancellationToken)`
- `MainWindow`
  - `EnsureFlatLocateTargetVisible()`
- `SshTerminalPane`
  - `OnQuickStartBrowseHostsClick(...)`（调用 `LocateHostFromQuickStartCommand`）

### 2.2 终端 token 与主题映射

- `MainWindow.axaml`
  - `Terminal.*` token（Background、Foreground、RowStripe、Selection、Cursor、Syntax、Overlay）
- `MainWindow.axaml.cs`
  - `ApplyShellPalette(...)`（动态刷新 `Terminal.*` 与兼容 `ShellTerminal*`）
- `SshTerminalPane.axaml`
  - 终端与 overlay 全部消费 `Terminal.*` token

### 2.3 fork 渲染层

- `RowStripedTerminalView : TerminalView`
  - `Render(DrawingContext)`
  - `RenderRowStripes(...)`
  - `ApplyThemeAndFallbackColoring()`
  - `SyncAnsiThemePalette()`
  - `ApplyFallbackHighlighting(TerminalBuffer)`
  - `ApplyFallbackHighlightingToLine(BufferLine)`
  - `BuildFallbackTokenSpans(string)`（L1/L2）

## 3. TDD 优先测试清单（建议顺序）

### A. Quick Start 定位行为（优先级 P0）

1. 当前 Tab 有连接配置时，定位目标优先使用当前连接（而非最近连接首项）。
2. 当前 Tab 无连接配置时，使用 `FilteredQuickStartRecentConnections` 首项。
3. 无可用目标时，仅打开 Hosts 资产，不抛异常，状态文案正确。
4. 树形模式定位后：
   - 祖先文件夹均为 `IsExpanded = true`
   - `SelectedAssetNode` 为目标连接
5. 平铺模式定位后：
   - `SelectedAssetNode` 为目标连接
   - `SelectedAssetNodes` 仅包含目标
   - UI 层触发 `ScrollIntoView`
6. 定位成功后目标节点高亮在约 1.2 秒后回落，选中态不丢失。

### B. 主题 token 与 Light 修复（优先级 P0）

1. Light 非透明模式下 `Terminal.Background` 为浅色（非暗底）。
2. Light 透明模式下 `Terminal.Background` 仍为浅色半透明（非暗底）。
3. 终端主体、Quick Start overlay、Connecting overlay 读取的是 `Terminal.*` 而非硬编码色值。

### C. 渲染层增强（优先级 P1）

1. `RowStripedTerminalView` 在 `base.Render` 前绘制行条纹。
2. `Terminal.RowStripe.Even/Odd` 缺失时不崩溃，渲染降级安全。
3. ANSI remap 生效：`Terminal.Options.Theme` 与 token 同步。
4. fallback 仅作用于默认前景色单元格（不覆盖已有 ANSI 颜色）。
5. L1 规则：error/warn/success/path/host/number 命中时颜色变化。
6. L2 规则：shell prompt 语境下变量、引号段、pipe/redirect、flag、command 命中时颜色变化。
7. selection/cursor 视觉优先级高于 stripe（基类渲染层仍可见）。

## 4. 边缘情况（Edge Cases）

1. `FilteredQuickStartRecentConnections` 与真实资产树不一致（已删除/重命名节点）。
2. 当前处于非 Hosts pane 且资产面板收起时触发定位。
3. 树节点深层嵌套（多级 folder）路径展开正确性。
4. 平铺列表在过滤状态下目标不在当前 `CurrentAssetFlatList`。
5. 连续快速点击定位，前一次高亮取消与后一次高亮衔接。
6. 终端 buffer 在滚动/高吞吐输出时 fallback 处理的性能与闪烁。
7. 行文本含宽字符/组合字符时，按列 fallback 的 token 对齐偏差风险。

## 5. 已知限制（供下一阶段评估）

1. fallback 采用轻量规则与按列映射，复杂 Unicode 场景可能存在高亮边界误差。
2. 当前 L2 仅覆盖常见 shell 语法特征，不是完整语法解析器。
3. `SkylarkTerminal.slnx` 仍引用缺失的 `tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj`；
   - 现阶段验证使用项目级 build：
   - `dotnet build src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork/Iciclecreek.Avalonia.Terminal.Fork.csproj`
   - `dotnet build src/SkylarkTerminal/SkylarkTerminal.csproj`

## 6. 下一步 TDD 建议

1. 先补 `MainWindowViewModel` 单元测试（定位优先级、树/平铺定位、高亮状态机）。
2. 再补 `MainWindow` 交互测试（`EnsureFlatLocateTargetVisible` 的滚动触发）。
3. 最后补 fork 渲染测试（mock buffer + token 断言 + ANSI/fallback 互斥断言）。
