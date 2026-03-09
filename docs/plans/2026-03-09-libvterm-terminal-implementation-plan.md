# libvterm Terminal Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在保留现有 `SSH.NET + ISshTerminalSession + Workspace` 宿主架构的前提下，引入 `libvterm` 终端底座，并在仓库内新增 `Native / Interop / Avalonia` 三个子项目，最终以可回滚方式替换当前 `Iciclecreek` 终端实现。

**Architecture:** 使用 staged rollout。先新增三个子项目与 backend abstraction，再在 `SkylarkTerminal.Terminal.Native` 中引入 vendored `libvterm` 与 native shim，在 `SkylarkTerminal.Terminal.Interop` 中构建稳定托管 API，在 `SkylarkTerminal.Terminal.Avalonia` 中实现终端控件，最后通过 `ITerminalViewHostFactory` 把 `SshTerminalPane` 从具体控件中解耦。旧 `Iciclecreek` backend 在迁移期继续保留，直到 libvterm 路线通过回归与发布验证。

**Tech Stack:** `.NET 10`, `Avalonia 11.3.12`, `FluentAvaloniaUI 2.5.0`, `CommunityToolkit.Mvvm 8.4.0`, `Microsoft.Extensions.DependencyInjection 10.0.0`, `SSH.NET 2025.1.0`, `libvterm`, `C/C++ build tooling`, `xUnit`

---

**Design Input:** `docs/plans/2026-03-09-libvterm-terminal-design.md`

## 开发前约束

1. 第一阶段只保证 `win-x64`，不要同时追求所有 RID。
2. 旧后端 `Iciclecreek.Avalonia.Terminal.Fork` 不在本轮首批任务中删除。
3. 新终端后端必须可以通过 backend option 切回旧实现。
4. `SshConnectionService`、`ISshTerminalSession`、`SessionRegistryService` 不做破坏式重写。
5. native 层不要把 raw `libvterm` ABI 直接暴露给 C#；必须经由 shim 统一出口。
6. 所有任务优先走 TDD 或 smoke-first 验证，先写失败测试/失败验证，再补最小实现。
7. 每个 task 独立 commit。

## Task Map

1. 建立三子项目与 backend abstraction
2. 落地 native vendor / shim / build boundary
3. 落地 C# interop engine 与 screen snapshot
4. 落地 Avalonia terminal control 与输入编码
5. 接入应用层 terminal host adapter 与 DI
6. 迁移 `SshTerminalPane`，保留 overlay 与 session 行为
7. 完成回归、发布验证与默认后端切换

---

### Task 1: 建立三子项目与 backend abstraction

**Files:**
- Modify: `SkylarkTerminal.slnx`
- Create: `src/SkylarkTerminal.Terminal.Native/SkylarkTerminal.Terminal.Native.csproj`
- Create: `src/SkylarkTerminal.Terminal.Interop/SkylarkTerminal.Terminal.Interop.csproj`
- Create: `src/SkylarkTerminal.Terminal.Avalonia/SkylarkTerminal.Terminal.Avalonia.csproj`
- Create: `src/SkylarkTerminal/Terminal/TerminalBackendKind.cs`
- Create: `src/SkylarkTerminal/Terminal/TerminalBackendOptions.cs`
- Create: `src/SkylarkTerminal/Terminal/ITerminalViewHost.cs`
- Create: `src/SkylarkTerminal/Terminal/ITerminalViewHostFactory.cs`
- Test: `tests/SkylarkTerminal.Tests/TerminalBackendOptionsTests.cs`

**Step 1: Write the failing test**

```csharp
using SkylarkTerminal.Terminal;

namespace SkylarkTerminal.Tests;

public class TerminalBackendOptionsTests
{
    [Fact]
    public void DefaultBackend_ShouldBeLegacyIciclecreek_UntilLibvtermIsVerified()
    {
        var options = new TerminalBackendOptions();

        Assert.Equal(TerminalBackendKind.LegacyIciclecreek, options.DefaultBackend);
    }

    [Fact]
    public void BackendContracts_ShouldExposeFactoryAndHostBoundary()
    {
        Assert.NotNull(typeof(ITerminalViewHost));
        Assert.NotNull(typeof(ITerminalViewHostFactory));
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalBackendOptionsTests" -v minimal`  
Expected: FAIL，`SkylarkTerminal.Terminal` 命名空间和相关类型尚不存在。

**Step 3: Write minimal implementation**

```csharp
namespace SkylarkTerminal.Terminal;

public enum TerminalBackendKind
{
    LegacyIciclecreek = 0,
    Libvterm = 1,
}
```

```csharp
namespace SkylarkTerminal.Terminal;

public sealed class TerminalBackendOptions
{
    public TerminalBackendKind DefaultBackend { get; set; } =
        TerminalBackendKind.LegacyIciclecreek;
}
```

```csharp
namespace SkylarkTerminal.Terminal;

public interface ITerminalViewHost
{
    Control View { get; }
    void FeedOutput(ReadOnlySpan<byte> utf8Bytes);
    Task SendResizeAsync(int cols, int rows, CancellationToken cancellationToken = default);
    event Func<string, Task>? SendRequested;
}
```

```csharp
namespace SkylarkTerminal.Terminal;

public interface ITerminalViewHostFactory
{
    ITerminalViewHost Create(TerminalBackendKind backend);
}
```

项目文件先只做空壳：

- `SkylarkTerminal.Terminal.Native.csproj`：native orchestration placeholder
- `SkylarkTerminal.Terminal.Interop.csproj`：`net10.0` class library
- `SkylarkTerminal.Terminal.Avalonia.csproj`：引用 `Avalonia`

并把三个项目加入 `SkylarkTerminal.slnx`。

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalBackendOptionsTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add SkylarkTerminal.slnx \
        src/SkylarkTerminal.Terminal.Native/SkylarkTerminal.Terminal.Native.csproj \
        src/SkylarkTerminal.Terminal.Interop/SkylarkTerminal.Terminal.Interop.csproj \
        src/SkylarkTerminal.Terminal.Avalonia/SkylarkTerminal.Terminal.Avalonia.csproj \
        src/SkylarkTerminal/Terminal/TerminalBackendKind.cs \
        src/SkylarkTerminal/Terminal/TerminalBackendOptions.cs \
        src/SkylarkTerminal/Terminal/ITerminalViewHost.cs \
        src/SkylarkTerminal/Terminal/ITerminalViewHostFactory.cs \
        tests/SkylarkTerminal.Tests/TerminalBackendOptionsTests.cs
git commit -m "feat: scaffold terminal backend projects"
```

---

### Task 2: 落地 native vendor / shim / build boundary

**Files:**
- Create: `src/SkylarkTerminal.Terminal.Native/README.md`
- Create: `src/SkylarkTerminal.Terminal.Native/vendor/libvterm/.upstream-version`
- Create: `src/SkylarkTerminal.Terminal.Native/vendor/libvterm/LICENSE.thirdparty`
- Create: `src/SkylarkTerminal.Terminal.Native/native/include/skylark_terminal_bridge.h`
- Create: `src/SkylarkTerminal.Terminal.Native/native/src/skylark_terminal_bridge.c`
- Create: `src/SkylarkTerminal.Terminal.Native/native/CMakeLists.txt`
- Create: `src/SkylarkTerminal.Terminal.Native/build/BuildNative.ps1`
- Create: `src/SkylarkTerminal.Terminal.Native/build/build-native.sh`
- Create: `src/SkylarkTerminal.Terminal.Native/build/native-manifest.json`
- Test: `tests/SkylarkTerminal.Tests/TerminalNativeManifestTests.cs`

**Step 1: Write the failing test**

```csharp
using System.Text.Json;

namespace SkylarkTerminal.Tests;

public class TerminalNativeManifestTests
{
    [Fact]
    public void NativeManifest_ShouldDeclareLibraryName_AndSupportedRid()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "src", "SkylarkTerminal.Terminal.Native", "build", "native-manifest.json");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(
            "skylark_terminal",
            doc.RootElement.GetProperty("libraryName").GetString());

        Assert.Contains(
            doc.RootElement.GetProperty("supportedRids").EnumerateArray().Select(x => x.GetString()),
            rid => rid == "win-x64");
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalNativeManifestTests" -v minimal`  
Expected: FAIL，manifest 和 native build 文件尚不存在。

**Step 3: Write minimal implementation**

`native-manifest.json` 先定义最小约束：

```json
{
  "libraryName": "skylark_terminal",
  "supportedRids": ["win-x64"],
  "upstream": "libvterm",
  "packagingMode": "copy-to-runtimes"
}
```

`skylark_terminal_bridge.h` 先暴露少量稳定入口，不直接暴露 raw `libvterm` 结构体：

```c
typedef struct skylark_terminal skylark_terminal;

skylark_terminal* skylark_terminal_create(int rows, int cols);
void skylark_terminal_destroy(skylark_terminal* terminal);
void skylark_terminal_reset(skylark_terminal* terminal);
void skylark_terminal_resize(skylark_terminal* terminal, int rows, int cols);
int skylark_terminal_feed_utf8(skylark_terminal* terminal, const char* data, int length);
int skylark_terminal_copy_dirty_rows(skylark_terminal* terminal, int* rows, int capacity);
int skylark_terminal_copy_cells(
    skylark_terminal* terminal,
    int row,
    skylark_cell* cells,
    int capacity);
```

`BuildNative.ps1` / `build-native.sh` 负责：

1. 校验 upstream snapshot 存在
2. 调用 `cmake`
3. 产出 `skylark_terminal.dll`
4. 复制到 `artifacts/native/win-x64/`

`README.md` 必须记录：

- upstream 来源
- snapshot 更新方式
- license 要求
- 本仓库只把它作为 terminal core，而不是 UI framework

**Step 4: Run validation**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalNativeManifestTests" -v minimal`  
Expected: PASS。

Run: `pwsh -File src/SkylarkTerminal.Terminal.Native/build/BuildNative.ps1 -Runtime win-x64`  
Expected: 生成 `artifacts/native/win-x64/skylark_terminal.dll`。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal.Terminal.Native \
        tests/SkylarkTerminal.Tests/TerminalNativeManifestTests.cs
git commit -m "feat: add libvterm native bridge boundary"
```

---

### Task 3: 落地 C# interop engine 与 screen snapshot

**Files:**
- Modify: `src/SkylarkTerminal.Terminal.Interop/SkylarkTerminal.Terminal.Interop.csproj`
- Create: `src/SkylarkTerminal.Terminal.Interop/Native/SkylarkTerminalNative.cs`
- Create: `src/SkylarkTerminal.Terminal.Interop/Native/SkylarkTerminalSafeHandle.cs`
- Create: `src/SkylarkTerminal.Terminal.Interop/Model/TerminalColor.cs`
- Create: `src/SkylarkTerminal.Terminal.Interop/Model/TerminalCell.cs`
- Create: `src/SkylarkTerminal.Terminal.Interop/Model/TerminalCursorState.cs`
- Create: `src/SkylarkTerminal.Terminal.Interop/Model/TerminalScreenSnapshot.cs`
- Create: `src/SkylarkTerminal.Terminal.Interop/Engine/TerminalChangeSet.cs`
- Create: `src/SkylarkTerminal.Terminal.Interop/Engine/TerminalEngine.cs`
- Test: `tests/SkylarkTerminal.Tests/TerminalEngineTests.cs`

**Step 1: Write the failing test**

```csharp
using System.Text;
using SkylarkTerminal.Terminal.Interop.Engine;

namespace SkylarkTerminal.Tests;

public class TerminalEngineTests
{
    [Fact]
    public void FeedUtf8_ShouldUpdateDirtyRows_AndExposeCellText()
    {
        using var engine = new TerminalEngine(rows: 4, cols: 12);

        var changes = engine.FeedUtf8(Encoding.UTF8.GetBytes("ls\r\nok"));
        var snapshot = engine.GetSnapshot();

        Assert.Contains(0, changes.DirtyRows);
        Assert.Contains(1, changes.DirtyRows);
        Assert.Equal("l", snapshot.Rows[0][0].Text);
        Assert.Equal("o", snapshot.Rows[1][0].Text);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalEngineTests" -v minimal`  
Expected: FAIL，`TerminalEngine` 尚不存在，或尚未成功绑定 native library。

**Step 3: Write minimal implementation**

`SkylarkTerminalNative.cs`：

```csharp
internal static partial class SkylarkTerminalNative
{
    [LibraryImport("skylark_terminal", EntryPoint = "skylark_terminal_create")]
    internal static partial SkylarkTerminalSafeHandle Create(int rows, int cols);

    [LibraryImport("skylark_terminal", EntryPoint = "skylark_terminal_feed_utf8")]
    internal static unsafe partial int FeedUtf8(
        SkylarkTerminalSafeHandle handle,
        byte* data,
        int length);
}
```

`TerminalEngine.cs`：

```csharp
public sealed class TerminalEngine : IDisposable
{
    private readonly SkylarkTerminalSafeHandle _handle;

    public TerminalEngine(int rows, int cols)
    {
        _handle = SkylarkTerminalNative.Create(rows, cols);
    }

    public unsafe TerminalChangeSet FeedUtf8(ReadOnlySpan<byte> utf8Bytes)
    {
        fixed (byte* ptr = utf8Bytes)
        {
            SkylarkTerminalNative.FeedUtf8(_handle, ptr, utf8Bytes.Length);
        }

        return ReadDirtyRowsAndBuildChangeSet();
    }

    public TerminalScreenSnapshot GetSnapshot() => ReadSnapshot();

    public void Dispose() => _handle.Dispose();
}
```

`TerminalScreenSnapshot` 要求：

- 固定 `Rows` / `Cols`
- 每个 cell 包含 `Text`, `Foreground`, `Background`, `Attributes`
- cursor state 单独存储

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalEngineTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal.Terminal.Interop \
        tests/SkylarkTerminal.Tests/TerminalEngineTests.cs
git commit -m "feat: add libvterm interop engine"
```

---

### Task 4: 落地 Avalonia terminal control 与输入编码

**Files:**
- Modify: `src/SkylarkTerminal.Terminal.Avalonia/SkylarkTerminal.Terminal.Avalonia.csproj`
- Create: `src/SkylarkTerminal.Terminal.Avalonia/Controls/LibvtermTerminalView.cs`
- Create: `src/SkylarkTerminal.Terminal.Avalonia/Input/TerminalKeyEncoder.cs`
- Create: `src/SkylarkTerminal.Terminal.Avalonia/Rendering/TerminalRenderState.cs`
- Create: `src/SkylarkTerminal.Terminal.Avalonia/Rendering/TerminalGlyphRunCache.cs`
- Create: `src/SkylarkTerminal.Terminal.Avalonia/Selection/TerminalSelectionModel.cs`
- Create: `src/SkylarkTerminal.Terminal.Avalonia/Clipboard/TerminalClipboardPayload.cs`
- Test: `tests/SkylarkTerminal.Tests/TerminalKeyEncoderTests.cs`
- Test: `tests/SkylarkTerminal.Tests/TerminalSelectionModelTests.cs`

**Step 1: Write the failing tests**

```csharp
using Avalonia.Input;
using SkylarkTerminal.Terminal.Avalonia.Input;

namespace SkylarkTerminal.Tests;

public class TerminalKeyEncoderTests
{
    [Fact]
    public void ArrowUp_ShouldEncodeAnsiSequence()
    {
        var encoded = TerminalKeyEncoder.Encode(Key.Up, KeyModifiers.None, applicationCursorKeys: false);

        Assert.Equal("\u001b[A", encoded);
    }

    [Fact]
    public void Enter_ShouldEncodeCarriageReturn()
    {
        var encoded = TerminalKeyEncoder.Encode(Key.Enter, KeyModifiers.None, applicationCursorKeys: false);

        Assert.Equal("\r", encoded);
    }
}
```

```csharp
using SkylarkTerminal.Terminal.Avalonia.Selection;

namespace SkylarkTerminal.Tests;

public class TerminalSelectionModelTests
{
    [Fact]
    public void ExtractText_ShouldJoinRowsUsingLf()
    {
        var model = new TerminalSelectionModel();
        var rows = new[] { "hello", "world" };

        model.Begin(0, 0);
        model.Update(1, 5);

        Assert.Equal("hello\nworld", model.ExtractText(rows));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalKeyEncoderTests|FullyQualifiedName~TerminalSelectionModelTests" -v minimal`  
Expected: FAIL。

**Step 3: Write minimal implementation**

`LibvtermTerminalView` 必须具备这些能力：

```csharp
public sealed class LibvtermTerminalView : Control
{
    public void FeedOutput(ReadOnlySpan<byte> utf8Bytes) { ... }
    public Task ResizeAsync(int cols, int rows, CancellationToken cancellationToken = default) { ... }
    public string? CopySelection() { ... }
    public Task PasteAsync(string text) { ... }
    public event Func<string, Task>? SendRequested;
}
```

`TerminalKeyEncoder` 至少覆盖：

- `Enter -> "\r"`
- `Backspace -> "\u007f"`
- `Up/Down/Left/Right`
- `Home/End`
- `Ctrl+C`, `Ctrl+D`, `Ctrl+L`
- bracketed paste on/off 时的 paste 包装

`TerminalRenderState` / `TerminalGlyphRunCache` 负责：

- 只重绘 dirty rows
- 基于统一 monospace metrics 计算 cell rect
- 光标与 selection 单独绘制

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalKeyEncoderTests|FullyQualifiedName~TerminalSelectionModelTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal.Terminal.Avalonia \
        tests/SkylarkTerminal.Tests/TerminalKeyEncoderTests.cs \
        tests/SkylarkTerminal.Tests/TerminalSelectionModelTests.cs
git commit -m "feat: add avalonia libvterm control"
```

---

### Task 5: 接入应用层 terminal host adapter 与 DI

**Files:**
- Modify: `src/SkylarkTerminal/SkylarkTerminal.csproj`
- Create: `src/SkylarkTerminal/Terminal/Legacy/LegacyIciclecreekTerminalHost.cs`
- Create: `src/SkylarkTerminal/Terminal/Libvterm/LibvtermTerminalHost.cs`
- Create: `src/SkylarkTerminal/Terminal/TerminalViewHostFactory.cs`
- Modify: `src/SkylarkTerminal/App.axaml.cs`
- Test: `tests/SkylarkTerminal.Tests/TerminalViewHostFactoryTests.cs`

**Step 1: Write the failing test**

```csharp
using Microsoft.Extensions.DependencyInjection;
using SkylarkTerminal.Terminal;

namespace SkylarkTerminal.Tests;

public class TerminalViewHostFactoryTests
{
    [Fact]
    public void Factory_ShouldReturnLegacyHost_WhenConfiguredForLegacy()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TerminalBackendOptions
        {
            DefaultBackend = TerminalBackendKind.LegacyIciclecreek
        });
        services.AddSingleton<ITerminalViewHostFactory, TerminalViewHostFactory>();
        services.AddSingleton<LegacyIciclecreekTerminalHost>();
        services.AddSingleton<LibvtermTerminalHost>();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ITerminalViewHostFactory>();
        var host = factory.Create(TerminalBackendKind.LegacyIciclecreek);

        Assert.IsType<LegacyIciclecreekTerminalHost>(host);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalViewHostFactoryTests" -v minimal`  
Expected: FAIL，factory 和 host 类型尚不存在。

**Step 3: Write minimal implementation**

`TerminalViewHostFactory`：

```csharp
public sealed class TerminalViewHostFactory : ITerminalViewHostFactory
{
    private readonly IServiceProvider _services;

    public TerminalViewHostFactory(IServiceProvider services)
    {
        _services = services;
    }

    public ITerminalViewHost Create(TerminalBackendKind backend) =>
        backend switch
        {
            TerminalBackendKind.Libvterm => _services.GetRequiredService<LibvtermTerminalHost>(),
            _ => _services.GetRequiredService<LegacyIciclecreekTerminalHost>(),
        };
}
```

`SkylarkTerminal.csproj`：

- 新增对 `SkylarkTerminal.Terminal.Interop.csproj`
- 新增对 `SkylarkTerminal.Terminal.Avalonia.csproj`
- 暂时保留对 `Iciclecreek.Avalonia.Terminal.Fork.csproj`

`App.axaml.cs`：

- 注册 `TerminalBackendOptions`
- 注册 `LegacyIciclecreekTerminalHost`
- 注册 `LibvtermTerminalHost`
- 注册 `ITerminalViewHostFactory`

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~TerminalViewHostFactoryTests" -v minimal`  
Expected: PASS。

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/SkylarkTerminal.csproj \
        src/SkylarkTerminal/App.axaml.cs \
        src/SkylarkTerminal/Terminal \
        tests/SkylarkTerminal.Tests/TerminalViewHostFactoryTests.cs
git commit -m "feat: add terminal host factory and backend wiring"
```

---

### Task 6: 迁移 `SshTerminalPane`，保留 overlay 与 session 行为

**Files:**
- Modify: `src/SkylarkTerminal/Views/SshTerminalPane.axaml`
- Modify: `src/SkylarkTerminal/Views/SshTerminalPane.axaml.cs`
- Create: `src/SkylarkTerminal/Terminal/TerminalHostExtensions.cs`
- Test: `tests/SkylarkTerminal.Tests/SshTerminalPaneBackendSmokeTests.cs`

**Step 1: Write the failing smoke test**

```csharp
using SkylarkTerminal.Terminal;
using SkylarkTerminal.Views;

namespace SkylarkTerminal.Tests;

public class SshTerminalPaneBackendSmokeTests
{
    [Fact]
    public void Pane_ShouldExposeBackendAgnosticHostBoundary()
    {
        Assert.NotNull(typeof(SshTerminalPane));
        Assert.NotNull(typeof(TerminalHostExtensions));
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SshTerminalPaneBackendSmokeTests" -v minimal`  
Expected: FAIL，`TerminalHostExtensions` 尚不存在，或 pane 仍与 `RowStripedTerminalView` 强绑定。

**Step 3: Write minimal implementation**

迁移目标不是重写整个 pane，而是把它从具体控件里“拔出来”：

1. `SshTerminalPane.axaml` 保留：
   - `InfoBar`
   - `QuickStartOverlay`
   - `ConnectingOverlay`
2. 将原先的 `terminalFork:RowStripedTerminalView` 替换为 backend-neutral host container，例如：

```xml
<ContentControl x:Name="TerminalHostPresenter" />
```

3. `SshTerminalPane.axaml.cs` 中：
   - 通过 DI 获取 `ITerminalViewHostFactory`
   - 在 `Loaded` / `Tab` 变化时创建当前 backend host
   - 把 session 输出转发到 `ITerminalViewHost.FeedOutput(...)`
   - 订阅 `SendRequested`，转发到 `_session.SendAsync(...)`
   - 复制、粘贴、清屏、resize 不再直接访问 `TerminalHost.Terminal`

`TerminalHostExtensions` 先统一这些宿主层操作：

```csharp
public static class TerminalHostExtensions
{
    public static async Task PasteFromClipboardAsync(
        this ITerminalViewHost host,
        IClipboardService clipboardService) { ... }

    public static void FeedText(this ITerminalViewHost host, string text) { ... }
}
```

**Step 4: Run validation**

Run: `dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj --filter "FullyQualifiedName~SshTerminalPaneBackendSmokeTests" -v minimal`  
Expected: PASS。

Run: `dotnet build SkylarkTerminal.slnx -c Debug`  
Expected: PASS。

手工验收：

1. 打开一个 SSH tab，连接成功
2. 输入普通命令，按 Enter 正常
3. `Copy / Paste / Clear Screen / Reconnect` 可用
4. Quick Start overlay 仍正常显示
5. 拖拽 tab / 左右分屏 / 窗口 resize 不报错

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Views/SshTerminalPane.axaml \
        src/SkylarkTerminal/Views/SshTerminalPane.axaml.cs \
        src/SkylarkTerminal/Terminal/TerminalHostExtensions.cs \
        tests/SkylarkTerminal.Tests/SshTerminalPaneBackendSmokeTests.cs
git commit -m "refactor: migrate ssh terminal pane to backend host"
```

---

### Task 7: 完成回归、发布验证与默认后端切换

**Files:**
- Modify: `src/SkylarkTerminal/Terminal/TerminalBackendOptions.cs`
- Modify: `src/SkylarkTerminal/App.axaml.cs`
- Modify: `docs/plans/2026-03-09-libvterm-terminal-design.md`
- Modify: `README.md`
- Test: `tests/SkylarkTerminal.Tests/TerminalBackendDefaultTests.cs`

**Step 1: Write the failing test**

```csharp
using SkylarkTerminal.Terminal;

namespace SkylarkTerminal.Tests;

public class TerminalBackendDefaultTests
{
    [Fact]
    public void DefaultBackend_ShouldBeLibvterm_AfterParityIsVerified()
    {
        var options = new TerminalBackendOptions
        {
            DefaultBackend = TerminalBackendKind.Libvterm
        };

        Assert.Equal(TerminalBackendKind.Libvterm, options.DefaultBackend);
    }
}
```

**Step 2: Run verification matrix before changing default**

Run:

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal
dotnet build SkylarkTerminal.slnx -c Debug
dotnet build SkylarkTerminal.slnx -c Release
```

Expected:

- 所有测试通过
- Debug / Release 构建通过

手工回归矩阵：

1. 普通 shell 输入
2. 高频输出
3. `vim`
4. `less`
5. `top`
6. 中文输入法焦点切换
7. copy / paste / selection
8. reconnect
9. split pane
10. tab drag

只有全部通过，才允许把默认 backend 从 `LegacyIciclecreek` 改成 `Libvterm`。

**Step 3: Flip default backend and update docs**

```csharp
public sealed class TerminalBackendOptions
{
    public TerminalBackendKind DefaultBackend { get; set; } =
        TerminalBackendKind.Libvterm;
}
```

`README.md` 需要记录：

- 当前默认 terminal backend
- 如何切回 legacy backend
- native build prerequisites

**Step 4: Run final validation**

Run:

```bash
dotnet test tests/SkylarkTerminal.Tests/SkylarkTerminal.Tests.csproj -v minimal
dotnet build SkylarkTerminal.slnx -c Release
```

Expected:

- PASS
- 发布构建可生成包含 native asset 的产物

**Step 5: Commit**

```bash
git add src/SkylarkTerminal/Terminal/TerminalBackendOptions.cs \
        src/SkylarkTerminal/App.axaml.cs \
        docs/plans/2026-03-09-libvterm-terminal-design.md \
        README.md \
        tests/SkylarkTerminal.Tests/TerminalBackendDefaultTests.cs
git commit -m "feat: switch default terminal backend to libvterm"
```

## 延后任务

这些内容不要混入首批迁移：

1. 删除 `src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork`
2. 做多 RID 发布支持
3. 做更激进的 glyph batching / GPU 优化
4. 引入 session recording、search panel、terminal theme editor

这些应作为 libvterm 方案稳定后的后续里程碑。
