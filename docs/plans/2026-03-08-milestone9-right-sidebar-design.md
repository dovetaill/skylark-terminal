# Milestone 9 - RightSidebar 容器与模式切换设计方案

## 1. 背景

当前项目已具备右侧工具栏的基础能力（显示/隐藏、`GridSplitter` 拖拽、`Snippets/History/SFTP` 数据与命令），但现状仍偏“单文件内联实现”，缺少更清晰的容器化结构与模式切换语义。

现有关键事实（基于当前代码）：

- 右侧栏列结构与收缩联动已存在：`MainWindow.axaml` + `MainWindow.axaml.cs`。
- ViewModel 已有右侧状态与模式枚举：`IsRightSidebarVisible`、`SelectedRightToolsView`、`RightToolsViewKind`。
- 终端为 `Iciclecreek.Avalonia.Terminal` 本地 fork 扩展，不是 `AvaloniaEdit`。

本次 Milestone 9 目标不是重写终端或主布局，而是在既有基础上完成“右侧容器结构升级 + 模式切换模块化 + 平滑显示”。

## 2. 目标

### 2.1 功能目标

1. 右侧栏显示时可通过 `GridSplitter` 调宽，隐藏时完全不占空间。
2. 右侧栏顶部提供模式切换区域，至少支持 `Snippets` 与 `History`，并保留 `SFTP` 为核心模式。
3. 下方内容区通过 `ContentControl` 动态切换对应 `UserControl`。
4. 顶部状态栏触发侧栏时具备平滑显示体验。

### 2.2 非功能目标

1. 不影响左侧资产区与中间 Workspace 行为。
2. 保持 MVVM 边界，避免将模式切换逻辑写进 code-behind。
3. 与现有主题资源（深浅色、透明/不透明）保持一致。

## 3. 边界（In Scope / Out of Scope）

### 3.1 In Scope

- 右侧栏容器结构重整。
- 模式头部控件形态升级（`TabStrip`）。
- 右侧内容区动态模板切换。
- 开关动画与交互状态处理。

### 3.2 Out of Scope

- 终端渲染引擎或 SSH 会话链路改造。
- SFTP 真实后端能力扩展（仅保持现有入口与视图承载）。
- 左侧资产与 Workspace 的业务规则重写。

## 4. 方案对比

### 4.1 设计要点 A：右侧容器与可拖拽宽度

#### 方案 A1（已选）：保留 `Grid + GridSplitter + ColumnDefinition`

- 优点：
  - 与现有结构一致，改动面小，回归风险低。
  - 已验证可支持阈值自动收起与列宽联动。
  - 对中间 Workspace 侵入最小。
- 缺点：
  - 平滑动画需额外补充，不是“开箱即用”。

#### 方案 A2：改为 `SplitView` 右侧 Pane

- 优点：
  - `IsPaneOpen` 语义清晰。
- 缺点：
  - 右侧拖拽宽度需要额外自定义 resizer。
  - 迁移成本更高，和当前布局整合复杂度更大。

结论：采用 A1。

### 4.2 设计要点 B：顶部模式切换控件

#### 方案 B1（已选）：`TabStrip` 作为 Mode Header

- 优点：
  - 语义最贴合“模式切换”。
  - 可实现 Win11 Fluent 风格的激活态与指示条。
  - 后续扩展新模式更自然。
- 缺点：
  - 需要单独写一套样式以达成视觉质感。

#### 方案 B2：`ToggleButton` 互斥组

- 优点：
  - 实现简单。
- 缺点：
  - 样式状态和可维护性较弱，视觉一致性难控制。

结论：采用 B1。

### 4.3 设计要点 C：内容区动态切换

#### 方案 C1（已选）：`ContentControl + DataTemplate -> UserControl`

- 优点：
  - 组件边界清晰，符合 MVVM。
  - 每个模式内容可独立演进、可测试性更好。
- 缺点：
  - 需要新增视图文件与模板映射。

#### 方案 C2：同页多控件 `IsVisible` 开关

- 优点：
  - 改动最小。
- 缺点：
  - 随模式增长易变臃肿；视图耦合高。

结论：采用 C1。

### 4.4 设计要点 D：平滑显示与布局稳定

#### 方案 D1（已选）：双阶段动画（列宽联动 + 内容滑入/淡入）

- 优点：
  - 视觉连贯，符合高质感 Fluent 体验。
  - 对左侧业务无干扰。
- 缺点：
  - 需要处理快速重复触发（开关抖动）与状态竞争。

#### 方案 D2：仅淡入淡出，无滑入

- 优点：
  - 实现简单。
- 缺点：
  - “侧栏平滑显示”感知较弱。

结论：采用 D1。

## 5. 最终决策

采用组合方案：**A1 + B1 + C1 + D1**，并保留 `SFTP` 作为核心第三模式。

落地原则：

1. 继续使用现有 `MainContentGrid` 的右侧列与 `GridSplitter` 机制。
2. 将右侧面板改造为“模式头 + 动态内容宿主”的容器结构。
3. 模式头使用 `TabStrip`（`Snippets / History / SFTP`）。
4. 内容区使用 `ContentControl`，根据 `RightToolsViewKind` 映射到独立 `UserControl`。
5. 显示/隐藏采用双阶段动画，并对快速切换做状态保护。

## 6. 设计细节

### 6.1 视图结构分层

推荐拆分为以下结构：

1. `RightToolsHostView`：右侧总容器（header + content + context 操作入口）。
2. `SnippetsToolsView`：Snippets 模式内容。
3. `HistoryToolsView`：History 模式内容。
4. `SftpToolsView`：SFTP 模式内容（保留核心能力）。

`MainWindow.axaml` 仅负责放置右侧容器，不再内联大量具体内容模板。

### 6.2 Mode Header（B1）样式准则

`TabStrip` 样式目标：

1. 高度：紧凑（建议 30~34）。
2. 激活态：底部指示线 + 半透明背景层。
3. 非激活态：低对比文本 + hover 微增强。
4. 图标：使用 `Segoe Fluent Icons`，与顶部状态栏图标体系统一。
5. 主题：全部走 `DynamicResource`（深浅色、透明/不透明均可复用）。

### 6.3 内容区动态装载（C1）

`ContentControl` 绑定到当前模式对象（或枚举代理属性），通过 `DataTemplate` 映射到对应 `UserControl`：

- `Snippets -> SnippetsToolsView`
- `History -> HistoryToolsView`
- `Sftp -> SftpToolsView`

避免在一个 XAML 中同时长期驻留多个大型内容树。

### 6.4 动画策略（D1）

建议状态机：

- `Hidden`
- `Opening`
- `Visible`
- `Closing`

开启动作：

1. 先恢复右列与 splitter 宽度（从 0 到目标宽度）。
2. 同步触发右容器 `TranslateTransform.X`（正值到 0）与 `Opacity`（0 到 1）过渡。

关闭动作：

1. 先执行容器淡出/滑出。
2. 动画结束后将右列和 splitter 置 0，确保不占位。

防抖策略：

1. 当处于 `Opening/Closing` 时，重复命令采用“最后一次意图覆盖”。
2. 拖拽 splitter 期间禁用关闭动画抢占。

## 7. 实施步骤

1. 容器拆分：将右侧内联内容抽离到独立视图组件。
2. 模式头升级：将顶部按钮组替换为 `TabStrip`，统一命令与选中状态。
3. 动态内容：接入 `ContentControl + DataTemplate` 映射。
4. 动画接入：实现右侧开关双阶段动画与状态保护。
5. SFTP 保留：确保 `ShowSftpToolsCommand` 与 Mode Header 的第三项保持可用。
6. 样式打磨：按 Fluent 风格补齐 hover/active/pressed/disabled 状态。
7. 测试补齐：更新/新增与右侧容器和模式切换相关测试。

## 8. 风险与回滚

### 8.1 主要风险

1. 动画与列宽更新顺序冲突，导致闪烁或空白列残留。
2. 快速连点切换引发状态竞争（显示状态与列宽不同步）。
3. 模式切换后 DataContext 传递不完整，导致空视图。

### 8.2 回滚策略

1. 保留现有 `Grid` 列宽联动逻辑作为兜底路径。
2. 若动画导致不稳定，可先降级为“无滑动，仅显隐”，保留 C1 容器化收益。
3. 若 `TabStrip` 样式回归风险过高，临时恢复按钮组，但保持 `ContentControl` 动态切换不回退。

## 9. 验证清单（Checklist）

### 9.1 交互验收

1. 顶部状态栏按钮可平滑展开/收起右侧栏。
2. 右侧栏展开后可拖拽 `GridSplitter` 调宽。
3. 收起后右列与 splitter 不占空间。
4. 模式头可切换 `Snippets / History / SFTP`。
5. 每次切换时，下方内容区仅显示当前模式对应 `UserControl`。

### 9.2 回归验收

1. 左侧资产区拖拽缩放与收起逻辑不受影响。
2. 中间 Workspace（标签拖拽、分屏）行为不受影响。
3. 深浅色 + 透明/不透明模式下样式一致。

### 9.3 自动化建议

1. ViewModel 测试：
   - `ToggleRightSidebar` 状态机边界；
   - `ShowSnippets/ShowHistory/ShowSftp` 模式切换与可见性联动。
2. UI 级策略测试（如现有测试框架允许）：
   - 右列宽度在 show/hide 时的断言；
   - 模式切换后内容宿主类型断言。

## 10. 参考依据

1. Avalonia GridSplitter 官方文档  
   https://docs.avaloniaui.net/docs/reference/controls/gridsplitter
2. Avalonia Transitions 官方文档  
   https://docs.avaloniaui.net/docs/guides/graphics-and-animation/transitions
3. Avalonia TransitioningContentControl 官方文档  
   https://docs.avaloniaui.net/docs/reference/controls/transitioningcontentcontrol
4. Avalonia TabStrip 官方文档  
   https://docs.avaloniaui.net/docs/reference/controls/tabstrip
5. Avalonia SplitView 官方文档（对比方案参考）  
   https://docs.avaloniaui.net/docs/reference/controls/splitview

