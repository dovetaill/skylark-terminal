# Right Sidebar UI Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在保留现有右侧栏 `A1` 网格布局的前提下，完成图标化模式栏（`B2` 吸收 `B1` 语义优势）、子模式 ViewModel 架构（`C2`）、动态动作区（`E1`）、SFTP 导航服务化（`F2`）与 Xftp 风格单栏导航体验（`H1`）。

**Architecture:** 右侧栏继续挂在 `MainWindow` 的 `Grid.Column=5`，保留 `GridSplitter` 和阈值自动收起逻辑。UI 重构为 `RightSidebarHostView`（顶层 ModeRail + 中层 DynamicActionBar + 下层 TransitioningContentControl）。状态由 `MainWindowViewModel` 切换到 `ActiveRightMode`，各模式由 `IRightPanelModeViewModel` 子类负责，SFTP 导航由独立 `SftpNavigationService` 管理历史栈和路径行为。

**Tech Stack:** `.NET 10`, `Avalonia 11.3.12`, `FluentAvaloniaUI 2.5.0`, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`, `xUnit`

---

## 实施约束

1. 只做右侧栏重构相关改动，不触碰 SSH 会话核心链路。
2. 每个任务必须遵守 TDD：先写失败测试，再最小实现，再回归。
3. 每个任务独立 commit，确保可回滚。
4. 优先结构正确和可维护，不做临时拼接式实现。

---

### Task 1: 建立 `C2` 模式架构骨架（子 ViewModel）

**Files:**
- Create: `src/SkylarkTerminal/ViewModels/RightPanelModes/IRightPanelModeViewModel.cs`
- Create: `src/SkylarkTerminal/ViewModels/RightPanelModes/SnippetsModeViewModel.cs`
- Create: `src/SkylarkTerminal/ViewModels/RightPanelModes/HistoryModeViewModel.cs`
- Create: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Modify: `src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`
- Test: `tests/SkylarkTerminal.Tests/RightPanelModeArchitectureTests.cs`

**Step 1: 写失败测试**

```csharp
using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;
using SkylarkTerminal.ViewModels.RightPanelModes;

namespace SkylarkTerminal.Tests;

public class RightPanelModeArchitectureTests
{
    [Fact]
    public void MainWindow_ShouldExposeActiveRightMode_AndThreeModes()
    {
        var vm = new MainWindowViewModel();

        Assert.NotNull(vm.ActiveRightMode);
        Assert.Equal(3, vm.RightPanelModes.Count);
        Assert.Contains(vm.RightPanelModes, m => m.Kind == RightToolsViewKind.Snippets);
        Assert.Contains(vm.RightPanelModes, m => m.Kind == RightToolsViewKind.History);
        Assert.Contains(vm.RightPanelModes, m => m.Kind == RightToolsViewKind.Sftp);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightPanelModeArchitectureTests" -v minimal`  
Expected: FAIL，提示 `ActiveRightMode` / `RightPanelModes` 不存在。

**Step 3: 最小实现**

```csharp
// IRightPanelModeViewModel.cs
public interface IRightPanelModeViewModel
{
    RightToolsViewKind Kind { get; }
    string Title { get; }
    string Glyph { get; }
    object ContentNode { get; }
    IReadOnlyList<ModeActionDescriptor> Actions { get; }
}
```

```csharp
// MainWindowViewModel.cs (关键片段)
public ObservableCollection<IRightPanelModeViewModel> RightPanelModes { get; } = [];
public IRightPanelModeViewModel ActiveRightMode => ResolveActiveMode(SelectedRightToolsView);
```

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightPanelModeArchitectureTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/ViewModels/RightPanelModes \
        src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs \
        tests/SkylarkTerminal.Tests/RightPanelModeArchitectureTests.cs
git commit -m "refactor: introduce right panel sub-viewmodel architecture"
```

---

### Task 2: 落地 `E1` 动态动作区元数据模型

**Files:**
- Create: `src/SkylarkTerminal/Models/ModeActionDescriptor.cs`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SnippetsModeViewModel.cs`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/HistoryModeViewModel.cs`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Modify: `src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`
- Test: `tests/SkylarkTerminal.Tests/RightPanelModeActionsTests.cs`

**Step 1: 写失败测试**

```csharp
using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class RightPanelModeActionsTests
{
    [Fact]
    public void EachMode_ShouldExposeExpectedActions()
    {
        var vm = new MainWindowViewModel();

        vm.ShowSnippetsToolsCommand.Execute(null);
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "snippet.new");
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "snippet.search");

        vm.ShowHistoryToolsCommand.Execute(null);
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.search");
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.sort");

        vm.ShowSftpToolsCommand.Execute(null);
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "sftp.back");
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "sftp.up");
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightPanelModeActionsTests" -v minimal`  
Expected: FAIL，动作元数据尚未定义。

**Step 3: 最小实现**

```csharp
public sealed record ModeActionDescriptor(
    string Id,
    string Glyph,
    string Tooltip,
    IRelayCommand Command,
    bool IsToggle = false);
```

每个 mode 提供动作集合（`Snippet: New/Search/Sort/Layout`，`History: Search/Sort/Layout/Clear`，`Sftp: Back/Forward/Refresh/Up/AddressCommit`）。

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightPanelModeActionsTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Models/ModeActionDescriptor.cs \
        src/SkylarkTerminal/ViewModels/RightPanelModes \
        src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs \
        tests/SkylarkTerminal.Tests/RightPanelModeActionsTests.cs
git commit -m "feat: add metadata-driven dynamic action model for right panel modes"
```

---

### Task 3: 实现 `F2` SFTP 导航服务（历史栈 + 地址解析）

**Files:**
- Create: `src/SkylarkTerminal/Services/ISftpNavigationService.cs`
- Create: `src/SkylarkTerminal/Services/SftpNavigationService.cs`
- Modify: `src/SkylarkTerminal/App.axaml.cs`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Test: `tests/SkylarkTerminal.Tests/SftpNavigationServiceTests.cs`

**Step 1: 写失败测试**

```csharp
using SkylarkTerminal.Services;

namespace SkylarkTerminal.Tests;

public class SftpNavigationServiceTests
{
    [Fact]
    public void Navigate_Back_Forward_ShouldMaintainStacks()
    {
        var nav = new SftpNavigationService("/");
        nav.NavigateTo("/var");
        nav.NavigateTo("/var/log");

        Assert.True(nav.CanGoBack);
        Assert.Equal("/var", nav.GoBack());
        Assert.True(nav.CanGoForward);
        Assert.Equal("/var/log", nav.GoForward());
    }

    [Fact]
    public void GoUp_ShouldResolveParentPath()
    {
        var nav = new SftpNavigationService("/var/log");
        Assert.Equal("/var", nav.GoUp());
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpNavigationServiceTests" -v minimal`  
Expected: FAIL，`SftpNavigationService` 不存在。

**Step 3: 最小实现**

```csharp
public sealed class SftpNavigationService : ISftpNavigationService
{
    // 内部维护 CurrentPath、BackStack、ForwardStack
    // NavigateTo 会推进 back 栈并清空 forward 栈
    // GoBack/GoForward/GoUp 返回新路径并更新栈状态
}
```

并在 `App.axaml.cs` 注册 DI：

```csharp
services.AddSingleton<ISftpNavigationService>(_ => new SftpNavigationService("/"));
```

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpNavigationServiceTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Services/ISftpNavigationService.cs \
        src/SkylarkTerminal/Services/SftpNavigationService.cs \
        src/SkylarkTerminal/App.axaml.cs \
        src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs \
        tests/SkylarkTerminal.Tests/SftpNavigationServiceTests.cs
git commit -m "feat: introduce sftp navigation service with history stacks"
```

---

### Task 4: 新建 `RightSidebarHostView`（B2-Hybrid + D1）

**Files:**
- Create: `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
- Create: `src/SkylarkTerminal/Views/RightSidebarHostView.axaml.cs`
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml`
- Test: `tests/SkylarkTerminal.Tests/RightSidebarHostViewTemplateTests.cs`

**Step 1: 写失败测试**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightSidebarHostViewTemplateTests
{
    [Fact]
    public void HostView_ShouldContainModeRail_ActionBar_AndTransitionContent()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightSidebarHostView.axaml");
        Assert.True(File.Exists(path));
        var xaml = File.ReadAllText(path);

        Assert.Contains("ItemsSource=\"{Binding RightToolsModeItems}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding ActiveRightMode.Actions}\"", xaml);
        Assert.Contains("<TransitioningContentControl", xaml);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarHostViewTemplateTests" -v minimal`  
Expected: FAIL，目标视图未创建。

**Step 3: 最小实现**

`RightSidebarHostView` 结构：
1. Row0: `ModeRail` 图标按钮栏（不显示文本）。
2. Row1: `DynamicActionBar`（图标按钮 + 地址输入框槽位）。
3. Row2: `TransitioningContentControl` 根据 `ActiveRightMode.ContentNode` 切换内容。
4. 去掉旧 `Tools Panel (E)` 与 `Hide` 文字栏（`D1`）。

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarHostViewTemplateTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Views/RightSidebarHostView.axaml \
        src/SkylarkTerminal/Views/RightSidebarHostView.axaml.cs \
        src/SkylarkTerminal/Views/MainWindow.axaml \
        tests/SkylarkTerminal.Tests/RightSidebarHostViewTemplateTests.cs
git commit -m "refactor: replace inline right sidebar with host view composition"
```

---

### Task 5: 模式内容组件化（Snippets / History / SFTP）

**Files:**
- Create: `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml`
- Create: `src/SkylarkTerminal/Views/RightModes/SnippetsModeView.axaml.cs`
- Create: `src/SkylarkTerminal/Views/RightModes/HistoryModeView.axaml`
- Create: `src/SkylarkTerminal/Views/RightModes/HistoryModeView.axaml.cs`
- Create: `src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml`
- Create: `src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml.cs`
- Modify: `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
- Test: `tests/SkylarkTerminal.Tests/RightModeViewsBindingTests.cs`

**Step 1: 写失败测试**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightModeViewsBindingTests
{
    [Theory]
    [InlineData("SnippetsModeView.axaml", "SnippetItems")]
    [InlineData("HistoryModeView.axaml", "HistoryItems")]
    [InlineData("SftpModeView.axaml", "SftpItems")]
    public void ModeViews_ShouldBindExpectedCollections(string file, string bindingKey)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", file);
        Assert.True(File.Exists(path));
        Assert.Contains(bindingKey, File.ReadAllText(path));
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightModeViewsBindingTests" -v minimal`  
Expected: FAIL，文件缺失。

**Step 3: 最小实现**

1. 拆分三种模式内容视图，绑定沿用现有 `SnippetItems/HistoryItems/SftpItems`。
2. 在 `RightSidebarHostView.axaml` 的 `DataTemplates` 中映射到三个视图组件。

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightModeViewsBindingTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Views/RightModes \
        src/SkylarkTerminal/Views/RightSidebarHostView.axaml \
        tests/SkylarkTerminal.Tests/RightModeViewsBindingTests.cs
git commit -m "refactor: split right sidebar mode content into dedicated views"
```

---

### Task 6: 实现 `H1` SFTP 单栏 Explorer 顶部导航条

**Files:**
- Modify: `src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Test: `tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs`

**Step 1: 写失败测试**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SftpModeToolbarTemplateTests
{
    [Fact]
    public void SftpModeView_ShouldContainBackForwardAddressRefreshUp()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SftpModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("ToolTip.Tip=\"Back\"", xaml);
        Assert.Contains("ToolTip.Tip=\"Forward\"", xaml);
        Assert.Contains("Watermark=\"/", xaml);
        Assert.Contains("ToolTip.Tip=\"Refresh\"", xaml);
        Assert.Contains("ToolTip.Tip=\"Up\"", xaml);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeToolbarTemplateTests" -v minimal`  
Expected: FAIL，工具栏元素尚不存在。

**Step 3: 最小实现**

1. SFTP 顶部加入 5 类导航控件：`Back`、`Forward`、`AddressBox`、`Refresh`、`Up`。
2. `AddressBox` 回车触发 `AddressCommitCommand`，命令走 `SftpNavigationService.TryResolveAddressInput()`。
3. 下方文件列表继续使用 `SftpItems`。

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeToolbarTemplateTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml \
        src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs \
        tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs
git commit -m "feat: add xftp-like navigation toolbar in sftp mode"
```

---

### Task 7: 实现 `G1` 视觉统一与 B2-Hybrid 交互增强

**Files:**
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml`
- Modify: `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml.cs`
- Test: `tests/SkylarkTerminal.Tests/RightSidebarStyleConsistencyTests.cs`

**Step 1: 写失败测试**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightSidebarStyleConsistencyTests
{
    [Fact]
    public void MainWindow_ShouldDefineRightSidebarSharedStyleTokens()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "MainWindow.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("RightSidebarModeButton", xaml);
        Assert.Contains("RightSidebarActionButton", xaml);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarStyleConsistencyTests" -v minimal`  
Expected: FAIL，样式 token/class 尚未定义。

**Step 3: 最小实现**

1. 在 `MainWindow.axaml` 定义右侧样式类，复用左侧按钮视觉 token（`G1`）。
2. ModeRail 保持 icon-only，但保留单选语义和清晰 selected 态（`B2` 吸收 `B1` 优势）。
3. 在 `MainWindow.axaml.cs` 增加快捷键 `Ctrl+1/2/3` 切换模式。

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarStyleConsistencyTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Views/MainWindow.axaml \
        src/SkylarkTerminal/Views/RightSidebarHostView.axaml \
        src/SkylarkTerminal/Views/MainWindow.axaml.cs \
        tests/SkylarkTerminal.Tests/RightSidebarStyleConsistencyTests.cs
git commit -m "style: unify right sidebar visual language with left rail and add mode shortcuts"
```

---

### Task 8: 全量回归与文档同步

**Files:**
- Modify: `README.md`
- Modify: `docs/plans/2026-03-08-right-sidebar-ui-redesign-design.md`（如需补充“已实施状态”）

**Step 1: 运行右侧栏相关测试集**

Run:

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj \
  --filter "FullyQualifiedName~RightPanel|FullyQualifiedName~RightSidebar|FullyQualifiedName~SftpMode" -v minimal
```

Expected: 本次新增/修改用例全部 PASS。

**Step 2: 运行全量测试**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal`  
Expected: PASS；若有历史失败，需明确标注 pre-existing。

**Step 3: 运行构建验证**

Run: `dotnet build SkylarkTerminal.slnx -v minimal`  
Expected: `Build succeeded.`

**Step 4: 同步 README**

更新“右侧工具区”章节：
1. 图标化 ModeRail（Snippets/History/SFTP）
2. 动态动作区
3. SFTP 单栏导航（Back/Forward/Address/Refresh/Up）
4. 视觉统一策略与快捷键

**Step 5: 提交**

```bash
git add README.md docs/plans/2026-03-08-right-sidebar-ui-redesign-design.md
git commit -m "docs: sync right sidebar redesign architecture and behavior"
```

---

## 验收门槛（Done Definition）

1. 右侧栏顶部不再出现 `Tools Panel (E)` 与 `Hide` 文本按钮。
2. 最顶层为图标模式栏，且具备选中高亮、键盘导航、快捷键切换。
3. 模式按钮栏下方存在动态动作区，不同模式动作不同。
4. SFTP 模式具备 `Back/Forward/Address/Refresh/Up` 导航闭环。
5. 右侧栏仍可拖拽调宽，阈值收起行为不回归。
6. 深浅主题下右侧栏与左侧栏风格一致。
7. 新增测试与现有相关测试通过。

## 风险与回滚

1. 若 `C2` 引发绑定复杂度上升，先保留子 VM 接口，临时收敛命令来源到 `MainWindowViewModel`。
2. 若 `SftpNavigationService` 出现路径状态错乱，回退到仅 `Refresh + Address`，保留服务接口不删。
3. 若样式冲突影响左侧栏，通过独立 class 前缀限制 selector 作用域。

