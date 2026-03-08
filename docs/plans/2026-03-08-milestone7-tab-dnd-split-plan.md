# Milestone 7 - 标签页拖拽与四向分屏方案

- Date: 2026-03-08
- Project: SkylarkTerminal (Avalonia + FluentAvalonia)
- Status: 已确认（Architecture/Plan）
- Mode: 纯方案设计，不涉及业务代码改动

## 1. 目标与范围

### 1.1 目标
- 支持 `Workspace` 多 `Pane`。
- 支持同 `Pane` 内 Tab 重排。
- 支持跨 `Pane` 拖拽移动 Tab。
- 支持拖拽到目标 `Pane` 的四向槽位（Left/Right/Top/Bottom）触发分屏。
- 分屏后会话不中断，仅变更显示宿主。

### 1.2 非目标（本里程碑不做）
- 不做分屏布局持久化（重启后恢复单 Pane，参考竞品策略）。
- 不做多窗口分离（drag out 新窗口）。
- 不做业务连接逻辑重写（SSH/SFTP 业务协议不改）。

## 2. 已确认决策矩阵

- A2: `SplitTree` 布局模型，支持上下左右分屏。
- B2-Lite: 同 Pane 重排交给 `TabView CanReorderTabs=True`；自定义仅处理跨 Pane 与分屏动作。
- C2: `DragSessionService`（进程内拖拽会话上下文）。
- D3: 四向投放槽（Left/Right/Top/Bottom）。
- E1-R: 递归 `Grid + GridSplitter` 渲染（Horizontal 用列，Vertical 用行）。
- F1: 性能硬约束（`maxPaneCount` + `minPaneSize`）。
- G1: 空 Pane 自动回收并压缩树结构。
- H2: 会话上移并保持不中断（拖拽仅切宿主视图）。
- I1: 服务化边界（`WorkspaceLayoutService + SessionRegistryService`）。
- J2: Global Drag Overlay（Workspace 顶层统一投放层）。
- K1: 不做布局持久化。

## 3. 当前基线（调研结论）

- 当前 Workspace 是单 `TabView` + 单集合 `WorkspaceTabs`。
- 当前已启用 `CanReorderTabs/CanDragTabs`，但尚未做跨 Pane/分屏逻辑。
- 终端控件是 `RowStripedTerminalView : TerminalView`（Iciclecreek fork），非 `AvaloniaEdit`。
- SSH 会话来自 `SSH.NET ShellStream (xterm-256color)`，输出写入 `TerminalHost.Terminal.Write(...)`。

## 4. 目标架构设计

### 4.1 领域模型

#### 4.1.1 布局树（SplitTree）
- `WorkspaceLayoutNode`（抽象）
- `PaneNode : WorkspaceLayoutNode`
  - `PaneId`
- `SplitNode : WorkspaceLayoutNode`
  - `Orientation` (`Horizontal`/`Vertical`)
  - `Ratio` (0~1)
  - `First: WorkspaceLayoutNode`
  - `Second: WorkspaceLayoutNode`

#### 4.1.2 Pane 模型
- `WorkspacePaneViewModel`
  - `PaneId`
  - `ObservableCollection<WorkspaceTabItemViewModel> Tabs`
  - `WorkspaceTabItemViewModel? SelectedTab`

#### 4.1.3 会话模型（不中断）
- `SessionRegistryService`
  - `TabId -> ISshTerminalSessionHandle`
  - 提供 `GetOrCreate/Attach/Detach`，不因跨 Pane 移动而销毁会话

### 4.2 服务边界（I1）

- `IWorkspaceLayoutService`
  - 维护 `SplitTree`、`Pane` 注册表、分屏/合并操作
  - 对外 API（高层）：
    - `MoveTab(sourcePaneId, targetPaneId, tabId, index?)`
    - `SplitAndMove(sourcePaneId, tabId, dropDirection)`
    - `RecyclePaneIfEmpty(paneId)`

- `IDragSessionService`
  - 生命周期：`Start -> UpdateHover -> Commit/Cancel`
  - 记录拖拽源 `PaneId/TabId/TabRef`

- `ISessionRegistryService`
  - 管理 Tab 对应 session handle，确保拖拽过程中连接连续

### 4.3 UI 组合

- `WorkspaceHost`（主容器）
  - 递归渲染 `SplitTree` 为嵌套 `Grid`
  - 每个 `PaneNode` 渲染一个 `TabView`
  - 每个 `SplitNode` 渲染一个 `GridSplitter`

- `GlobalDragOverlay`（J2）
  - 挂载在 Workspace 顶层（高 z-index）
  - 根据当前 hover Pane 动态显示四向投放槽
  - 统一命中测试与预高亮反馈

## 5. 交互与事件流

### 5.1 同 Pane 重排（B2-Lite）
1. `TabView` 内建重排处理顺序变化。
2. `TabDragCompleted` 后同步 ViewModel 集合顺序（仅校准，不主导重排）。

### 5.2 跨 Pane 拖拽
1. `TabDragStarting` -> `DragSessionService.Start(...)`。
2. `GlobalDragOverlay` 根据指针位置计算目标 Pane 与候选槽位。
3. drop 到目标 Pane 非槽位区域时执行 `MoveTab(...)`。
4. `SessionRegistry` 保持原 session handle，不触发重连。

### 5.3 四向分屏（D3）
1. drop 命中 `Left/Right/Top/Bottom` 槽位。
2. `WorkspaceLayoutService.SplitAndMove(...)`：
   - 构造 `SplitNode`
   - 生成新 `PaneNode`
   - 将 Tab 移入新 Pane
3. 视图重绘，`GridSplitter` 可立即调节分割比例。

### 5.4 空 Pane 回收（G1）
1. Tab 移出后若源 Pane 空：
2. 删除空 Pane 节点；
3. 若父节点仅剩一个子节点，自动提升并压缩树；
4. 保证树无冗余层级。

## 6. 性能与可维护性策略

### 6.1 F1 硬约束
- `maxPaneCount`（建议默认 8，可配置）。
- `minPaneSize`（建议默认 340 px，可配置）。
- 超限行为：禁止继续 split，仅允许 tab move。

### 6.2 渲染性能
- Overlay 命中计算基于当前 hover Pane，不做全树高频扫描。
- 拖拽态 UI 更新节流（按帧或轻量 debounce）。
- 仅在树结构变化时重建对应局部 Grid。

### 6.3 维护策略
- 业务逻辑集中在 service 层，`MainWindowViewModel` 仅编排命令与绑定。
- 布局树与拖拽会话解耦，便于后续扩展（例如保存布局、弹出窗口）。

## 7. 视觉与交互规范（Win11 Fluent 风格）

- Global overlay 槽位使用半透明强调边框 + 轻量填充，避免干扰终端内容阅读。
- 高亮优先级：命中槽位 > 命中 Pane > 非命中状态。
- 槽位尺寸自适应 Pane 大小，小 Pane 使用比例阈值防止误触。
- 所有反馈应在 1 帧内可见，释放拖拽后立即清空 overlay 状态。

## 8. 错误与边界处理

- Drop 无效目标：取消操作并恢复原状态。
- 超过 `maxPaneCount`：显示轻提示并拒绝 split。
- 目标 Pane 尺寸不足：拒绝 split，允许普通 move。
- 拖拽取消（Esc/丢失焦点）：`DragSessionService.Cancel()` 并清空 overlay。
- 任何异常不应影响现有 SSH 会话生命周期。

## 9. 验收映射（对应需求）

1. 同区拖拽重排：
- 同一 `TabView` 内可自由改变顺序。

2. 边缘分屏：
- 拖拽到四向槽位任一方向并释放，可创建对应方向新 Pane，并移动 Tab。

3. 跨 Pane 移动：
- 已存在多 Pane 时，Tab 可在 Pane 之间移动。

4. 会话连续性：
- 拖拽/分屏全过程 SSH 会话不断线、终端输出连续。

5. 结构回收：
- 源 Pane 被移空后可自动回收，布局树保持最简。

## 10. 测试建议（实现阶段执行）

- 单元测试：
  - `WorkspaceLayoutService`：split/move/recycle/tree-compress
  - `DragSessionService`：state transition
  - `SessionRegistryService`：attach/detach continuity

- 集成/UI 测试：
  - 同 Pane reorder
  - 四向槽位 split
  - 多 Pane cross-move
  - maxPane/minPane 限制
  - 拖拽期间 session continuity

## 11. 里程碑交付结果定义

- 交付产物：
  - 上下左右分屏
  - 跨 Pane 拖拽
  - 全局四向投放 overlay
  - 空 Pane 自动回收
  - 会话不中断保证

- 明确不包含：
  - 布局持久化（K1）
  - 新窗口分离

