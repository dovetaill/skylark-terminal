# RightSidebar Layout Bugfix2 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在不触碰 `SshTerminalPane` 与 `RowStripedTerminalView` 主链路的前提下，完成 `RightSidebar` 第二轮根因修复：为 mode rail 建立语义化图标与中文提示体系，把 `SFTP` 加载与状态管理收回模式自身，移除 `CommandBar` overflow 对地址输入框的影响，并把 `SFTP` 内容区重绘为稳定、完整、现代的远程文件面板。

**Architecture:** 保留现有 `RightSidebarHostView + IRightPanelModeViewModel + header slot` 总体架构，不回退到 `MainWindow` 内联实现。`Snippets / History` 继续走轻量 action strip；`SFTP` 改成 `Grid`-based toolbar，命令 metadata 和中文文案都收口到 ViewModel/模型层，模式切换后由 `SftpModeViewModel` 自己维护 `Idle / Loading / Loaded / Empty / Error` 状态机与数据集合。

**Tech Stack:** `.NET 10`, `Avalonia 11.3.12`, `FluentAvaloniaUI 2.5.0`, `CommunityToolkit.Mvvm 8.4.0`, `Microsoft.Extensions.DependencyInjection 10.0.0`, `xUnit`

---

**Design Input:** `docs/plans/2026-03-09-right-sidebar-layout-bugfix2-design.md`

## 开发前约束

1. 当前工作树存在与本任务无关的脏改动，执行前先确认不要误清理或覆盖。
2. 仅修改右栏相关 View / ViewModel / 样式 / 文案 / 测试 / 文档；不修改 `src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork`。
3. 每个任务必须按 TDD 顺序执行：先失败测试，再最小实现，再回归，再提交。
4. 每个任务单独 commit，保证结构性改动可独立回滚。
5. 右栏任何弹层都禁止承载 `TextBox` 或其他复杂输入控件；输入区只能存在于固定布局区域。

## Task Map

1. 建立 mode rail 语义元数据与中文 tooltip 契约
2. 将 `SFTP` 加载职责下沉到 `SftpModeViewModel` 并建立完整状态机
3. 收口右栏命令 metadata 与中文文案来源
4. 用自定义 `Grid` toolbar 替换 `SFTP CommandBar`，显式定义 `More` flyout 边界
5. 重绘 `SFTP` 内容区为状态感知的双层文件列表
6. 同步文档并完成回归验证

---

### Task 1: 建立 Mode Rail 语义元数据与中文 Tooltip 契约

**Files:**
- Create: `src/SkylarkTerminal/Models/RightModeIconKey.cs`
- Create: `src/SkylarkTerminal/Models/RightModeIconCatalog.cs`
- Modify: `src/SkylarkTerminal/Models/RightToolsModeItem.cs`
- Modify: `src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
- Modify: `tests/SkylarkTerminal.Tests/RightToolsModeSwitchTests.cs`
- Create: `tests/SkylarkTerminal.Tests/RightSidebarModeMetadataTests.cs`

**Step 1: Write the failing test**

```csharp
using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class RightSidebarModeMetadataTests
{
    [Fact]
    public void RightToolsModeItems_ShouldExposeChineseTooltip_AndSemanticIconKey()
    {
        var vm = new MainWindowViewModel();

        Assert.Collection(
            vm.RightToolsModeItems,
            item =>
            {
                Assert.Equal(RightToolsViewKind.Snippets, item.Kind);
                Assert.Equal("代码块", item.TooltipZh);
                Assert.Equal(RightModeIconKey.Snippets, item.IconKey);
            },
            item =>
            {
                Assert.Equal(RightToolsViewKind.History, item.Kind);
                Assert.Equal("历史记录", item.TooltipZh);
                Assert.Equal(RightModeIconKey.History, item.IconKey);
            },
            item =>
            {
                Assert.Equal(RightToolsViewKind.Sftp, item.Kind);
                Assert.Equal("SFTP 文件", item.TooltipZh);
                Assert.Equal(RightModeIconKey.RemoteFiles, item.IconKey);
            });
    }
}
```

并在 `RightToolsModeSwitchTests.cs` 新增 host template 约束：

```csharp
[Fact]
public void RightSidebarHostView_ShouldBindModeTooltip()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightSidebarHostView.axaml");
    var xaml = File.ReadAllText(path);

    Assert.Contains("ToolTip.Tip=\"{Binding TooltipZh}\"", xaml, StringComparison.Ordinal);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarModeMetadataTests|FullyQualifiedName~RightToolsModeSwitchTests" -v minimal`  
Expected: FAIL，`TooltipZh` / `IconKey` / `RightModeIconKey` 尚不存在。

**Step 3: Write minimal implementation**

```csharp
namespace SkylarkTerminal.Models;

public enum RightModeIconKey
{
    Snippets,
    History,
    RemoteFiles
}
```

```csharp
namespace SkylarkTerminal.Models;

public static class RightModeIconCatalog
{
    public static string Resolve(RightModeIconKey key) => key switch
    {
        RightModeIconKey.Snippets => "\uE8D2",
        RightModeIconKey.History => "\uE81C",
        RightModeIconKey.RemoteFiles => "\uF0E8",
        _ => "\uE10C",
    };
}
```

```csharp
public sealed record RightToolsModeItem(
    RightToolsViewKind Kind,
    string TitleZh,
    string TooltipZh,
    RightModeIconKey IconKey)
{
    public string Glyph => RightModeIconCatalog.Resolve(IconKey);
}
```

`MainWindowViewModel` 中把 mode rail 初始化改为中文元数据；`RightSidebarHostView.axaml` 中给 mode item 增加 `ToolTip.Tip="{Binding TooltipZh}"`。

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarModeMetadataTests|FullyQualifiedName~RightToolsModeSwitchTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Models/RightModeIconKey.cs \
        src/SkylarkTerminal/Models/RightModeIconCatalog.cs \
        src/SkylarkTerminal/Models/RightToolsModeItem.cs \
        src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs \
        src/SkylarkTerminal/Views/RightSidebarHostView.axaml \
        tests/SkylarkTerminal.Tests/RightToolsModeSwitchTests.cs \
        tests/SkylarkTerminal.Tests/RightSidebarModeMetadataTests.cs
git commit -m "refactor: add semantic metadata for right sidebar mode rail"
```

---

### Task 2: 下沉 `SFTP` 加载职责并建立完整状态机

**Files:**
- Create: `src/SkylarkTerminal/Models/SftpPanelLoadState.cs`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Modify: `src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`
- Modify: `tests/SkylarkTerminal.Tests/SftpAddressInteractionStateTests.cs`
- Create: `tests/SkylarkTerminal.Tests/SftpModeActivationStateTests.cs`

**Step 1: Write the failing test**

```csharp
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels.RightPanelModes;

namespace SkylarkTerminal.Tests;

public class SftpModeActivationStateTests
{
    [Fact]
    public async Task ActivateAsync_ShouldLoadItems_AndTransitionToLoaded()
    {
        var vm = new SftpModeViewModel(
            new MockSftpService(),
            new SftpNavigationService("/"),
            actions: []);

        Assert.Equal(SftpPanelLoadState.Idle, vm.LoadState);

        await vm.ActivateAsync("mock-conn-01");

        Assert.Equal(SftpPanelLoadState.Loaded, vm.LoadState);
        Assert.NotEmpty(vm.Items);
        Assert.Null(vm.ErrorMessage);
    }
}
```

并扩展地址栏测试：

```csharp
[Fact]
public async Task CommitAddressCommand_ShouldKeepStateOwnedBySftpMode()
{
    var vm = new SftpModeViewModel(new MockSftpService(), new SftpNavigationService("/"), []);

    await vm.ActivateAsync("mock-conn-01");
    vm.ExpandAddressEditorCommand.Execute(null);
    vm.AddressInput = "/logs";

    await vm.CommitAddressAsync();

    Assert.False(vm.IsAddressEditorExpanded);
    Assert.Contains(vm.Items, item => item.FullPath.Contains("/logs", StringComparison.Ordinal));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeActivationStateTests|FullyQualifiedName~SftpAddressInteractionStateTests" -v minimal`  
Expected: FAIL，`LoadState` / `Items` / `ActivateAsync` / `CommitAddressAsync` 尚不存在。

**Step 3: Write minimal implementation**

```csharp
namespace SkylarkTerminal.Models;

public enum SftpPanelLoadState
{
    Idle,
    Loading,
    Loaded,
    Empty,
    Error
}
```

```csharp
public sealed partial class SftpModeViewModel : ObservableObject, IRightPanelModeViewModel
{
    private readonly ISftpService _sftpService;
    private string? _activeConnectionId;

    public ObservableCollection<RemoteFileNode> Items { get; } = [];

    [ObservableProperty]
    private SftpPanelLoadState loadState = SftpPanelLoadState.Idle;

    [ObservableProperty]
    private string? errorMessage;

    public async Task ActivateAsync(string connectionId)
    {
        _activeConnectionId = connectionId;
        await LoadDirectoryAsync(_navigationService.CurrentPath);
    }

    public async Task CommitAddressAsync()
    {
        CommitAddress(AddressInput);
        await LoadDirectoryAsync(CurrentPath);
        IsAddressEditorExpanded = false;
    }

    private async Task LoadDirectoryAsync(string path) { ... }
}
```

`MainWindowViewModel` 在切换到 `SFTP` 模式时，不再手动操作 `SftpItems`，而是调用 `await sftpMode.ActivateAsync("mock-conn-01")`。保留 connection id 的来源逻辑在宿主，保留数据与状态的所有权在 `SftpModeViewModel`。

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeActivationStateTests|FullyQualifiedName~SftpAddressInteractionStateTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Models/SftpPanelLoadState.cs \
        src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs \
        src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs \
        tests/SkylarkTerminal.Tests/SftpAddressInteractionStateTests.cs \
        tests/SkylarkTerminal.Tests/SftpModeActivationStateTests.cs
git commit -m "feat: move sftp loading state into mode view model"
```

---

### Task 3: 收口右栏命令 Metadata 与中文文案来源

**Files:**
- Create: `src/SkylarkTerminal/Models/SftpToolbarActionDescriptor.cs`
- Modify: `src/SkylarkTerminal/Models/ModeActionDescriptor.cs`
- Modify: `src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Modify: `src/SkylarkTerminal/Views/RightHeaders/ActionStripHeaderView.axaml`
- Create: `tests/SkylarkTerminal.Tests/RightSidebarLocalizationMetadataTests.cs`

**Step 1: Write the failing test**

```csharp
using SkylarkTerminal.ViewModels;
using SkylarkTerminal.ViewModels.RightPanelModes;

namespace SkylarkTerminal.Tests;

public class RightSidebarLocalizationMetadataTests
{
    [Fact]
    public async Task RightSidebarCommands_ShouldExposeChineseTooltips()
    {
        var vm = new MainWindowViewModel();

        vm.ShowHistoryToolsCommand.Execute(null);
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.search" && a.TooltipZh == "搜索历史");
        Assert.Contains(vm.ActiveRightMode.Actions, a => a.Id == "history.clear" && a.TooltipZh == "清空历史");

        await vm.ShowSftpToolsCommand.ExecuteAsync(null);
        var sftp = Assert.IsType<SftpModeViewModel>(vm.ActiveRightMode);
        Assert.Contains(sftp.LeadingCommands, a => a.Id == "sftp.back" && a.TooltipZh == "后退");
        Assert.Contains(sftp.TrailingCommands, a => a.Id == "sftp.refresh" && a.TooltipZh == "刷新");
        Assert.Contains(sftp.MoreCommands, a => a.Id == "sftp.copy-path" && a.LabelZh == "复制当前路径");
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarLocalizationMetadataTests|FullyQualifiedName~RightPanelModeActionsTests" -v minimal`  
Expected: FAIL，`TooltipZh` / `LabelZh` / `LeadingCommands` / `MoreCommands` 尚不存在。

**Step 3: Write minimal implementation**

```csharp
public sealed record ModeActionDescriptor(
    string Id,
    string Glyph,
    string LabelZh,
    string TooltipZh,
    IRelayCommand Command,
    bool IsToggle = false);
```

```csharp
public sealed record SftpToolbarActionDescriptor(
    string Id,
    string Glyph,
    string LabelZh,
    string TooltipZh,
    IRelayCommand Command);
```

`MainWindowViewModel` 中将 `Snippets / History` 动作 builder 改为中文 metadata；`SftpModeViewModel` 新增三组公开集合：

```csharp
public IReadOnlyList<SftpToolbarActionDescriptor> LeadingCommands { get; }
public IReadOnlyList<SftpToolbarActionDescriptor> TrailingCommands { get; }
public IReadOnlyList<SftpToolbarActionDescriptor> MoreCommands { get; }
```

`ActionStripHeaderView.axaml` 中 tooltip 绑定改为 `TooltipZh`。

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightSidebarLocalizationMetadataTests|FullyQualifiedName~RightPanelModeActionsTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Models/SftpToolbarActionDescriptor.cs \
        src/SkylarkTerminal/Models/ModeActionDescriptor.cs \
        src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs \
        src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs \
        src/SkylarkTerminal/Views/RightHeaders/ActionStripHeaderView.axaml \
        tests/SkylarkTerminal.Tests/RightSidebarLocalizationMetadataTests.cs
git commit -m "refactor: centralize right sidebar command metadata and copy"
```

---

### Task 4: 用自定义 Grid Toolbar 替换 `SFTP CommandBar`

**Files:**
- Modify: `src/SkylarkTerminal/Models/RightPanelHeaderNode.cs`
- Modify: `src/SkylarkTerminal/Views/RightSidebarHostView.axaml`
- Create: `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml`
- Create: `src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml.cs`
- Delete: `src/SkylarkTerminal/Views/RightHeaders/SftpCommandBarHeaderView.axaml`
- Delete: `src/SkylarkTerminal/Views/RightHeaders/SftpCommandBarHeaderView.axaml.cs`
- Modify: `src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs`
- Modify: `tests/SkylarkTerminal.Tests/RightSidebarHostViewTemplateTests.cs`
- Modify: `tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs`

**Step 1: Write the failing test**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SftpModeToolbarTemplateTests
{
    [Fact]
    public void SftpHeader_ShouldUseGridToolbar_And_NotDependOnCommandBar()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightHeaders", "SftpToolbarHeaderView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<Grid", xaml, StringComparison.Ordinal);
        Assert.Contains("Button.Flyout", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding TooltipZh}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ui:CommandBar", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandBarElementContainer", xaml, StringComparison.Ordinal);
    }
}
```

并更新 `RightSidebarHostViewTemplateTests.cs`：

```csharp
Assert.Contains("<rightHeaders:SftpToolbarHeaderView/>", xaml, StringComparison.Ordinal);
Assert.DoesNotContain("<rightHeaders:SftpCommandBarHeaderView/>", xaml, StringComparison.Ordinal);
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeToolbarTemplateTests|FullyQualifiedName~RightSidebarHostViewTemplateTests" -v minimal`  
Expected: FAIL，仓库仍然使用 `SftpCommandBarHeaderView` 和 `CommandBar`。

**Step 3: Write minimal implementation**

```csharp
public sealed record SftpToolbarRightPanelHeader : RightPanelHeaderNode;
```

```xml
<UserControl ... x:Class="SkylarkTerminal.Views.RightHeaders.SftpToolbarHeaderView">
    <Grid ColumnDefinitions="Auto,Auto,*,Auto,Auto,Auto"
          ColumnSpacing="6">
        <!-- LeadingCommands -->
        <!-- Address chip / editor -->
        <!-- TrailingCommands -->
        <!-- More button with pure-action flyout -->
    </Grid>
</UserControl>
```

`SftpToolbarHeaderView.axaml.cs` 延续现有地址 chip/editor 焦点控制，但逻辑只处理固定 header 区域中的输入框；`More` flyout 中禁止放 `TextBox`。`RightSidebarHostView.axaml` 的 data template 改为渲染 `SftpToolbarHeaderView`。

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeToolbarTemplateTests|FullyQualifiedName~RightSidebarHostViewTemplateTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Models/RightPanelHeaderNode.cs \
        src/SkylarkTerminal/Views/RightSidebarHostView.axaml \
        src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml \
        src/SkylarkTerminal/Views/RightHeaders/SftpToolbarHeaderView.axaml.cs \
        src/SkylarkTerminal/ViewModels/RightPanelModes/SftpModeViewModel.cs \
        tests/SkylarkTerminal.Tests/RightSidebarHostViewTemplateTests.cs \
        tests/SkylarkTerminal.Tests/SftpModeToolbarTemplateTests.cs
git rm src/SkylarkTerminal/Views/RightHeaders/SftpCommandBarHeaderView.axaml \
       src/SkylarkTerminal/Views/RightHeaders/SftpCommandBarHeaderView.axaml.cs
git commit -m "refactor: replace sftp command bar with fixed grid toolbar"
```

---

### Task 5: 重绘 `SFTP` 内容区为状态感知的双层文件列表

**Files:**
- Modify: `src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml`
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml`
- Modify: `tests/SkylarkTerminal.Tests/RightModeViewsBindingTests.cs`
- Modify: `tests/SkylarkTerminal.Tests/RightSidebarStyleConsistencyTests.cs`
- Create: `tests/SkylarkTerminal.Tests/SftpModeStateTemplateTests.cs`

**Step 1: Write the failing test**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class SftpModeStateTemplateTests
{
    [Fact]
    public void SftpModeView_ShouldDefineStateSections_And_DualRowItemTemplate()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(root, "src", "SkylarkTerminal", "Views", "RightModes", "SftpModeView.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("LoadState", xaml, StringComparison.Ordinal);
        Assert.Contains("Idle", xaml, StringComparison.Ordinal);
        Assert.Contains("Loading", xaml, StringComparison.Ordinal);
        Assert.Contains("Empty", xaml, StringComparison.Ordinal);
        Assert.Contains("Error", xaml, StringComparison.Ordinal);
        Assert.Contains("Items", xaml, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"Auto,Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding FullPath}\"", xaml, StringComparison.Ordinal);
    }
}
```

并更新样式测试：

```csharp
Assert.Contains("RightSidebarSftpRowHoverBackgroundBrush", xaml, StringComparison.Ordinal);
Assert.DoesNotContain("Selector=\"ui|CommandBar.RightSidebarSftpCommandBar\"", xaml, StringComparison.Ordinal);
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeStateTemplateTests|FullyQualifiedName~RightModeViewsBindingTests|FullyQualifiedName~RightSidebarStyleConsistencyTests" -v minimal`  
Expected: FAIL，当前 `SftpModeView.axaml` 仍是简单 `ListBox`，且样式仍带 `RightSidebarSftpCommandBar`。

**Step 3: Write minimal implementation**

```xml
<UserControl ...>
    <Grid RowDefinitions="*">
        <!-- Idle / Loading / Empty / Error presenters -->
        <ListBox IsVisible="{Binding DataContext.ActiveRightMode.IsLoaded, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}"
                 ItemsSource="{Binding DataContext.ActiveRightMode.Items, RelativeSource={RelativeSource AncestorType=views:RightSidebarHostView}}">
            <ListBox.ItemTemplate>
                <DataTemplate DataType="models:RemoteFileNode">
                    <Grid RowDefinitions="Auto,Auto"
                          ColumnDefinitions="Auto,*"
                          RowSpacing="2"
                          ColumnSpacing="8">
                        <!-- icon + name -->
                        <!-- type/size/fullpath secondary line -->
                    </Grid>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </Grid>
</UserControl>
```

`MainWindow.axaml` 中新增 `SFTP` 内容区 row hover / secondary text / empty state 所需 token，删除 `CommandBar` 专属样式 token。

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SftpModeStateTemplateTests|FullyQualifiedName~RightModeViewsBindingTests|FullyQualifiedName~RightSidebarStyleConsistencyTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Views/RightModes/SftpModeView.axaml \
        src/SkylarkTerminal/Views/MainWindow.axaml \
        tests/SkylarkTerminal.Tests/RightModeViewsBindingTests.cs \
        tests/SkylarkTerminal.Tests/RightSidebarStyleConsistencyTests.cs \
        tests/SkylarkTerminal.Tests/SftpModeStateTemplateTests.cs
git commit -m "style: redesign sftp content state templates and dual-row list"
```

---

### Task 6: 同步文档并完成回归验证

**Files:**
- Modify: `README.md`
- Modify: `docs/plans/2026-03-09-right-sidebar-layout-bugfix2-design.md`
- Modify: `docs/plans/2026-03-09-right-sidebar-layout-bugfix2-implementation-plan.md`

**Step 1: Write the failing doc checklist**

在实现完成前，先列出必须同步的文档差异：

```markdown
- README 中 `SFTP` header 不能再描述为 `CommandBar`
- 设计文档需标记实现状态与最终命名
- implementation plan 末尾补实际验证命令与结果记录区
```

**Step 2: Run targeted regression**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightPanel|FullyQualifiedName~RightSidebar|FullyQualifiedName~RightToolsMode|FullyQualifiedName~SftpMode|FullyQualifiedName~SftpAddress" -v minimal`  
Expected: PASS。

**Step 3: Run full regression**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal`  
Expected: PASS。

Run: `dotnet build SkylarkTerminal.slnx -v minimal`  
Expected: BUILD SUCCEEDED。

**Step 4: Update docs with final terminology**

将以下术语统一：

```text
SftpCommandBarHeaderView -> SftpToolbarHeaderView
CommandBar overflow toolbar -> fixed Grid toolbar
generic action tooltip -> localized metadata-driven tooltip
```

**Step 5: Commit**

```bash
git add README.md \
        docs/plans/2026-03-09-right-sidebar-layout-bugfix2-design.md \
        docs/plans/2026-03-09-right-sidebar-layout-bugfix2-implementation-plan.md
git commit -m "docs: sync right sidebar bugfix2 implementation notes"
```

---

## Final Verification Checklist

- mode rail 显示中文 tooltip，`SFTP` 图标语义正确
- `Snippets / History / SFTP` 没有残留英文 tooltip / label
- 从 mode rail 直接切入 `SFTP` 时，模式能稳定进入 `Loading -> Loaded/Empty/Error`
- `SFTP` 地址输入框不再进入三点菜单
- `More` flyout 中无输入控件，点击外部可自然 dismiss
- `SFTP` 内容区在正常、空目录、异常三种状态下都有明确 UI
- `RightSidebarHostView` 不再引用 `SftpCommandBarHeaderView`
- `MainWindow.axaml` 不再保留 `RightSidebarSftpCommandBar` 样式
- `SshTerminalPane` 与 `RowStripedTerminalView` 未被触碰

## Execution Notes

- 建议执行时优先使用独立 worktree，避免和当前工作树脏改动互相污染。
- `Task 2` 与 `Task 4` 最容易引入交互回归，完成后应立刻手工验证右栏窄宽度场景。
- 如果 `Task 4` 期间发现 `Flyout` dismiss 语义在当前平台异常，优先排查 `ShowMode` / `Placement` / `ShouldUseOverlayLayer`，不要退回 `CommandBar` overflow。

## Suggested Verification Commands

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightPanel|FullyQualifiedName~RightSidebar|FullyQualifiedName~RightToolsMode|FullyQualifiedName~SftpMode|FullyQualifiedName~SftpAddress" -v minimal
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal
dotnet build SkylarkTerminal.slnx -v minimal
```

## Actual Execution Record

已按 Task 顺序完成实现，并在隔离 worktree `feature/right-sidebar-bugfix2` 中分别提交：

- `8723ddd` `refactor: add semantic metadata for right sidebar mode rail`
- `8d54bfb` `feat: move sftp loading state into mode view model`
- `99b6471` `refactor: centralize right sidebar command metadata and copy`
- `d453eae` `refactor: replace sftp command bar with fixed grid toolbar`
- `a0714e9` `style: redesign sftp content state templates and dual-row list`

## Actual Verification Results

执行日期：`2026-03-09`

- Targeted regression
  - Command: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightPanel|FullyQualifiedName~RightSidebar|FullyQualifiedName~RightToolsMode|FullyQualifiedName~SftpMode|FullyQualifiedName~SftpAddress" -v minimal`
  - Result: `Passed, 15/15`
- Full regression
  - Command: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal`
  - Result: `Passed, 85/85`
- Build
  - Command: `dotnet build SkylarkTerminal.slnx -v minimal`
  - Result: `Build succeeded, 0 Warning(s), 0 Error(s)`

## Final Terminology

- `SftpCommandBarHeaderView` -> `SftpToolbarHeaderView`
- `CommandBar overflow toolbar` -> `fixed Grid toolbar`
- `generic action tooltip` -> `localized metadata-driven tooltip`
- `SftpItems on MainWindowViewModel` -> compatibility alias; state ownership remains in `SftpModeViewModel`
