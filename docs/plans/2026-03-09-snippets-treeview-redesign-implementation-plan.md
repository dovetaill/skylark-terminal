# Snippets TreeView Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将 `Snippets` browse 视图改为真正的树状列表，移除卡片式布局与 tags 编辑流，同时补齐分类/代码块删除语义和树节点上下文菜单。

**Architecture:** 保留当前 `RightSidebarHostView + SnippetsModeViewModel + ISnippetRepository` 架构，不改 snippets 本地 JSON 格式。browse 层直接使用 `TreeView + TreeDataTemplate` 消费现有 `SnippetCategory -> Items` 层级；分类删除、editable 分类输入和 tags 兼容策略全部收敛在 `SnippetsModeViewModel` 与 `AppDialogService`。

**Tech Stack:** `.NET 10`, `Avalonia 11.3.12`, `FluentAvaloniaUI 2.5.0`, `CommunityToolkit.Mvvm 8.4.0`, `xUnit`

---

## 开发前约束

1. 在独立 worktree 中执行，不要直接在主工作区改代码。
2. 本轮只修改 `Snippets` 相关 View / ViewModel / 对话框 / 样式 / 测试 / README。
3. 严格按 TDD 执行：每个任务先写失败测试，再做最小实现，再跑验证。
4. `SnippetItem.Tags` 不能被这轮 UI 改动意外清空；编辑已有 snippet 时必须保留旧 tags。
5. 每个任务单独 commit，提交边界与任务边界一致。

## Task Map

1. 用模板测试锁定 `TreeView` browse 结构，移除卡片式布局
2. 重做 `SnippetsModeView` 的树节点模板、root/category/item 右键菜单与双击粘贴行为
3. 扩展 `SnippetsModeViewModel` 与 `AppDialogService`，补分类删除、级联确认和树过滤逻辑
4. 收敛 create/edit 表单，去掉 tags 并切到 editable 分类输入
5. 同步 README，补 TDD 交接文档并执行全量验证

---

### Task 1: 锁定 TreeView browse 模板并移除卡片结构

**Files:**
- Modify: `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml`
- Create: `tests/SkylarkTerminal.Tests/SnippetsTreeViewTemplateTests.cs`
- Modify: `tests/SkylarkTerminal.Tests/RightModeViewsBindingTests.cs`

**Step 1: Write the failing test**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SnippetsTreeViewTemplateTests
{
    [Fact]
    public void SnippetsBrowse_ShouldUseTreeView_AndDropCardLayout()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<TreeView", xaml, StringComparison.Ordinal);
        Assert.Contains("TreeDataTemplate DataType=\"models:SnippetCategory\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RightSidebarSnippetCard", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RightSidebarSnippetActionBand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Items.Count", xaml, StringComparison.Ordinal);
    }
}
```

并把 `RightModeViewsBindingTests.cs` 中 snippets 的断言从：

```csharp
[InlineData("SnippetsModeView.axaml", "ItemsSource=\"{Binding VisibleCategories}\"")]
```

改成：

```csharp
[InlineData("SnippetsModeView.axaml", "<TreeView ItemsSource=\"{Binding VisibleCategories}\"")]
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsTreeViewTemplateTests|FullyQualifiedName~RightModeViewsBindingTests" -v minimal`  
Expected: FAIL，当前 browse 还是 `ItemsControl + card`。

**Step 3: Write minimal implementation**

- 把 browse 根层改成 `TreeView ItemsSource="{Binding VisibleCategories}"`。
- `SnippetCategory` 使用 `TreeDataTemplate`，子项取自 `Items`。
- browse 结构中移除：
  - `RightSidebarSnippetCard`
  - `RightSidebarSnippetActionBand`
  - 分类数量 chip
  - 预览文本和 tag 区块

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsTreeViewTemplateTests|FullyQualifiedName~RightModeViewsBindingTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml \
        tests/SkylarkTerminal.Tests/SnippetsTreeViewTemplateTests.cs \
        tests/SkylarkTerminal.Tests/RightModeViewsBindingTests.cs
git commit -m "refactor: switch snippets browse to tree view"
```

---

### Task 2: 补树节点模板、右键菜单和双击粘贴回归

**Files:**
- Modify: `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml`
- Modify: `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml.cs`
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml`
- Modify: `src/SkylarkTerminal/Models/SnippetsText.cs`
- Create: `tests/SkylarkTerminal.Tests/SnippetsTreeContextMenuTemplateTests.cs`
- Modify: `tests/SkylarkTerminal.Tests/RightSidebarStyleConsistencyTests.cs`

**Step 1: Write the failing test**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SnippetsTreeContextMenuTemplateTests
{
    [Fact]
    public void SnippetsTree_ShouldDefineRootCategoryAndItemMenus()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("snippets-root-context-flyout", xaml, StringComparison.Ordinal);
        Assert.Contains("snippets-category-context-flyout", xaml, StringComparison.Ordinal);
        Assert.Contains("snippets-item-context-flyout", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"delete-snippet\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"delete-category\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SnippetsTree_CodeBehind_ShouldKeepDoubleTapAsPaste()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml.cs");
        var code = File.ReadAllText(path);

        Assert.Contains("await vm.PasteAsync(item);", code, StringComparison.Ordinal);
    }
}
```

并在 `RightSidebarStyleConsistencyTests.cs` 追加：

```csharp
Assert.Contains("snippets-category-context-flyout", xaml, StringComparison.Ordinal);
Assert.Contains("snippets-item-context-flyout", xaml, StringComparison.Ordinal);
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsTreeContextMenuTemplateTests|FullyQualifiedName~RightSidebarStyleConsistencyTests" -v minimal`  
Expected: FAIL，当前只有 root/card flyout，没有分类/树节点 flyout。

**Step 3: Write minimal implementation**

- 在 `TreeView` 上保留 root `ContextFlyout`。
- 为分类节点模板增加 `snippets-category-context-flyout`。
- 为代码块节点模板增加 `snippets-item-context-flyout`。
- 节点采用：
  - 分类：文件夹图标 + 分类名
  - 代码块：代码图标 + 代码块名称
- `SnippetsModeView.axaml.cs` 中新增对 `delete-category` / `delete-snippet` tag 的分发，但先调用 ViewModel 公开方法，不在 View 层写删除逻辑。
- `MainWindow.axaml` 为三个 flyout 都补宽度样式。

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsTreeContextMenuTemplateTests|FullyQualifiedName~RightSidebarStyleConsistencyTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml \
        src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml.cs \
        src/SkylarkTerminal/Views/MainWindow.axaml \
        src/SkylarkTerminal/Models/SnippetsText.cs \
        tests/SkylarkTerminal.Tests/SnippetsTreeContextMenuTemplateTests.cs \
        tests/SkylarkTerminal.Tests/RightSidebarStyleConsistencyTests.cs
git commit -m "feat: add snippets tree node menus"
```

---

### Task 3: 扩展 ViewModel 与对话框，补分类删除和树过滤语义

**Files:**
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SnippetsModeViewModel.cs`
- Modify: `src/SkylarkTerminal/Services/IAppDialogService.cs`
- Modify: `src/SkylarkTerminal/Services/AppDialogService.cs`
- Modify: `src/SkylarkTerminal/Services/Mock/MockAppDialogService.cs`
- Modify: `src/SkylarkTerminal/Models/SnippetsText.cs`
- Create: `tests/SkylarkTerminal.Tests/SnippetsCategoryDeletionTests.cs`

**Step 1: Write the failing test**

```csharp
using SkylarkTerminal.Models;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;
using System.Collections.ObjectModel;

namespace SkylarkTerminal.Tests;

public class SnippetsCategoryDeletionTests
{
    [Fact]
    public async Task DeleteCategoryAsync_ShouldDeleteNonEmptyCategory_AfterCascadeConfirm()
    {
        var repo = new MockSnippetRepository(
        [
            new SnippetCategory
            {
                Name = "Ops",
                Items = new ObservableCollection<SnippetItem>
                {
                    new() { Title = "Restart", Content = "systemctl restart app" }
                }
            }
        ]);
        var dialog = new MockAppDialogService
        {
            DeleteSnippetCategoryConfirmResult = true
        };
        var vm = new SnippetsModeViewModel(
            repo,
            new MockClipboardService(),
            new MockTerminalCommandBridge(),
            dialog,
            static () => null,
            static () => []);

        await vm.LoadAsync();
        await vm.DeleteCategoryAsync(vm.Categories[0]);

        Assert.Empty(vm.Categories);
        Assert.True(dialog.LastDeleteCategoryIncludesChildren);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsCategoryDeletionTests" -v minimal`  
Expected: FAIL，当前没有分类删除命令与确认接口。

**Step 3: Write minimal implementation**

- `IAppDialogService` 增加：
  - `Task<bool> ShowDeleteSnippetCategoryConfirmAsync(string categoryName, int snippetCount);`
- `AppDialogService` 根据 `snippetCount` 生成两种文案：
  - 空分类：普通确认
  - 非空分类：明确说明会同时删除所有代码块
- `MockAppDialogService` 增加对应结果与观测字段。
- `SnippetsModeViewModel` 增加：
  - `DeleteCategoryAsync(SnippetCategory category, CancellationToken ct = default)`
  - `DeleteSnippetAsync(SnippetItem item, CancellationToken ct = default)` 或等价方法
- `RebuildVisibleCategories()` 搜索只匹配：
  - 分类名
  - 代码块标题
  - 代码块内容
  - 不再匹配 tags

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsCategoryDeletionTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/ViewModels/RightPanelModes/SnippetsModeViewModel.cs \
        src/SkylarkTerminal/Services/IAppDialogService.cs \
        src/SkylarkTerminal/Services/AppDialogService.cs \
        src/SkylarkTerminal/Services/Mock/MockAppDialogService.cs \
        src/SkylarkTerminal/Models/SnippetsText.cs \
        tests/SkylarkTerminal.Tests/SnippetsCategoryDeletionTests.cs
git commit -m "feat: support snippets category deletion"
```

---

### Task 4: 收敛 create/edit 表单，去掉 tags 并切到 editable 分类输入

**Files:**
- Modify: `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SnippetsModeViewModel.cs`
- Modify: `src/SkylarkTerminal/Models/SnippetsText.cs`
- Create: `tests/SkylarkTerminal.Tests/SnippetsEditorFormTemplateTests.cs`

**Step 1: Write the failing test**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SnippetsEditorFormTemplateTests
{
    [Fact]
    public void SnippetsEditor_ShouldUseEditableCategoryPicker_AndDropTagsField()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SnippetsModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<ComboBox", xaml, StringComparison.Ordinal);
        Assert.Contains("IsEditable=\"True\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft.TagsText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TagsWatermark", xaml, StringComparison.Ordinal);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsEditorFormTemplateTests" -v minimal`  
Expected: FAIL，当前表单还是 `TextBox + Tags`。

**Step 3: Write minimal implementation**

- `SnippetsModeView.axaml`
  - 把分类输入换成 editable `ComboBox`
  - 去掉 tags 输入
  - 移除表单中的“新建分类”按钮
- `SnippetsModeViewModel`
  - 提供已有分类名称列表，例如 `CategoryOptions`
  - 保存时根据输入名称：
    - 命中已有分类则复用
    - 未命中则新建分类
  - 编辑已有 snippet 时保留原有 `item.Tags`
  - 新建 snippet 时写入空 tags

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsEditorFormTemplateTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml \
        src/SkylarkTerminal/ViewModels/RightPanelModes/SnippetsModeViewModel.cs \
        src/SkylarkTerminal/Models/SnippetsText.cs \
        tests/SkylarkTerminal.Tests/SnippetsEditorFormTemplateTests.cs
git commit -m "refactor: simplify snippets editor form"
```

---

### Task 5: 同步 README、补设计交接并执行全量验证

**Files:**
- Modify: `README.md`
- Create: `docs/plans/2026-03-09-snippets-treeview-redesign-tdd-spec.md`

**Step 1: Run targeted regression before docs**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SnippetsTreeViewTemplateTests|FullyQualifiedName~SnippetsTreeContextMenuTemplateTests|FullyQualifiedName~SnippetsCategoryDeletionTests|FullyQualifiedName~SnippetsEditorFormTemplateTests" -v minimal`  
Expected: PASS。

**Step 2: Update docs**

在 `README.md` 补充：

- snippets browse 改为树状分类视图
- 双击代码块为直接粘贴
- 分类/代码块支持删除
- create/edit 去掉 tags，分类支持选择已有或输入新名

新增 `docs/plans/2026-03-09-snippets-treeview-redesign-tdd-spec.md`，记录：

- TreeView 模板结构
- 删除分类的级联确认逻辑
- tags 兼容策略
- editable 分类输入行为
- 手工 smoke 步骤

**Step 3: Run full verification**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal`  
Expected: PASS。

Run: `dotnet build SkylarkTerminal.slnx -v minimal`  
Expected: PASS。

**Step 4: Commit**

```bash
git add README.md \
        docs/plans/2026-03-09-snippets-treeview-redesign-tdd-spec.md
git commit -m "docs: describe snippets treeview flow"
```

**Step 5: Final check**

Run: `git status --short`  
Expected: 工作区只剩本轮预期变更；无意外文件残留。

---

## 最终验收标准

1. `Snippets` browse 已切到真正的 `TreeView`。
2. 分类节点和代码块节点都采用“图标 + 名称”的轻量列表结构。
3. browse 不再显示数量 chip、预览文本、tags 区块和底部快捷动作带。
4. 分类与代码块右键菜单都补齐删除能力。
5. 删除非空分类时会明确提示将同时删除该分类下的所有代码块。
6. `Create / Edit` 不再出现 tags 输入，分类字段支持选择已有分类或输入新名字。
7. 双击代码块节点仍然是直接粘贴到当前终端。
8. `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal` 通过。
9. `dotnet build SkylarkTerminal.slnx -v minimal` 通过。
