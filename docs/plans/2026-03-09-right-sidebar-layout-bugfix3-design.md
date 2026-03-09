# 2026-03-09 RightSidebar Layout Bugfix3 Design

## 背景

当前 `RightSidebar` 的 `SFTP` 模式已经完成固定 `Grid` toolbar、地址栏 `chip/editor` 双态和内容区状态卡片化改造，但在最新一轮视觉与交互联调中，又暴露出 3 个新的稳定性问题：

1. 点击地址栏后，展开态编辑器会侵入左侧命令区，遮住 `Forward` 按钮。
2. `More` 按钮点击后只出现黑色空框，未正确显示菜单内容。
3. 从 `SFTP` 切到 `Snippets / History` 时，会短暂闪出 `SFTP` 的状态卡片，形成明显残影。

本轮目标不是扩展 `SFTP` 业务能力，而是在既有右栏架构下，重新确认 `SFTP` 头部与模式切换的稳定设计基线，作为后续修复的唯一依据。

## 调研结论

### 代码边界

- 右侧栏宿主：`src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
- `SFTP` 头部：`src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml`
- `SFTP` 内容区：`src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml`
- `SFTP` 模式状态：`src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- 模式切换入口：`src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`

### 当前实现事实

- `SFTP` 头部已经不再依赖 `CommandBar`，而是固定三段 `Grid`：
  - 左：`Back / Forward`
  - 中：地址 `chip / editor`
  - 右：`Refresh / Up / More`
- 地址编辑器当前直接在原位展开，且有固定 `MinWidth=220`，没有单独的覆盖层或宽度分级策略。
- `More` 当前采用普通 `Flyout + Border + ItemsControl + Button` 组合，而不是菜单型 flyout。
- 右栏模式内容宿主仍使用 `TransitioningContentControl` 承载 `ActiveRightMode.ContentNode`。

### 终端实现回顾

当前 SSH 终端不是 `AvaloniaEdit`，而是第三方 fork 控件 `Iciclecreek.Avalonia.Terminal.Fork.RowStripedTerminalView`，挂载在 `src/SkylarkTerminal/Views/SshTerminalPane.axaml`。真实 SSH 连接服务由 `SSH.NET` 驱动，位于 `src/SkylarkTerminal/Services/SshConnectionService.cs`。因此本轮 bug 不属于终端渲染链路，而属于 `RightSidebar/SFTP` 宿主与交互层。

### Git 历史回顾

最近相关提交链路如下：

- `6138b73` `feat: add compact-expand interaction for sftp address editor`
- `d453eae` `refactor: replace sftp command bar with fixed grid toolbar`
- `a0714e9` `style: redesign sftp content state templates and dual-row list`

可见本轮问题均发生在 `bugfix2` 之后的新一版 `SFTP` 头部与状态表达中。

## 目标

- 修复地址栏展开时遮挡左侧命令的问题。
- 将 `More` 恢复为稳定、可维护、可扩展的 Fluent 风格菜单。
- 保证 `历史路径` 与 `搜索` 不塞进 `More`，同时不把头部工具区挤爆。
- 去掉无收益的内容切换动画，彻底消除 `SFTP` 残影闪烁。

## 非目标

- 不扩展真实 `SFTP` 上传、下载、删除、重命名能力。
- 不改 `SSH` 终端控件、连接服务或 workspace 分屏架构。
- 不在本轮引入多层级复杂设置面板。

## 方案对比

### 设计点 A：地址编辑器展开方式

#### 方案 A1：原位展开，重新压缩中间槽位

优点：

- 结构简单
- 变更最小

缺点：

- 很难彻底避免与左右按钮抢空间
- 窄宽度下仍然脆弱

#### 方案 A2：覆盖式地址编辑层

做法：

- 默认仍显示 `PathChip`
- 点击后，不在原位把 `TextBox` 挤开布局，而是在 header 内拉起一个覆盖式 editor layer
- editor 覆盖中间路径区，并压住自身区域，不改变左右命令按钮布局
- 提交、`Esc`、失焦后关闭 overlay，回到 `PathChip`

优点：

- 地址编辑态和浏览态边界清晰
- 左右命令区始终稳定
- 更适合当前 340px 右栏宽度

缺点：

- 需要单独设计 overlay 外观与层级
- 如果样式处理不好，容易显得突兀

**最终决策：采用 A2**

### 设计点 B：`More` 菜单宿主

#### 方案 B1：改用 `FAMenuFlyout`

做法：

- 使用 `FluentAvalonia` 的 `FAMenuFlyout`
- 命令项使用 `MenuFlyoutItem`
- 勾选项使用 `ToggleMenuFlyoutItem`
- 菜单项统一使用“前图标 + 后文字”布局

优点：

- 语义正确，适合命令菜单
- 比普通 `Flyout + ItemsControl` 更易维护
- 天然支持勾选态和统一菜单样式
- 可直接承接 Fluent 视觉语言

缺点：

- 需要把当前 `MoreCommands` 从“普通按钮集合”整理成菜单项模型

#### 方案 B2：保留普通 `Flyout`

优点：

- 理论上更自由

缺点：

- 需要自己维护 popup 内容布局、样式和绑定链路
- 对当前黑框问题没有结构性止损

**最终决策：采用 B1**

### 设计点 C：`历史路径` 与 `搜索` 的放置方式

#### 方案 C1：嵌入 `PathChip` 的 utility slot

做法：

- 外围头部仍保留：
  - `Back`
  - `Forward`
  - `PathChip`
  - `Refresh`
  - `Up`
  - `More`
- `历史路径` 与 `搜索` 不作为外围独立按钮，而是作为 `PathChip` 内部 trailing utility actions
- 当进入 `A2` 的地址编辑 overlay 态时，这两个 utility actions 自动隐藏
- `More` 只保留勾选或轻动作，例如 `显示隐藏文件`

优点：

- 不额外挤占头部宽度
- 单行结构最稳定
- 视觉上仍然紧凑，不会把 toolbar 做碎
- 与 `A2` 的浏览态/编辑态切换天然兼容

缺点：

- 需要把 `PathChip` 组合成更完整的复合控件

#### 方案 C2：外围独立按钮 + 宽度断点折叠

优点：

- 语义直观

缺点：

- 需要维护断点逻辑
- 宽窄变化时按钮位置会漂移
- 容易重新走回“伪 overflow”复杂度

#### 方案 C3：二级展开条

优点：

- 不会挤单行按钮

缺点：

- 头部会从单层变双层
- 破坏当前右栏干净的顶部节奏

**最终决策：采用 C1**

### 设计点 D：模式切换残影

#### 方案 D1：移除 `TransitioningContentControl` 过渡

做法：

- 右栏内容区不再做无必要的切换动画
- 直接改为普通 `ContentControl`，或将 `PageTransition` 设为 `null`
- 目标是让旧内容在模式切换时立即卸载，不参与下一帧渲染

优点：

- 实现直接
- 风险最低
- 能从结构上消除 `SFTP` 状态卡片残影

缺点：

- 失去模式切换动画

#### 方案 D2：保留动画，补可见性门闩

优点：

- 可保留过渡效果

缺点：

- 复杂度不值得
- 仍有残影或时序回归风险

**最终决策：采用 D1**

## 最终决策

本轮最终采用组合方案：**A2 + B1 + C1 + D1**

具体含义如下：

- 地址栏从“原位展开”改为“覆盖式编辑层”。
- `More` 从普通 `Flyout` 改为 `FAMenuFlyout`。
- `历史路径` 与 `搜索` 不进入 `More`，而是嵌入 `PathChip` 内部 trailing utility slot。
- 右栏内容切换去掉 `TransitioningContentControl` 过渡，优先保证稳定性。

## 目标形态

### 1. Header 浏览态

- 左侧固定：`Back`、`Forward`
- 中间固定：`PathChip`
- `PathChip` 内部从左到右：
  - 当前路径文本
  - `历史路径`
  - `搜索`
- 右侧固定：`Refresh`、`Up`、`More`

### 2. Header 编辑态

- 点击 `PathChip` 后，弹出覆盖式地址编辑层
- 覆盖层只影响中间路径区域，不推动左右按钮
- 编辑态下隐藏 `历史路径` 与 `搜索` utility actions
- `Enter` 提交，`Esc` 或失焦收起

### 3. More 菜单

- 使用 `FAMenuFlyout`
- 仅承载轻动作与勾选项
- 本轮默认纳入：
  - `显示隐藏文件`
- 菜单项统一使用：
  - 左图标
  - 右文字
  - 勾选项显示选中状态

### 4. 模式切换

- 右栏切换 `Snippets / History / SFTP` 时不再播放内容切换动画
- 切走 `SFTP` 后，其状态卡片和错误卡片必须立即消失

## 实施步骤

1. 重构 `SFTP` header 结构
   - 将中间路径区改为“浏览态 `PathChip` + 编辑态 overlay”双层结构
   - 保持左右命令区宽度稳定
2. 整理 `PathChip` 复合布局
   - 在 `PathChip` 内嵌 `历史路径` 与 `搜索` utility actions
   - 编辑态自动隐藏 utility actions
3. 重建 `More` 菜单模型
   - 从普通命令集合切换为菜单项模型
   - 使用 `FAMenuFlyout` 与勾选项承载 `显示隐藏文件`
4. 移除右栏无收益过渡
   - 替换或禁用 `TransitioningContentControl`
   - 确保模式切换时旧内容立即卸载
5. 补齐静态测试与模板测试
   - 校验 `SFTP` header 不再依赖普通 `Flyout + ItemsControl` 组合
   - 校验右栏宿主不再使用默认内容切换过渡

## 风险与回滚

### 风险

- `A2` 如果遮罩层尺寸、圆角或边框处理不好，会显得比原位展开更突兀。
- `C1` 会让 `PathChip` 成为复合控件，模板复杂度会上升。
- `B1` 需要调整现有 `MoreCommands` 模型，测试需要同步更新。

### 回滚策略

- 若 `A2` 视觉不达标，可回滚到 `A1` 的“原位固定槽位展开”方案，但保留 `B1 + D1`。
- 若 `C1` 最终信息密度仍不理想，可回退为 `历史路径` 保留在 chip 内，`搜索` 单独外置为右侧单按钮。
- 若 `FAMenuFlyout` 在样式层出现不可接受问题，可保留菜单语义模型，仅替换 presenter 实现，不恢复到普通 `Flyout + ItemsControl`。

## 验证清单

- 点击地址栏后，`Forward` 不再被编辑器遮挡。
- 地址编辑 overlay 打开和关闭时，左右命令按钮位置不跳动。
- `历史路径` 与 `搜索` 在浏览态可见，在编辑态自动隐藏。
- `More` 点击后显示正常的 Fluent 风格菜单，不再出现黑色空框。
- `显示隐藏文件` 在菜单中表现为勾选项，而不是普通命令按钮。
- 从 `SFTP` 切换到 `Snippets / History` 时，不再闪出 `SFTP` 的状态卡片或重试按钮。
- 不改动 SSH 终端控件、连接链路与业务服务边界。

## 参考

- Avalonia `TransitioningContentControl` 文档（说明 `PageTransition` 可设为 `null` 禁用过渡）：
  https://docs.avaloniaui.net/docs/reference/controls/transitioningcontentcontrol
- Avalonia `Flyout / MenuFlyout` 文档：
  https://docs.avaloniaui.net/docs/reference/controls/flyouts
  https://docs.avaloniaui.net/docs/reference/controls/menu-flyout
- FluentAvalonia `FAMenuFlyout` 文档：
  https://amwx.github.io/FluentAvaloniaDocs/pages/Controls/FAMenuFlyout

## 实现同步（2026-03-09）

- `RightSidebarHostView` 已切换为普通 `ContentControl` 承载 `ActiveRightMode.ContentNode`，模式切换时不再保留 `TransitioningContentControl` 过渡。
- `SFTP` 头部已实现为 browse surface + overlay shell：点击路径 chip 打开地址 overlay，点击 chip 内搜索 utility button 打开搜索 overlay。
- 浏览态下 `PathChip` 内部包含 `历史路径` 与 `搜索` 两个 utility actions；overlay 可见时 utility strip 自动隐藏。
- `历史路径` 与 `More` 均已切换到 `FluentAvalonia` 的 `FAMenuFlyout`。历史路径菜单在 `Opening` 时由 `RecentPaths` 动态构建，实际导航仍通过 `SftpModeViewModel.NavigateHistoryPathCommand` 执行。
- `More` 菜单当前只保留 `显示隐藏文件` 勾选项，符合“轻动作 + 勾选项”的约束，不再承载 `TextBox` 或普通 `Flyout + ItemsControl` 结构。
- `SFTP` 内容列表已改为绑定 `VisibleItems`，过滤后无结果时显示 `FilteredEmptyStatePanel`，文案为“没有匹配结果”。
- 本轮只修改右栏 View / ViewModel / 样式 / Mock / 测试 / 文档，未触碰 `SshTerminalPane`、`RowStripedTerminalView` 与 `SSH.NET` 连接链路。
- GUI 手动清单尚未在当前 headless 会话中逐项点验；本轮以模板测试、状态测试、服务测试与全量 `dotnet test` 作为交付依据。
