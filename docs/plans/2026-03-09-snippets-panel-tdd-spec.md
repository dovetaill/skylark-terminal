# 2026-03-09 Snippets Panel TDD Spec

## 目标

为 `Snippets` 面板补齐下一阶段的行为测试与回归测试，覆盖 repository、terminal bridge、mode view model、browse/editor/detail 视图以及与 `MainWindowViewModel` 的集成边界。

## 本轮已落地的核心类

### Models

- `SnippetItem`
- `SnippetCategory`
- `SnippetPanelState`
- `SnippetEditDraft`
- `SnippetStoreDocument`
- `SnippetStoreJsonContext`
- `SnippetDispatchResult`

### Services

- `ISnippetRepository`
- `JsonSnippetRepository`
- `MockSnippetRepository`
- `ITerminalCommandBridge`
- `TerminalCommandBridge`
- `MockTerminalCommandBridge`

### ViewModels

- `SnippetsModeViewModel`
- `MainWindowViewModel` 中的 snippets mode 装配逻辑

### Views

- `SnippetsModeView.axaml`
- `SnippetsModeView.axaml.cs`

## 关键方法与测试关注点

### `JsonSnippetRepository`

关键方法：

- `LoadAsync`
- `SaveAsync`

测试重点：

- 首次无文件时返回空分类
- 正常保存后可完整 round-trip `Category / Title / Content / Tags`
- 损坏 JSON 会备份为 `.broken` 并回退为空
- 原子写入时 `.tmp` 不应残留
- 分类和 snippet 的 `SortOrder / CreatedAt / UpdatedAt` 是否保留

### `TerminalCommandBridge`

关键方法：

- `PasteToActiveAsync`
- `RunInActiveAsync`
- `RunInAllTabsAsync`

测试重点：

- `Paste` 不追加换行
- `Run` 追加 `\r`
- `RunInAllTabs` 跳过无连接 tab、非 SSH tab、已断开 tab
- 某个 tab 发送失败时结果计数是否正确
- 空白内容是否允许发送，或是否需要后续加输入校验

### `SnippetsModeViewModel`

关键方法：

- `LoadAsync`
- `RebuildVisibleCategories`
- `SaveDraftAsync`
- `DeleteSelectedSnippetAsync`
- `PasteAsync`
- `RunAsync`
- `RunInAllTabsAsync`
- `BeginCreateCommand`
- `OpenEditCommand`
- `OpenViewMoreCommand`
- `CancelEditCommand`
- `StartNewCategoryCommand`

测试重点：

- `FilterText` 是否匹配分类名、标题、内容、tag
- `PanelState` 在 `Browse / Create / Edit / ViewMore` 间切换是否正确
- `SaveDraftAsync` 在创建态是否自动建分类
- `SaveDraftAsync` 在编辑态是否支持分类迁移
- `DeleteSelectedSnippetAsync` 是否尊重确认框返回值
- `RunInAllTabsAsync` 是否把目标数量传给确认对话框
- `CancelEditCommand` 是否清理 `Draft` 并返回 browse
- `SelectedSnippet` 与 `Draft` 是否始终保持一致

### `SnippetsModeView`

关键入口：

- `OnSnippetCardDoubleTapped`
- `OnSnippetActionClick`
- `OnSnippetContextActionClick`
- `OnEditorSaveClick`
- `OnEditorCancelClick`
- `OnEditorRemoveClick`
- `OnViewMoreBackClick`

测试重点：

- browse 模板包含 `VisibleCategories`、搜索框、context flyout
- editor 模板包含标题、分类、tags、内容、`Save / Cancel / Remove`
- detail 模板包含返回按钮、标题、分类、完整内容
- 右键菜单顺序必须保持 `Run / Edit / Run in all tabs / Copy / View more / Paste`
- 双击行为只走 `PasteAsync`

## 边缘情况

- `snippets.json` 被手工破坏后，启动时应恢复为空数据且备份损坏文件
- 新建 snippet 时分类名大小写只差大小写，是否应视为同一分类
- 编辑态从旧分类移动到新分类后，空分类是否保留
- `TagsText` 出现重复 tag、空 tag、前后空格时的归一化行为
- `RunInAllTabsAsync` 在 0 个目标 tab 时是否仍弹确认
- `SelectedWorkspaceTab` 为 `null` 时，`PasteAsync / RunAsync` 应返回无操作
- `ViewMore` 从右键进入后，返回 browse 是否保留原有搜索条件
- `PreviewText` 目前按固定长度截断，后续若改为按行数或按宽度裁剪，需要同步测试
- 当前 browse 卡片动作带已常驻显示；如果后续改成 hover-only，需要补模板与交互测试

## 下一阶段推荐测试清单

1. 为 `SnippetsModeViewModel` 增加编辑态迁移分类、取消编辑、删除后空分类处理测试。
2. 为 `TerminalCommandBridge` 增加失败 tab 统计测试与 0 目标 tab 测试。
3. 为 `JsonSnippetRepository` 增加时间戳、排序字段和 `.tmp` 清理测试。
4. 为 `SnippetsModeView` 增加右键菜单顺序和三态可见性模板测试。
5. 增加 `MainWindowViewModel` 集成测试，确认 snippets mode 在默认构造和 DI 构造下都能稳定加载。

## 当前回归基线

本轮结束时已验证：

- snippets 相关精选回归：`25` 个测试通过
- 全量测试：`105` 个测试通过
- 解决方案构建：成功，`0 Warning(s)`，`0 Error(s)`
