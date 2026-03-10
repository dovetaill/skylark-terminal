# 2026-03-09 Snippets TreeView Redesign TDD Spec

## 目标

为 `Snippets TreeView Redesign` 提供下一阶段测试输入，覆盖树状 browse、分类级删除确认、editable 分类输入以及 tags 兼容策略。

## 本轮核心类与文件

- `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml`
  - browse 从 `ItemsControl + card` 切到真正的 `TreeView`
  - `SnippetCategory` 使用 `TreeDataTemplate`
  - 分类节点和代码块节点改为“图标 + 名称”的轻量树节点
  - root / category / item 三层 `ContextFlyout` 已拆分
  - create / edit 表单已切到 editable `ComboBox` 分类输入
  - tags 输入与 browse tags 展示已从 UI 移除
- `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml.cs`
  - `OnSnippetCardDoubleTapped(...)` 仍保持 `PasteAsync`
  - `HandleSnippetActionAsync(...)` 现在分发 `delete-snippet`
  - `HandleCategoryActionAsync(...)` 现在分发 `new-snippet / new-category / delete-category`
- `src/SkylarkTerminal/ViewModels/RightPanelModes/SnippetsModeViewModel.cs`
  - `RebuildVisibleCategories()` 搜索仅匹配分类名、标题、内容
  - 新增 `DeleteCategoryAsync(...)`
  - 新增 `DeleteSnippetAsync(...)`
  - 新增 `CategoryOptions`
  - `PersistAndReturnToBrowseAsync(...)` 会同步刷新 `CategoryOptions`
- `src/SkylarkTerminal/Services/IAppDialogService.cs`
  - 新增 `ShowDeleteSnippetCategoryConfirmAsync(string categoryName, int snippetCount)`
- `src/SkylarkTerminal/Services/AppDialogService.cs`
  - 分类删除确认现在区分空分类与非空分类文案
- `src/SkylarkTerminal/Services/Mock/MockAppDialogService.cs`
  - 增加分类删除确认结果与 `LastDeleteCategoryIncludesChildren` 观测字段
- `src/SkylarkTerminal/Models/SnippetsText.cs`
  - 增加分类删除标题与消息构造函数
- `src/SkylarkTerminal/Views/MainWindow.axaml`
  - 增加 `snippets-category-context-flyout` 与 `snippets-item-context-flyout` 的 presenter 样式

## 关键方法与测试关注点

- `SnippetsModeViewModel.RebuildVisibleCategories()`
  - 必须只匹配分类名、代码块标题、代码块内容
  - 不应再通过 `Tags` 命中过滤结果
- `SnippetsModeViewModel.DeleteCategoryAsync(...)`
  - 删除空分类时应走普通确认
  - 删除非空分类时必须带出“会同时删除子代码块”的确认语义
  - 删除当前选中 snippet 所属分类后，应清空 `SelectedSnippet`
- `SnippetsModeViewModel.DeleteSnippetAsync(...)`
  - item 右键 `delete-snippet` 应直接走公开方法，而不是在 View 层写删除逻辑
- `SnippetsModeViewModel.SaveDraftAsync(...)`
  - 新建 snippet 时，如果分类名不存在，应自动创建分类
  - 编辑已有 snippet 时，如果 UI 未暴露 tags，保存后仍必须保留原有 `item.Tags`
  - 新建 snippet 时应写入空 tags，而不是 `null`
- `SnippetsModeViewModel.CategoryOptions`
  - 必须反映当前所有分类名，供 editable `ComboBox` 使用
  - 新增分类、删除分类、重命名分类后都应重新构建
- `SnippetsModeView.OnSnippetCardDoubleTapped(...)`
  - 双击代码块节点仍然必须只执行 `PasteAsync`
- `SnippetsModeView.HandleCategoryActionAsync(...)`
  - `new-snippet` 应预填当前分类名
  - `delete-category` 应调用 `DeleteCategoryAsync(...)`
- `AppDialogService.ShowDeleteSnippetCategoryConfirmAsync(...)`
  - `snippetCount == 0` 与 `snippetCount > 0` 必须生成不同文案

## 下一阶段建议测试点

- ViewModel 行为测试
  - 编辑已有 snippet 后保存，原 `Tags` 必须保持不变
  - 新建 snippet 后保存，`Tags` 必须为 `[]`
  - 删除分类后，`CategoryOptions` 必须同步移除对应名称
  - 新建新分类名保存后，`CategoryOptions` 必须立刻包含新名称
- 模板结构测试
  - `TreeView` 节点模板必须同时存在 root/category/item 三层菜单
  - 分类节点与代码块节点必须包含对应图标 glyph
  - create / edit 表单不应再出现 `StartNewCategoryCommand`
- 对话框测试
  - 分类删除消息中应包含准确的 `snippetCount`
  - 空分类删除消息不应误报级联删除
- 集成测试
  - 从分类节点右键进入 `新建代码块` 后，editor 中应预填当前分类
  - 从 item 节点删除最后一个 snippet 后，空分类是否保留需有明确测试

## 重点边缘情况

- editable `ComboBox` 允许自由输入，需验证输入新分类名时不会被现有选项覆盖
- 当前 `SaveDraftAsync()` 仍依赖 `Draft.TagsText` 作为内部过渡字段；如果后续有人在 `BeginCreate()` 或 `OpenEdit()` 改动草稿初始化，可能意外丢失旧 tags
- 删除非空分类后，当前实现会直接删除整个分类；若后续引入“移动代码块到其他分类”，测试基线需要重写
- `TreeViewItem.IsExpanded` 目前只在分类层双向绑定；若后续引入更深层节点或虚拟化，需要补展开状态保持测试
- `CategoryOptions` 当前按 `StringComparer.OrdinalIgnoreCase` 去重；若后续要求保留大小写差异，需要重新定义分类唯一性

## 已有验证命令

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsTreeViewTemplateTests|FullyQualifiedName~SnippetsTreeContextMenuTemplateTests|FullyQualifiedName~SnippetsCategoryDeletionTests|FullyQualifiedName~SnippetsEditorFormTemplateTests" -v minimal
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal
dotnet build SkylarkTerminal.slnx -v minimal
```

## 手工 Smoke 建议

- 在 browse 空白区右键，确认 root 菜单仍为 `新建代码块 / 新建分类 / 从剪贴板创建 / 清空搜索`
- 展开分类树，分别右键分类节点与代码块节点，确认菜单没有串层
- 双击代码块节点，确认只粘贴到当前终端，不自动执行
- 删除非空分类，确认弹窗明确提示会同时删除该分类下的所有代码块
- 在 create / edit 表单里：
  - 选择已有分类保存
  - 直接输入新分类名保存
  - 编辑已有 snippet 后保存，确认旧 tags 未被清空

## 当前 CLI 验证范围

- 已完成 snippets treeview 相关定向回归
- 当前命令行环境未执行 GUI 手工 smoke；若需要视觉/交互验收，应在可用桌面环境补测
