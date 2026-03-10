# 2026-03-09 Snippets TreeView Redesign Design

## 背景

当前 `Snippets` browse 视图仍然沿用卡片式布局：

1. 分类显示为标题 + 数量 chip，代码块显示为大卡片、预览文本和快捷动作带。
2. `Create / Edit` 表单仍暴露 `标签` 字段，不符合当前“代码块像资产节点一样简洁”的目标。
3. 分类管理能力不足，缺少删除分类入口；代码块右键菜单也缺少删除动作。
4. browse 结构和左侧资产列表的树状信息密度不一致，视觉上显得笨重。

本轮目标是把 `Snippets` browse 改为真正的 `TreeView`，让分类像左侧文件夹，代码块像左侧资产节点，同时补齐删除语义和更简洁的编辑流。

## 当前实现事实

- `Snippets` 仍挂在 `RightSidebarHostView -> SnippetsModeView -> SnippetsModeViewModel` 架构下，`DataContext` 链路已在上一轮修复。
- repository 数据结构仍是 `SnippetCategory -> ObservableCollection<SnippetItem>`，本地 JSON 已稳定落地。
- `SnippetsModeViewModel.RebuildVisibleCategories()` 已具备“分类命中 / 子项命中”过滤的基础能力，可以继续复用，而不必引入新的树节点持久化模型。
- `SnippetItem.Tags` 仍存在于运行时模型与持久化文档中，但当前需求不再要求 UI 暴露 tags。
- `AppDialogService` 当前只有“删除代码块”与“批量执行”确认接口，没有“删除分类”确认接口。

## 目标

- browse 改成真正的 `TreeView`，分类节点与代码块节点都采用“图标 + 名称”的轻量行式布局。
- 去掉当前分类数量 chip、代码块预览文本、tag 展示与底部快捷动作带。
- `Create / Edit` 去掉 tags 输入，仅保留标题、分类、内容。
- 分类输入支持“下拉选择已有分类”与“直接输入新分类名”。
- root / 分类 / 代码块 三层上下文菜单都补齐删除能力。
- 删除非空分类时弹确认，并明确提示会同时删除该分类下的所有代码块。

## 非目标

- 不改 `RightSidebarHostView`、`IRightPanelModeViewModel`、`TerminalCommandBridge` 主架构。
- 不引入 snippets 拖拽排序、多选、批量移动、拖拽进分类等扩展能力。
- 不修改 snippets 本地 JSON 的数据格式。
- 不引入全新的 tags 替代方案；tags 只从 UI 退场，不在本轮做数据迁移。

## 方案对比

### 方案 A：继续保留 `ItemsControl`，只把卡片样式做扁平化

优点：

- 改动最小
- 测试改动范围较小

缺点：

- 仍然不是树状信息架构
- 分类与代码块层级关系仍不清晰
- 不符合“像左侧资产列表”的目标

### 方案 B：在现有 `SnippetCategory + SnippetItem` 上直接改成 `TreeView`

做法：

- browse 改为 `TreeView ItemsSource="{Binding VisibleCategories}"`
- `SnippetCategory` 用 `TreeDataTemplate`，子项来自 `Items`
- `SnippetItem` 用普通 `DataTemplate`
- 搜索仍复用 `VisibleCategories` 过滤结果

优点：

- 不需要新增持久化层或树节点模型
- 最贴合左侧资产列表的树状交互
- 可以继续复用现有 `SnippetCategory.IsExpanded`

缺点：

- 需要重做 browse 模板和上下文菜单分层
- 要补“分类删除”和“编辑态去 tags”带来的回归测试

### 方案 C：新建独立 `SnippetTreeNode` 浏览模型

优点：

- browse 层语义最纯粹
- 未来扩展拖拽、多选更灵活

缺点：

- 本轮属于明显过度设计
- 数据投影和同步逻辑会额外增加复杂度

**最终决策：采用方案 B。**

## 最终设计

### 1. browse 信息架构

- 根层改为真正的 `TreeView`
- 一级节点：分类
  - 展现为文件夹图标 + 分类名
  - 去掉右侧数量 chip
- 二级节点：代码块
  - 展现为代码图标 + 代码块名称
  - 去掉预览文本、tag 区块、底部快捷动作带

### 2. 交互规则

- 双击代码块节点：直接 `PasteAsync` 到当前终端，不执行。
- 分类节点右键菜单：
  - `新建代码块`
  - `新建分类`
  - `删除分类`
- 代码块节点右键菜单：
  - `粘贴`
  - `运行`
  - `在全部标签页运行`
  - `编辑`
  - `查看详情`
  - `删除`
- browse 空白区 root 菜单继续保留：
  - `新建代码块`
  - `新建分类`
  - `从剪贴板创建`
  - `清空搜索`

### 3. 编辑流

- `Create / Edit` 表单只保留：
  - 标题
  - 分类
  - 内容
- 去掉 tags 输入，也不再显示 tags watermark。
- 分类字段改为可输入的下拉选择：
  - 可直接选择已有分类
  - 可输入新分类名，保存时自动创建
- 当前 `StartNewCategoryCommand` 触发的“新建分类按钮”入口可以从表单 UI 中移除，因为 editable selector 已覆盖该场景。

### 4. 删除语义

- 删除代码块：
  - 保持二次确认
- 删除空分类：
  - 普通删除确认
- 删除非空分类：
  - 明确提示“将同时删除该分类下的所有代码块”
  - 用户确认后删除分类及其全部子项

### 5. tags 兼容策略

- `SnippetItem.Tags` 和现有 JSON 字段继续保留，避免破坏已有数据格式。
- browse 不再显示 tags。
- 搜索不再依赖 tags，只匹配分类名、代码块标题和代码块内容。
- `Create / Edit` 不再允许修改 tags：
  - 新建代码块时写入空 tags
  - 编辑已有代码块时保留原有 tags，不因 UI 移除而静默清空

## 涉及组件

- `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml`
- `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml.cs`
- `src/SkylarkTerminal/ViewModels/RightPanelModes/SnippetsModeViewModel.cs`
- `src/SkylarkTerminal/Models/SnippetsText.cs`
- `src/SkylarkTerminal/Services/IAppDialogService.cs`
- `src/SkylarkTerminal/Services/AppDialogService.cs`
- `src/SkylarkTerminal/Services/Mock/MockAppDialogService.cs`
- `src/SkylarkTerminal/Views/MainWindow.axaml`
- `tests/SkylarkTerminal.Tests/*Snippets*.cs`

## 风险与回滚

### 风险

- `TreeView` 模板切换后，root / 分类 / 代码块 三层右键菜单的命中边界比当前更复杂，需要补模板测试避免冒泡到通用 tools menu。
- 移除 tags UI 后，如果 `SaveDraftAsync()` 仍按旧逻辑写入 `Draft.TagsText`，会把旧数据清空，因此必须显式保留已有 tags。
- editable 分类输入若绑定方式不稳，可能出现“只能选已有分类，不能输入新分类”或“输入新分类后无法保存”的问题。

### 回滚

- 若 `TreeView` 命中或展开行为不稳定，可回退到当前 `ItemsControl` browse 实现。
- 若 editable 分类输入存在控件兼容性问题，可临时退回“文本输入 + 已有分类建议列表”，但不应恢复 tags 字段。

## 验收标准

1. browse 视图改为 `TreeView`，分类与代码块都呈现为轻量行式结构。
2. 分类数量 chip、代码块预览、tag 区块和底部快捷动作带从 browse 移除。
3. `Create / Edit` 不再出现 tags 输入，分类字段既能选已有分类，也能输入新名字。
4. root / 分类 / 代码块右键菜单都具备对应删除入口。
5. 删除非空分类时，会明确提示将同时删除该分类下的所有代码块。
6. 双击代码块节点仍然是直接粘贴到当前终端。
