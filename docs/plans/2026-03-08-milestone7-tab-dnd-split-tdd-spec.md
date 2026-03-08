# Milestone 7 TDD Handoff Spec

- Date: 2026-03-08
- Project: SkylarkTerminal
- Scope: Tab drag-and-drop + four-direction split workspace
- Input Plan: `docs/plans/2026-03-08-milestone7-tab-dnd-split-plan.md`

## 1. 目标与交付状态

本阶段已完成以下能力落地：

1. `SplitTree` 布局模型（`PaneNode` / `SplitNode`）与布局服务。
2. 多 Pane 容器渲染（递归 `Grid + GridSplitter`）。
3. `TabView` 拖拽事件接线与全局 overlay 反馈。
4. 跨 Pane move、四向 split、空 Pane 回收。
5. session continuity（拖拽/分屏过程不销毁 SSH 会话）。
6. 约束处理（`maxPaneCount` / `minPaneSize`）与失败回退。

## 2. 核心类与职责

### 2.1 布局与模型

- `WorkspaceLayoutNode`  
  抽象布局节点基类，提供 `NodeId`。
- `PaneNode`  
  叶子节点，持有 `PaneId`。
- `SplitNode`  
  非叶子节点，持有 `Orientation`、`Ratio`、`First`、`Second`。
- `WorkspaceSplitOrientation`  
  枚举：`Horizontal` / `Vertical`。
- `WorkspaceDropDirection`  
  枚举：`Left` / `Right` / `Top` / `Bottom`。
- `WorkspaceDragSession` / `WorkspaceDragHoverTarget`  
  拖拽会话上下文与 hover 命中状态。

### 2.2 服务层

- `IWorkspaceLayoutService` / `WorkspaceLayoutService`
  - 维护布局树与 pane/tab 映射。
  - 提供 `MoveTab` / `SplitAndMove` / `RecyclePaneIfEmpty`。
- `IDragSessionService` / `DragSessionService`
  - 管理 `Start -> UpdateHover -> Commit/Cancel`。
- `ISessionRegistryService` / `SessionRegistryService`
  - 管理 `TabId -> ISshTerminalSessionHandle`。
  - 提供 `GetOrCreateAsync`、`Attach`、`TryGet`、`TryDetach`、`DisposeAsync`。
- `ISshTerminalSessionHandle`
  - 抽象 tab 与会话实例绑定（避免 UI 容器切换触发会话销毁）。

### 2.3 ViewModel / View

- `WorkspacePaneViewModel`
  - `PaneId`、`Tabs`、`SelectedTab`。
- `MainWindowViewModel`
  - 新增 `WorkspacePanes`、`SelectedWorkspacePane`、`WorkspaceLayoutRoot`。
  - 新增拖拽 overlay 状态：`IsWorkspaceDragOverlayVisible`、`WorkspaceDragHoverPaneId`、`WorkspaceDragHoverDirection`。
  - 新增拖拽流程编排：`BeginWorkspaceDragPreview`、`UpdateWorkspaceDragPreview`、`CompleteWorkspaceDragDrop`、`CancelWorkspaceDragPreview`。
  - 新增约束：`MaxWorkspacePaneCount`、`WorkspaceMinPaneSize`。
- `WorkspaceHost`
  - 递归渲染 SplitTree。
- `WorkspacePaneHost`
  - 承载单 pane `TabView`，处理拖拽事件。
- `WorkspaceDragOverlay`
  - 顶层四向槽位与 pane hover 可视反馈。

## 3. 关键方法行为契约

### 3.1 `WorkspaceLayoutService`

1. `MoveTab(sourcePaneId, targetPaneId, tabId, index?)`
   - 同 pane: 支持 reorder。
   - 跨 pane: 从 source 移除并插入 target。
2. `SplitAndMove(sourcePaneId, tabId, dropDirection)`
   - 在目标 pane 位置创建 `SplitNode` + 新 pane。
   - 按方向决定新 pane 位于 `First/Second`。
3. `RecyclePaneIfEmpty(paneId)`
   - 空 pane 删除，父 split 结构自动压缩。

### 3.2 `MainWindowViewModel`

1. `BeginWorkspaceDragPreview(...)`
   - 启动 drag session，展示 overlay。
2. `UpdateWorkspaceDragPreview(...)`
   - 更新 hover pane 与方向，刷新槽位热点。
3. `CompleteWorkspaceDragDrop(targetPaneId, dropDirection, targetIndex?)`
   - `dropDirection == null` 执行 move（同 pane reorder / 跨 pane move）。
   - `dropDirection != null` 执行 split + move。
   - 跨 pane split 失败时执行回退（恢复 source/target 集合与选中态）。
   - 按需触发布局 root 变化通知。
4. `CancelWorkspaceDragPreview()`
   - 取消会话并清空 overlay 状态。

### 3.3 `WorkspacePaneHost`

1. `OnWorkspaceTabDragStarting`
   - 设置活动 pane，调用 `BeginWorkspaceDragPreview`。
2. `OnWorkspaceTabStripDragOver`
   - 无 active session 拒绝拖拽。
   - 根据 pointer 与 pane 尺寸计算 drop direction。
3. `OnWorkspaceTabStripDrop`
   - 提交 drop；失败则调用取消逻辑。
4. `ResolveDropDirection`
   - 命中区域阈值 + `minPaneSize` 双重约束。
   - 尺寸不足时返回 `null`（退化为普通 move，不 split）。

## 4. 边缘情况（Edge Cases）清单

1. Drop 到无效目标 pane：取消并清空 overlay。
2. 拖拽过程中 session 丢失：提交失败并取消拖拽态，不影响其他 session。
3. `maxPaneCount` 超限：拒绝 split，仅允许 move，给出用户提示。
4. pane 尺寸不足（小于 `2 * minPaneSize`）：拒绝该方向 split。
5. 跨 pane split 预移动成功但 split 失败：必须回退到原 pane。
6. 源 pane 迁出后为空：触发 `RecyclePaneIfEmpty`，并保持树最简。
7. 同 pane reorder：不应触发 split/回收逻辑。
8. 结束事件先于 drop 或 drop 丢失：`TabDragCompleted` 仍要保证清理状态。
9. stale/disconnected session handle：`SessionRegistryService.GetOrCreateAsync` 需重建并释放旧实例。

## 5. 当前测试覆盖（已完成）

### 5.1 服务单元测试

- `WorkspaceLayoutServiceTests`
  - 初始 root pane
  - split 新建 pane + tab 迁移
  - 跨 pane move
  - 空 pane 回收与树压缩
  - 非法 pane 输入
- `DragSessionServiceTests`
  - start/update/commit/cancel 状态迁移
  - inactive update 无副作用
- `SessionRegistryServiceTests`
  - 首次创建与复用
  - stale handle 重建
  - dispose 释放
  - attach/detach 行为

## 6. 下一阶段 TDD 建议（待补）

### 6.1 MainWindowViewModel 行为测试

1. `CompleteWorkspaceDragDrop` 的 move 路径：
   - same pane reorder 索引边界（`null`/负数/超界）。
2. `CompleteWorkspaceDragDrop` 的 split 路径：
   - 四方向分别创建树结构断言。
3. 失败回退：
   - 预移动后 split 失败，source/target tabs 与选中态一致性。
4. 约束行为：
   - `maxPaneCount` 命中后提示文案与行为退化。

### 6.2 视图层交互测试（集成/UI）

1. `WorkspacePaneHost` 指针命中：
   - 边缘区域命中方向正确。
   - 小 pane 尺寸下不触发 split。
2. overlay 热点状态：
   - `UpdateWorkspaceDragPreview` 后 Left/Right/Top/Bottom Hot 标志互斥正确。
3. drag 生命周期：
   - `TabDragStarting -> DragOver -> Drop -> DragCompleted` 状态闭环。

### 6.3 会话连续性回归

1. 跨 pane move 前后同一 `TabId` 对应同一 session 实例。
2. split 后不触发额外 reconnect/disconnect。
3. close tab 时才触发 registry dispose。

## 7. 测试数据与替身建议

1. `ISshConnectionService` 使用 fake/stub，记录 create/disconnect 次数与参数。
2. `ISshTerminalSession` 使用可控连接态 fake（可模拟 stale）。
3. `IWorkspaceLayoutService` 可在 VM 测试中使用真实实现，验证树状态与 pane ids。

## 8. 验收基线（TDD Done 定义）

1. 同 pane reorder / 跨 pane move / 四向 split 均有自动化测试覆盖。
2. `maxPaneCount` 与 `minPaneSize` 限制有正反用例。
3. 回收逻辑和树压缩有结构断言。
4. 拖拽全过程 session continuity 有验证。
5. 所有测试通过，且 `dotnet build` 无 error。

## 9. 附注

- 当前仓库 `.gitignore` 包含 `tests/*` 规则。  
  如需将新增测试文件纳入版本控制，需先调整 ignore 规则或显式强制跟踪。

