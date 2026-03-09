# Snippets Panel Bugfix TDD Spec

## 目的

为 `Snippets` 面板本轮 bugfix 提供下一阶段测试输入，覆盖：

- `DataContext` 绑定链路修复
- root actions 与剪贴板读写
- `SnippetsText` 中文文案层
- browse root `ContextFlyout`
- `Create / Edit / ViewMore` 状态化动作区

## 本轮核心类与文件

- `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
  - `SnippetsModeView` 现在显式接收 `ActiveRightMode` 作为 `DataContext`
- `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml`
  - browse/create/edit/view-more 四个模板都直接绑定 `SnippetsModeViewModel`
  - browse 根层新增 `snippets-root-context-flyout`
  - `CreateEditorFooter`、`EditEditorFooter`、`ViewMoreHeader` 已拆分命名
- `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml.cs`
  - `TryGetViewModel(...)` 现在只接受 `SnippetsModeViewModel`
  - `copy` 动作改走 `SnippetsModeViewModel.CopyAsync(...)`
- `src/SkylarkTerminal/ViewModels/RightPanelModes/SnippetsModeViewModel.cs`
  - 新增 `BeginCreateCategoryCommand`
  - 新增 `BeginCreateFromClipboardCommand`
  - 新增 `ClearFilterCommand`
  - 新增 `CopyAsync(...)`
- `src/SkylarkTerminal/Services/IClipboardService.cs`
  - 新增 `GetTextAsync()`
- `src/SkylarkTerminal/Services/ClipboardService.cs`
  - 实现真实剪贴板读取
- `src/SkylarkTerminal/Services/Mock/MockClipboardService.cs`
  - 增加 `Text` 测试存根
- `src/SkylarkTerminal/Models/SnippetsText.cs`
  - 集中维护 snippets 相关中文文案和确认框消息
- `src/SkylarkTerminal/Services/AppDialogService.cs`
  - snippets 确认框改为使用 `SnippetsText`

## 关键方法与状态点

- `SnippetsModeView.TryGetViewModel(out SnippetsModeViewModel vm)`
  - 必须拒绝 `MainWindowViewModel` 假设
- `SnippetsModeView.HandleSnippetActionAsync(...)`
  - `copy` 必须转发到 `vm.CopyAsync(item)`
- `SnippetsModeViewModel.BeginCreate()`
  - 进入 `Create` 并重置 `Draft`
- `SnippetsModeViewModel.BeginCreateCategory()`
  - 进入 `Create`，并强制 `Draft.CreateNewCategory = true`
- `SnippetsModeViewModel.BeginCreateFromClipboardAsync()`
  - 从 `IClipboardService.GetTextAsync()` 预填 `Draft.Content`
- `SnippetsModeViewModel.CopyAsync(...)`
  - 将 snippet 内容写入 `IClipboardService`
- `SnippetsModeViewModel.ClearFilter()`
  - 将 `FilterText` 清空，并触发 `VisibleCategories` 重建
- `AppDialogService.ShowRunSnippetInAllTabsConfirmAsync(...)`
  - 必须使用 `SnippetsText` 中文标题和消息
- `AppDialogService.ShowDeleteSnippetConfirmAsync(...)`
  - 必须使用 `SnippetsText` 中文标题和消息

## 下一阶段建议测试点

- 模板结构测试
  - `RightSidebarHostView` 必须把 `ActiveRightMode` 显式传给 `SnippetsModeView`
  - `SnippetsModeView.axaml` 必须包含 `snippets-root-context-flyout`
  - `CreateEditorFooter`、`EditEditorFooter`、`ViewMoreHeader` 必须存在
- 状态机测试
  - `BeginCreateCategoryCommand` 后应进入 `Create` 且允许新建分类
  - `BeginCreateFromClipboardCommand` 后应进入 `Create` 且草稿内容来自剪贴板
  - `CancelEditCommand` 在 `Create / Edit / ViewMore` 后都应回到 `Browse`
- 文案测试
  - snippets 视图不应再出现 `Create Snippet / Edit Snippet / View More / Run in all tabs`
  - snippets 对话框不应再出现 `Delete snippet / Run in all tabs`
- 交互测试
  - browse 空白区右键能命中 root menu，而非落回通用 tools menu
  - 卡片右键仍然保留 item menu
  - create 态不应出现 `删除`
  - edit 态必须出现 `删除`
  - view-more 态必须出现 `返回`
- 集成测试
  - `Copy` 卡片动作应通过 `IClipboardService`，而不是直接依赖 `TopLevel.Clipboard`
  - `Run in all tabs` 仅统计已连接 SSH tabs，且确认文案为中文

## 重点边缘情况

- `DataContext` 再次被宿主模板改动时，按钮事件可能重新失效
- 剪贴板为空或不可用时，`BeginCreateFromClipboardCommand` 仍应稳定进入 `Create`，但草稿内容为空字符串
- `ClearFilterCommand` 在当前过滤词已为空时，不应抛异常
- `ViewMore` 退出仍然走 `CancelEditCommand`，后续若引入脏状态提示，需要额外补交互测试
- `Create` 与 `Edit` footer 已分离；如果未来有人合并模板，容易把 `删除` 再次带回 `Create`

## 已有验证命令

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsModeViewContextTemplateTests|FullyQualifiedName~SnippetsModeInteractionStateTests|FullyQualifiedName~SnippetsLocalizationTemplateTests|FullyQualifiedName~SnippetsModeContextMenuTemplateTests" -v minimal
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal
dotnet build SkylarkTerminal.slnx -v minimal
```

## 手工验证现状

- 当前命令行环境 `DISPLAY` 为空，未执行 GUI 手工 smoke
- 若后续在桌面环境补 smoke，建议验证：
  - `新建代码块` 的 `保存/取消`
  - 编辑已有 snippet 的 `保存/删除/取消`
  - `查看详情` 的 `返回`
  - browse 空白区 root menu
  - snippet 卡片 item menu
