# 2026-03-09 RightSidebar Layout Bugfix2 Design

## 实现状态（2026-03-09）

本设计基线已在 `feature/right-sidebar-bugfix2` 分支落地，最终命名与实现状态如下：

- `SftpCommandBarHeaderView` 已替换为 `SftpToolbarHeaderView`
- `SFTP` header 已改为固定 `Grid` toolbar，地址输入区不再进入 overflow/flyout
- `RightToolsModeItem` 已引入 `RightModeIconKey`、`TooltipZh` 与中文 mode rail metadata
- `ModeActionDescriptor` / `SftpToolbarActionDescriptor` 已统一承载中文 `LabelZh` / `TooltipZh`
- `SftpModeViewModel` 已持有 `Items`、`LoadState`、`ErrorMessage`，并暴露 `ActivateAsync` / `CommitAddressAsync`
- `SFTP` 内容区已切换为 `Idle / Loading / Loaded / Empty / Error` 状态模板与双层 row 文件列表

最终验证结果：

- 右栏定向回归：`15/15 PASS`
- 全量测试：`85/85 PASS`
- 构建：`dotnet build SkylarkTerminal.slnx -v minimal` 成功，`0 Warning(s), 0 Error(s)`

## 背景

当前 `RightSidebar` 已完成 mode-based host 架构、`SFTP` 独立 header slot 和单栏内容区改造，但仍存在一组连续性问题：

1. 顶部 `Snippets / History / SFTP` mode rail 图标语义弱，`SFTP` 图标不符合远程文件管理语境。
2. 顶部 mode rail 缺少中文悬浮提示，`History` 动作与 `SFTP` 导航命令仍混用英文 tooltip / label。
3. `SFTP` 模式从 mode rail 直接切换时，文件列表存在空白态，容易表现为“像是没渲染出来”。
4. `SFTP` 头部当前依赖 `FluentAvalonia CommandBar` 动态 overflow；在右栏窄宽度下，地址输入框会被挤入三点菜单，继而引出失焦回收异常、弹层层级观感错误、交互职责混乱。

本轮目标不是做局部补丁，而是在不进入业务实现的前提下，重新确认 `RightSidebar` 的稳定设计基线，作为后续实现与测试的唯一依据。

## 调研结论

### 当前相关源码

- 宿主视图：`src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
- `SFTP` 头部：`src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml`
- `Snippets / History` 动作头：`src/SkylarkTerminal/Views/RightHeaders/ActionStripHeaderView.axaml`
- `SFTP` 内容区：`src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml`
- 右栏入口状态：`src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`
- `SFTP` 模式状态：`src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- `SFTP` 路径历史：`src/SkylarkTerminal/Services/SftpNavigationService.cs`

### 终端控件实现确认

- 当前真实终端不是 `AvaloniaEdit`
- 当前终端也不是纯手写 `Control`
- 实际终端宿主位于 `src/SkylarkTerminal/Views/SshTerminalPane.axaml`
- 实际渲染控件为 `RowStripedTerminalView`
- 该控件位于 `src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork/RowStripedTerminalView.cs`
- 该控件继承 `Iciclecreek.Avalonia.Terminal` 的 `TerminalView`，项目在本地 fork 中增加了 row stripe、theme palette 和 fallback highlighting

### 历史提交脉络

- `4c41859` `feat: add xftp-like navigation toolbar in sftp mode`
- `3cac796` `refactor: introduce right panel header node architecture`
- `ef9dee8` `refactor: render right sidebar header through mode-specific slot`
- `880128a` `refactor: move sftp navigation into right sidebar command bar header`
- `6138b73` `feat: add compact-expand interaction for sftp address editor`
- `9e18542` `style: restyle right sidebar mode rail and sftp header controls`
- `670a284` `feat: 合并右侧栏布局修复到 master`

### 已确认的直接问题根因

- mode rail 当前仍是 glyph-only，缺少语义化 icon 系统与统一中文 tooltip。
- `History` 的 `ModeActionDescriptor.Tooltip` 与 `SFTP CommandBarButton.Label` 仍是英文字符串来源。
- `BuildSftpModeActions()` 返回空集合是刻意设计，用于避免 `SFTP` 同时显示通用动作栏和专属导航栏；因此“动作栏为空”本质上是旧认知与新结构不一致，不是独立 feature 缺失。
- 但 `SFTP` 文件列表空白是真问题：mode rail 切换只改变 `SelectedRightToolsView`，没有稳定触发 `ShowSftpTools()` 或等价加载入口。
- `CommandBar + CommandBarElementContainer + IsDynamicOverflowEnabled=True` 不适合窄右栏中的可聚焦 `TextBox`。地址编辑器被挤进 overflow 后，light dismiss、focus return、popup 层级体验都会变差。

## 目标

- 建立语义化、可扩展的 `RightSidebar` mode rail icon 系统
- 统一右栏中文 tooltip / label / 状态文案来源，消除中英混杂
- 让 `SFTP` 模式具备完整可观测状态：`Idle / Loading / Loaded / Empty / Error`
- 去除 `SFTP` 头部对 `CommandBar` 动态 overflow 的依赖
- 让 `SFTP` 内容区在视觉上不再像“空白占位”，而像一个完整可用的远程文件面板
- 保持现有 `MVVM + DI + mode host` 架构，不改 SSH terminal 主链路

## 非目标

- 不重写 `SSH` 终端控件
- 不替换 `Iciclecreek.Avalonia.Terminal.Fork`
- 不在本轮引入真实上传、下载、删除、重命名等 `SFTP` 业务能力
- 不在本轮新增复杂动画系统
- 不在本轮自动生成 implementation plan，除非用户后续明确要求

## 最终决策

本次确认的设计基线为：

- `A2` 顶部 mode rail 升级为更明确的 icon system，而不是简单替换单个 glyph
- `B1` tooltip / label 文案统一收口到 metadata 层
- `C2` `SFTP` 加载职责回收到 mode 自身，建立模式级状态管理
- `D2` `SFTP` 头部放弃 `CommandBar` 作为宿主，改为自定义 `Grid` toolbar
- `E2` 引入语义图标层，支持 icon key 到具体图标渲染的映射
- `F2` 建立完整 `SFTP` 状态机：`Idle / Loading / Loaded / Empty / Error`
- `G2` `SFTP` 文件列表采用双层 row 结构
- `H1` `More` 菜单只承载纯动作，不放任何可聚焦输入控件

## 方案对比与裁决

### 设计点 1：Mode Rail 图标体系

#### 方案 E1：继续使用 glyph 字符串

优点：

- 改动小
- 可快速替换 `SFTP` 图标

缺点：

- 图标语义仍然绑定到字体码位
- 选中态无法自然演进为 `Regular / Filled`
- ViewModel 继续承担 UI 细节

#### 方案 E2：引入语义图标层

做法：

- `RightToolsModeItem` 不再只暴露 `Glyph`
- 改为暴露 `ModeIconKey`、`TitleZh`、`TooltipZh`、可选 `SelectedIconKey`
- 在 View 层或 icon template 层决定如何渲染 `SymbolIcon`、`PathIcon` 或 font icon

优点：

- 是更根本的解耦
- 后续可自然支持 `Regular -> Filled`、hover/selected 视觉差异
- 更符合 Fluent 风格的语义图标系统

缺点：

- 需要新增 icon mapping 约定

**最终决策：采用 E2**

### 设计点 2：文案来源

#### 方案 B1：metadata 收口

做法：

- mode rail 文案、动作 tooltip、`SFTP` header command label 全部通过 metadata 提供
- XAML 只绑定，不内联写死中英文字符串

优点：

- 一致性强
- 易于测试
- 后续国际化有扩展空间

缺点：

- 要调整若干模型定义

#### 方案 B2：XAML 局部写死中文

优点：

- 修改最快

缺点：

- 文案分散
- 未来容易再次中英混杂

**最终决策：采用 B1**

### 设计点 3：SFTP 数据加载职责

#### 方案 C1：在宿主继续补触发

优点：

- 最小变更

缺点：

- 模式切换与数据加载继续耦合在 `MainWindowViewModel`
- 后续错误态与重试逻辑会继续散落

#### 方案 C2：由 `SftpModeViewModel` 自有激活/刷新入口

做法：

- 宿主只负责切模式
- `SftpModeViewModel` 负责 `ActivateAsync / RefreshAsync / NavigateAsync`
- 数据集合、状态、错误文案都归属到该 mode

优点：

- 职责清晰
- 更适配后续真实 `SFTP` 能力
- 可稳定支撑 `Loading / Empty / Error`

缺点：

- 需要补模式生命周期与通知约定

**最终决策：采用 C2**

### 设计点 4：SFTP Header 宿主

#### 方案 D1：保留 `CommandBar`，限制 overflow

优点：

- 沿用现有 FluentAvalonia 控件

缺点：

- 仍受窄宽度下 overflow 行为约束
- 输入控件与命令容器继续混用，脆弱性高

#### 方案 D2：改为自定义 `Grid` toolbar

做法：

- 左侧固定 `Back / Forward`
- 中部固定 `Address chip / editor`
- 右侧固定 `Refresh / Up / More`
- `More` 使用显式 `Flyout / MenuFlyout`

优点：

- 布局完全可控
- 地址输入框不会被塞进三点菜单
- 更容易保证失焦回收、focus return 和 light dismiss 正常

缺点：

- 需要自己维护 header 结构

**最终决策：采用 D2**

### 设计点 5：SFTP 状态表达

#### 方案 F1：只做 `Loading / Empty / Loaded`

优点：

- 简单

缺点：

- 出错场景难以区分为“无数据”还是“请求失败”

#### 方案 F2：完整状态机

状态：

- `Idle`
- `Loading`
- `Loaded`
- `Empty`
- `Error`

优点：

- 可观测性完整
- 用户能理解为什么没有内容
- 测试边界清晰

缺点：

- 模板和状态切换略多

**最终决策：采用 F2**

### 设计点 6：SFTP 文件列表表现

#### 方案 G1：维持单行列表

优点：

- 紧凑

缺点：

- 视觉表现偏弱
- 右栏窄宽度下容易像“临时占位内容”

#### 方案 G2：双层 row

做法：

- 第一行：文件/目录 icon + `Name`
- 第二行：路径片段、类型、大小等辅助信息
- 支持 hover、selected、tooltip
- 目录优先排序，文件次之

优点：

- 现代感更强
- 视觉层次更清楚
- 更符合“高颜值桌面客户端”的目标

缺点：

- 单项高度会略增

**最终决策：采用 G2**

### 设计点 7：More 菜单边界

#### 方案 H1：只放纯动作

例如：

- `刷新`
- `复制当前路径`
- `在新标签打开`
- `显示隐藏文件`

优点：

- 最稳
- 弹层职责清晰
- 不再出现输入控件进入 popup 的问题

缺点：

- 扩展性保守

#### 方案 H2：小型设置面板化

优点：

- 后续扩展丰富

缺点：

- 很容易膨胀成第二个子面板

**最终决策：采用 H1**

## 目标架构

### 1. Mode Rail

- 顶部继续保留“单选模式切换”语义
- 视觉从 glyph-only 过渡为 semantic icon tile
- 模型层不再暴露底层图标字符，而是暴露 icon key 与中文元数据
- 选中态允许使用与非选中态不同的图标样式

### 2. Header Layer

- `Snippets / History` 继续使用轻量 action strip
- `SFTP` 改为独立 `Grid` toolbar header，不再使用 `CommandBar`
- `SFTP` header 中的地址编辑器始终属于固定布局区域，不允许进入 overflow 或 flyout

### 3. SFTP Mode Lifecycle

- `SftpModeViewModel` 拥有模式激活入口
- 首次切入 `SFTP` 时负责加载目录
- 后续 `Refresh / Navigate / Retry` 均由模式自身驱动
- `MainWindowViewModel` 只负责模式选择与宿主绑定

### 4. SFTP Content Layer

- 内容区根据状态机切换：
  - `Idle`：未激活占位
  - `Loading`：骨架或 loading hint
  - `Empty`：明确空目录提示
  - `Error`：中文错误文案 + retry
  - `Loaded`：双层 row 文件列表

## 实施步骤

1. 调整右栏 metadata 模型
   - 为 mode rail 引入 icon key / 中文 tooltip 元数据
   - 为动作与 header command 建立统一文案来源
2. 重构 `SFTP` header 结构
   - 用自定义 `Grid` toolbar 替代 `CommandBar`
   - 固定地址区，不参与 overflow
   - `More` 改为显式轻动作 flyout
3. 下沉 `SFTP` 生命周期到 `SftpModeViewModel`
   - 模式切换时触发激活逻辑
   - 建立 `Idle / Loading / Loaded / Empty / Error`
4. 重绘 `SFTP` 内容区
   - 增加双层 row 模板
   - 增加目录/文件 icon、辅助信息与 tooltip
5. 补齐测试
   - 文案来源
   - 状态机
   - mode rail 选择行为
   - `More` 菜单边界
   - 地址输入框 focus / dismiss 行为

## 风险与回滚

### 风险

- `SFTP` 生命周期下沉后，宿主与 mode 的边界如果定义不清，可能出现重复加载或状态不同步。
- 自定义 `Grid` toolbar 会放弃 `CommandBar` 的现成适配逻辑，需要自行处理窄宽度下的压缩策略。
- 双层 row 若信息密度控制不好，右栏会显得拥挤而不是精致。
- 如果 `Flyout` 的 show mode、placement 和 dismiss 策略选型不当，仍可能出现点击外部关闭不自然的问题。

### 回滚策略

- 结构回滚优先级：
  1. 保留 `C2 / F2` 的状态机与加载职责重构
  2. 如 header 方案验证不佳，可单独回退 `D2` 到旧 header，但不恢复地址输入框进入 overflow 的设计
- Git 回滚建议：
  - 以本轮实现提交为单位进行 `git revert`
  - 不建议通过手工摘除局部 XAML 恢复旧行为，容易引入 header slot 与 mode state 脱节

## 验证清单

- mode rail 中 `Snippets / History / SFTP` 均有正确中文 tooltip
- `SFTP` icon 语义与文件管理场景一致，选中态清晰但不过重
- `History` 与 `SFTP` 头部动作文案全部为中文，无残留英文 tooltip / label
- 从 mode rail 直接切入 `SFTP` 时，内容区能稳定进入 `Loading -> Loaded/Empty/Error`
- `SFTP` 地址输入框始终位于固定 header 区，不会进入三点菜单
- 地址输入框聚焦后，点击其他区域可自然失焦并回收
- `More` 菜单关闭后不会表现为系统级顶置异常
- `More` 菜单中不包含任何 `TextBox` 或其他复杂输入控件
- `SFTP` 内容区在空目录、失败目录、正常目录三种情况下都有明确视觉反馈
- `Snippets / History` 现有模式切换不回归
- 不影响 `SshTerminalPane` 与 `RowStripedTerminalView`

## 参考资料

- FluentAvalonia `CommandBar` docs: <https://amwx.github.io/FluentAvaloniaDocs/pages/Controls/CommandBar>
- FluentAvalonia `CommandBarElementContainer` docs: <https://amwx.github.io/FluentAvaloniaDocs/pages/Controls/CommandBarElementContainer>
- Avalonia `ToolTip` docs: <https://docs.avaloniaui.net/docs/reference/controls/tooltip>
- Avalonia `Flyout` docs: <https://docs.avaloniaui.net/docs/reference/controls/flyouts>
- Avalonia `Popup.ShouldUseOverlayLayer` API: <https://api-docs.avaloniaui.net/docs/P_Avalonia_Controls_Primitives_Popup_ShouldUseOverlayLayer>
