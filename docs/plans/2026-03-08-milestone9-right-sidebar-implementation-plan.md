# Milestone 9 RightSidebar Container & Mode Switch Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在不改动 SSH 核心链路的前提下，完成右侧栏容器化重构、`TabStrip` 模式切换、`ContentControl` 动态 `UserControl` 切换，以及平滑开关动画，并保留 `SFTP` 为核心第三模式。

**Architecture:** 保留现有 `MainContentGrid + GridSplitter + ColumnDefinition` 右侧布局（A1），将内联工具内容拆分为独立视图组件。右侧头部用 `TabStrip`（B1）驱动 `SelectedRightToolsView`，内容区通过 `ContentControl + DataTemplate`（C1）按模式加载对应 `UserControl`。右侧开关采用“列宽联动 + 内容淡入滑入/淡出滑出”的双阶段流程（D1）。

**Tech Stack:** `.NET 10`, `Avalonia 11.3.12`, `FluentAvalonia 2.5.0`, `CommunityToolkit.Mvvm`, `xUnit`

---

## 开发前约束

1. 只改 Milestone 9 相关文件，不触碰 SSH/SFTP 服务实现。
2. 每个 Task 采用 TDD：先写失败测试，再实现，再回归，再提交。
3. 每个 Task 单独 commit，保证可回滚。
4. 本计划默认在当前工作树执行；若需要隔离，请先创建 worktree。

---

### Task 1: 建立右侧模式元数据与动态内容映射（ViewModel 层）

**Files:**
- Create: `src/SkylarkTerminal/Models/RightToolsModeItem.cs`
- Create: `src/SkylarkTerminal/Models/RightToolsContentNode.cs`
- Modify: `src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs`
- Test: `tests/SkylarkTerminal.Tests/RightToolsModeSwitchTests.cs`

**Step 1: 写失败测试**

```csharp
using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class RightToolsModeSwitchTests
{
    [Fact]
    public void RightToolsModeItems_ShouldContainSnippetsHistorySftp_InOrder()
    {
        var vm = new MainWindowViewModel();
        var kinds = vm.RightToolsModeItems.Select(x => x.Kind).ToArray();

        Assert.Equal(
            [RightToolsViewKind.Snippets, RightToolsViewKind.History, RightToolsViewKind.Sftp],
            kinds);
    }

    [Fact]
    public void ShowHistoryToolsCommand_ShouldSyncSelectedModeItemAndContentNode()
    {
        var vm = new MainWindowViewModel();

        vm.ShowHistoryToolsCommand.Execute(null);

        Assert.Equal(RightToolsViewKind.History, vm.SelectedRightToolsModeItem!.Kind);
        Assert.IsType<HistoryRightToolsContent>(vm.CurrentRightToolsContent);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightToolsModeSwitchTests" -v minimal`  
Expected: FAIL，提示 `MainWindowViewModel` 缺少 `RightToolsModeItems/SelectedRightToolsModeItem/CurrentRightToolsContent`。

**Step 3: 最小实现**

```csharp
// RightToolsModeItem.cs
namespace SkylarkTerminal.Models;
public sealed record RightToolsModeItem(RightToolsViewKind Kind, string Title, string Glyph);

// RightToolsContentNode.cs
namespace SkylarkTerminal.Models;
public abstract record RightToolsContentNode;
public sealed record SnippetsRightToolsContent : RightToolsContentNode;
public sealed record HistoryRightToolsContent : RightToolsContentNode;
public sealed record SftpRightToolsContent : RightToolsContentNode;
```

```csharp
// MainWindowViewModel.cs (关键片段)
public ObservableCollection<RightToolsModeItem> RightToolsModeItems { get; } =
[
    new(RightToolsViewKind.Snippets, "Snippets", "\uE8D2"),
    new(RightToolsViewKind.History, "History", "\uE81C"),
    new(RightToolsViewKind.Sftp, "SFTP", "\uE8B7"),
];

[ObservableProperty]
private RightToolsModeItem? selectedRightToolsModeItem;

public RightToolsContentNode CurrentRightToolsContent => SelectedRightToolsView switch
{
    RightToolsViewKind.Snippets => new SnippetsRightToolsContent(),
    RightToolsViewKind.History => new HistoryRightToolsContent(),
    _ => new SftpRightToolsContent(),
};

partial void OnSelectedRightToolsModeItemChanged(RightToolsModeItem? value)
{
    if (value is not null && value.Kind != SelectedRightToolsView)
    {
        SelectedRightToolsView = value.Kind;
    }
}

partial void OnSelectedRightToolsViewChanged(RightToolsViewKind value)
{
    OnPropertyChanged(nameof(IsSnippetsView));
    OnPropertyChanged(nameof(IsHistoryView));
    OnPropertyChanged(nameof(IsSftpView));
    OnPropertyChanged(nameof(CurrentRightToolsContent));

    var target = RightToolsModeItems.FirstOrDefault(x => x.Kind == value);
    if (target is not null && !ReferenceEquals(target, SelectedRightToolsModeItem))
    {
        SelectedRightToolsModeItem = target;
    }
}
```

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightToolsModeSwitchTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Models/RightToolsModeItem.cs \
        src/SkylarkTerminal/Models/RightToolsContentNode.cs \
        src/SkylarkTerminal/ViewModels/MainWindowViewModel.cs \
        tests/SkylarkTerminal.Tests/RightToolsModeSwitchTests.cs
git commit -m "feat: add right tools mode metadata and content mapping"
```

---

### Task 2: 拆分模式内容为独立 UserControl（Snippets / History / SFTP）

**Files:**
- Create: `src/SkylarkTerminal/Views/SnippetsToolsView.axaml`
- Create: `src/SkylarkTerminal/Views/SnippetsToolsView.axaml.cs`
- Create: `src/SkylarkTerminal/Views/HistoryToolsView.axaml`
- Create: `src/SkylarkTerminal/Views/HistoryToolsView.axaml.cs`
- Create: `src/SkylarkTerminal/Views/SftpToolsView.axaml`
- Create: `src/SkylarkTerminal/Views/SftpToolsView.axaml.cs`
- Test: `tests/SkylarkTerminal.Tests/RightToolsViewsTemplateTests.cs`

**Step 1: 写失败测试**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightToolsViewsTemplateTests
{
    [Fact]
    public void SnippetsToolsView_ShouldBindSnippetItems()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(repoRoot, "src", "SkylarkTerminal", "Views", "SnippetsToolsView.axaml");
        Assert.True(File.Exists(path));
        var xaml = File.ReadAllText(path);
        Assert.Contains("ItemsSource=\"{Binding SnippetItems}\"", xaml);
    }

    [Fact]
    public void HistoryToolsView_ShouldBindHistoryItems()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(repoRoot, "src", "SkylarkTerminal", "Views", "HistoryToolsView.axaml");
        Assert.True(File.Exists(path));
        var xaml = File.ReadAllText(path);
        Assert.Contains("ItemsSource=\"{Binding HistoryItems}\"", xaml);
    }

    [Fact]
    public void SftpToolsView_ShouldBindSftpItems()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(repoRoot, "src", "SkylarkTerminal", "Views", "SftpToolsView.axaml");
        Assert.True(File.Exists(path));
        var xaml = File.ReadAllText(path);
        Assert.Contains("ItemsSource=\"{Binding SftpItems}\"", xaml);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightToolsViewsTemplateTests" -v minimal`  
Expected: FAIL，提示文件不存在。

**Step 3: 最小实现**

```xml
<!-- SnippetsToolsView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:models="using:SkylarkTerminal.Models"
             xmlns:vm="using:SkylarkTerminal.ViewModels"
             x:Class="SkylarkTerminal.Views.SnippetsToolsView"
             x:DataType="vm:MainWindowViewModel">
    <ListBox ItemsSource="{Binding SnippetItems}">
        <ListBox.ItemTemplate>
            <DataTemplate DataType="models:CommandSnippet">
                <StackPanel Spacing="2">
                    <TextBlock FontWeight="SemiBold" Text="{Binding Name}"/>
                    <TextBlock FontFamily="Consolas" Opacity="0.86" Text="{Binding Command}"/>
                </StackPanel>
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>
</UserControl>
```

`HistoryToolsView.axaml` 与 `SftpToolsView.axaml` 复用当前 `MainWindow.axaml` 中既有模板结构，只做视图拆分，不做额外业务改动。

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightToolsViewsTemplateTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Views/SnippetsToolsView.axaml \
        src/SkylarkTerminal/Views/SnippetsToolsView.axaml.cs \
        src/SkylarkTerminal/Views/HistoryToolsView.axaml \
        src/SkylarkTerminal/Views/HistoryToolsView.axaml.cs \
        src/SkylarkTerminal/Views/SftpToolsView.axaml \
        src/SkylarkTerminal/Views/SftpToolsView.axaml.cs \
        tests/SkylarkTerminal.Tests/RightToolsViewsTemplateTests.cs
git commit -m "refactor: split right tools content into dedicated views"
```

---

### Task 3: 新建 RightToolsHostView（TabStrip + ContentControl + DataTemplate）

**Files:**
- Create: `src/SkylarkTerminal/Views/RightToolsHostView.axaml`
- Create: `src/SkylarkTerminal/Views/RightToolsHostView.axaml.cs`
- Test: `tests/SkylarkTerminal.Tests/RightToolsHostViewTemplateTests.cs`

**Step 1: 写失败测试**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightToolsHostViewTemplateTests
{
    [Fact]
    public void RightToolsHostView_ShouldContainTabStripAndContentControlTemplates()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(repoRoot, "src", "SkylarkTerminal", "Views", "RightToolsHostView.axaml");
        Assert.True(File.Exists(path));

        var xaml = File.ReadAllText(path);
        Assert.Contains("<ui:TabStrip", xaml);
        Assert.Contains("ItemsSource=\"{Binding RightToolsModeItems}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedRightToolsModeItem, Mode=TwoWay}\"", xaml);
        Assert.Contains("Content=\"{Binding CurrentRightToolsContent}\"", xaml);
        Assert.Contains("<views:SnippetsToolsView/>", xaml);
        Assert.Contains("<views:HistoryToolsView/>", xaml);
        Assert.Contains("<views:SftpToolsView/>", xaml);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightToolsHostViewTemplateTests" -v minimal`  
Expected: FAIL，提示文件不存在。

**Step 3: 最小实现**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="using:FluentAvalonia.UI.Controls"
             xmlns:views="using:SkylarkTerminal.Views"
             xmlns:models="using:SkylarkTerminal.Models"
             xmlns:vm="using:SkylarkTerminal.ViewModels"
             x:Class="SkylarkTerminal.Views.RightToolsHostView"
             x:DataType="vm:MainWindowViewModel">
    <Grid RowDefinitions="Auto,*">
        <ui:TabStrip Classes="RightToolsModeStrip"
                     ItemsSource="{Binding RightToolsModeItems}"
                     SelectedItem="{Binding SelectedRightToolsModeItem, Mode=TwoWay}">
            <ui:TabStrip.ItemTemplate>
                <DataTemplate DataType="models:RightToolsModeItem">
                    <StackPanel Orientation="Horizontal" Spacing="6">
                        <TextBlock FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" Text="{Binding Glyph}"/>
                        <TextBlock Text="{Binding Title}"/>
                    </StackPanel>
                </DataTemplate>
            </ui:TabStrip.ItemTemplate>
        </ui:TabStrip>

        <ContentControl Grid.Row="1" Content="{Binding CurrentRightToolsContent}">
            <ContentControl.DataTemplates>
                <DataTemplate DataType="models:SnippetsRightToolsContent"><views:SnippetsToolsView/></DataTemplate>
                <DataTemplate DataType="models:HistoryRightToolsContent"><views:HistoryToolsView/></DataTemplate>
                <DataTemplate DataType="models:SftpRightToolsContent"><views:SftpToolsView/></DataTemplate>
            </ContentControl.DataTemplates>
        </ContentControl>
    </Grid>
</UserControl>
```

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightToolsHostViewTemplateTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Views/RightToolsHostView.axaml \
        src/SkylarkTerminal/Views/RightToolsHostView.axaml.cs \
        tests/SkylarkTerminal.Tests/RightToolsHostViewTemplateTests.cs
git commit -m "feat: add right tools host view with tabstrip and content templates"
```

---

### Task 4: MainWindow 接入 RightToolsHostView，移除右侧栏内联内容

**Files:**
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml`
- Test: `tests/SkylarkTerminal.Tests/MainWindowRightSidebarCompositionTests.cs`

**Step 1: 写失败测试**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class MainWindowRightSidebarCompositionTests
{
    [Fact]
    public void MainWindow_ShouldUseRightToolsHostView_InRightSidebar()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(repoRoot, "src", "SkylarkTerminal", "Views", "MainWindow.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("<views:RightToolsHostView", xaml);
        Assert.DoesNotContain("Content=\"Snippet\"", xaml);
        Assert.DoesNotContain("Tools Panel (E)", xaml);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~MainWindowRightSidebarCompositionTests" -v minimal`  
Expected: FAIL，当前 `MainWindow.axaml` 仍有内联按钮组与内联 ListBox。

**Step 3: 最小实现**

在 `MainWindow.axaml` 右侧栏 `Border Grid.Column="5"` 内：

1. 保留右键菜单与外层容器。
2. 删除旧的 `Snippet/History/SFTP` 按钮组与 3 个内联 `ListBox`。
3. 替换为：

```xml
<views:RightToolsHostView DataContext="{Binding}" />
```

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~MainWindowRightSidebarCompositionTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Views/MainWindow.axaml \
        tests/SkylarkTerminal.Tests/MainWindowRightSidebarCompositionTests.cs
git commit -m "refactor: compose right sidebar with RightToolsHostView"
```

---

### Task 5: 实现 D1 平滑开关（双阶段）与可测试策略

**Files:**
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml`
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml.cs`
- Modify: `src/SkylarkTerminal/Views/MainWindowInteractionPolicy.cs`
- Test: `tests/SkylarkTerminal.Tests/MainWindowRightSidebarAnimationPolicyTests.cs`

**Step 1: 写失败测试（策略层）**

```csharp
using SkylarkTerminal.Views;

namespace SkylarkTerminal.Tests;

public class MainWindowRightSidebarAnimationPolicyTests
{
    [Theory]
    [InlineData(180d, 220d, true)]
    [InlineData(260d, 220d, false)]
    public void ShouldAutoCollapseRightSidebar_ShouldRespectThreshold(double width, double threshold, bool expected)
    {
        Assert.Equal(expected, MainWindowInteractionPolicy.ShouldAutoCollapseRightSidebar(width, threshold));
    }

    [Theory]
    [InlineData(0d, 340d, 340d)]
    [InlineData(280d, 340d, 280d)]
    public void ResolveRightSidebarRestoreWidth_ShouldUseFallbackWhenZero(double current, double fallback, double expected)
    {
        Assert.Equal(expected, MainWindowInteractionPolicy.ResolveRightSidebarRestoreWidth(current, fallback));
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~MainWindowRightSidebarAnimationPolicyTests" -v minimal`  
Expected: FAIL，策略方法尚不存在。

**Step 3: 最小实现**

```csharp
// MainWindowInteractionPolicy.cs
public static bool ShouldAutoCollapseRightSidebar(double width, double threshold)
    => width <= threshold;

public static double ResolveRightSidebarRestoreWidth(double currentWidth, double fallbackWidth)
    => currentWidth > 0d ? currentWidth : fallbackWidth;
```

`MainWindow.axaml` / `MainWindow.axaml.cs` 关键改造：

1. 给右侧容器和其内容根节点命名（如 `RightSidebarContainer`、`RightSidebarContentRoot`）。
2. 右侧内容根节点加 `Opacity + Margin` 过渡（`DoubleTransition + ThicknessTransition`）。
3. 在 `OnViewModelPropertyChanged(IsRightSidebarVisible)` 中走“开/关动画流程”：
   - 打开：先恢复右列宽与 splitter，再将内容从 `Margin=12,0,-12,0` + `Opacity=0` 过渡到 `Margin=0` + `Opacity=1`。
   - 关闭：先播放反向动画，再在动画结束后将右列和 splitter 置 `0`。
4. 拖拽阈值判断调用 `MainWindowInteractionPolicy.ShouldAutoCollapseRightSidebar(...)`，减少魔法逻辑散落。

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~MainWindowRightSidebarAnimationPolicyTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Views/MainWindow.axaml \
        src/SkylarkTerminal/Views/MainWindow.axaml.cs \
        src/SkylarkTerminal/Views/MainWindowInteractionPolicy.cs \
        tests/SkylarkTerminal.Tests/MainWindowRightSidebarAnimationPolicyTests.cs
git commit -m "feat: add smooth right sidebar open-close animation with policy helpers"
```

---

### Task 6: 完成 B1 风格打磨（TabStrip Fluent 样式）

**Files:**
- Modify: `src/SkylarkTerminal/Views/MainWindow.axaml`
- Test: `tests/SkylarkTerminal.Tests/RightToolsStyleTemplateTests.cs`

**Step 1: 写失败测试**

```csharp
using System;
using System.IO;

namespace SkylarkTerminal.Tests;

public class RightToolsStyleTemplateTests
{
    [Fact]
    public void MainWindow_ShouldDefineRightToolsTabStripStyles()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var path = Path.Combine(repoRoot, "src", "SkylarkTerminal", "Views", "MainWindow.axaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Selector=\"ui|TabStrip.RightToolsModeStrip\"", xaml);
        Assert.Contains("Selector=\"ui|TabStripItem.RightToolsModeItem:selected\"", xaml);
        Assert.Contains("Selector=\"ui|TabStripItem.RightToolsModeItem:pointerover\"", xaml);
    }
}
```

**Step 2: 运行测试确认失败**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightToolsStyleTemplateTests" -v minimal`  
Expected: FAIL，样式选择器未定义。

**Step 3: 最小实现**

在 `MainWindow.axaml` 增加 `TabStrip` 样式：

1. `ui|TabStrip.RightToolsModeStrip`：高度、边距、背景。
2. `ui|TabStripItem.RightToolsModeItem`：默认态文本透明度、内边距、圆角。
3. `pointerover/pressed/selected` 三态：选中态底部指示线 + 对比度增强。

并在 `RightToolsHostView.axaml` 给控件补 class：

```xml
<ui:TabStrip Classes="RightToolsModeStrip" ...>
<ui:TabStrip.ItemContainerTheme>
    <!-- 或直接给 TabStripItem class: RightToolsModeItem -->
</ui:TabStrip.ItemContainerTheme>
```

**Step 4: 运行测试确认通过**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~RightToolsStyleTemplateTests" -v minimal`  
Expected: PASS。

**Step 5: 提交**

```bash
git add src/SkylarkTerminal/Views/MainWindow.axaml \
        src/SkylarkTerminal/Views/RightToolsHostView.axaml \
        tests/SkylarkTerminal.Tests/RightToolsStyleTemplateTests.cs
git commit -m "style: polish right tools mode tabstrip in fluent style"
```

---

### Task 7: 回归验证与文档同步

**Files:**
- Modify: `README.md`（可选，若需要同步描述）

**Step 1: 运行右侧栏相关测试集**

Run:

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj \
  --filter "FullyQualifiedName~RightTools|FullyQualifiedName~MainWindowRightSidebar" -v minimal
```

Expected: 与本 Milestone 相关用例全部 PASS。

**Step 2: 运行完整测试（若基线允许）**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal`  
Expected: 全量 PASS；若存在历史遗留失败，需在提交说明标记“pre-existing failure”。

**Step 3: 运行构建验证**

Run: `dotnet build SkylarkTerminal.slnx -v minimal`  
Expected: `Build succeeded.`

**Step 4: 更新 README（可选）**

将“右侧工具区”描述更新为：

- `TabStrip` 模式切换（Snippets/History/SFTP）
- `ContentControl` 动态视图装载
- 平滑开关动画 + splitter 宽度联动

**Step 5: 提交**

```bash
git add README.md
git commit -m "docs: refresh right sidebar architecture and behavior notes"
```

---

## 验收门槛（Done Definition）

1. 顶部按钮触发右侧栏时有平滑开关效果。
2. 右侧栏展开后 `GridSplitter` 可调宽，收起后不占空间。
3. `TabStrip` 可切换 `Snippets/History/SFTP`，并驱动 `ContentControl` 动态加载对应 `UserControl`。
4. 左侧资产区与 Workspace 拖拽分屏行为无回归。
5. 相关测试与构建通过。

## 回滚方案

1. 若动画引入闪烁：先保留容器化与 C1，临时关闭关闭阶段动画（保留列宽联动）。
2. 若 `TabStrip` 样式不稳定：回退样式定义，不回退 `RightToolsHostView` 组件化结构。
3. 若 DataTemplate 映射异常：回退到上一提交并定位 `CurrentRightToolsContent` 映射关系。

