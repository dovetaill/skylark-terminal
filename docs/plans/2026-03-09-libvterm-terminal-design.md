# 2026-03-09 libvterm Terminal Design

## 背景

当前仓库的 SSH 终端链路已经跑通，但终端渲染层仍然依赖 `Iciclecreek.Avalonia.Terminal` 本地 fork：

- 视图入口：`src/SkylarkTerminal/Views/SshTerminalPane.axaml`
- 交互入口：`src/SkylarkTerminal/Views/SshTerminalPane.axaml.cs`
- 会话服务：`src/SkylarkTerminal/Services/SshConnectionService.cs`
- 第三方 fork：`src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork`

从当前实现看，真正稳定且值得保留的是上层业务链路：

- `SSH.NET + ShellStream("xterm-256color")`
- `ISshTerminalSession`
- `SessionRegistryService`
- `Workspace` 标签系统、拖拽分屏、Quick Start、右侧栏等宿主 UI

真正脆弱的是“终端解析与渲染控件”本身。当前 fork 可继续维护，但生态弱、外部参考少、长期演进风险高；而 `WebView + xterm.js` 虽然是主流路线，但会把一个纯 Avalonia、追求 Windows 11 Fluent 质感的桌面应用重新拉回浏览器宿主模型。

本轮目标不是立刻改代码，而是确定新的终端技术选型、项目拆分方式和实施顺序。

## 当前实现回顾

### 代码事实

- `SshTerminalPane.axaml` 当前直接宿主 `terminalFork:RowStripedTerminalView`。
- `SshTerminalPane.axaml.cs` 负责：
  - 键盘与文本输入
  - 复制粘贴
  - Resize
  - Quick Start overlay
  - 连接态 InfoBar
  - 把终端输出刷入 `TerminalHost.Terminal.Write(...)`
- `SshConnectionService.CreateTerminalSessionAsync(...)` 负责创建真实 SSH terminal session。
- `SkylarkTerminal.csproj` 当前直接引用 `src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork/Iciclecreek.Avalonia.Terminal.Fork.csproj`。

### 最近仓库状态

近期提交主要集中在 `RightSidebar` 和 `SFTP` 体验优化，说明“终端渲染层重构”目前适合独立拆分成一组新项目，而不应该继续在现有 UI 文件里叠加补丁：

- `38cf2fc docs: sync right sidebar bugfix3 implementation notes`
- `93ad620 feat: filter sftp content through visible items`
- `2cc17c9 refactor: replace sftp popup actions with fluent menus`

## 目标

1. 将终端技术栈从当前弱生态控件迁移到更可控、可演进的方案。
2. 尽量保留现有 SSH 会话、标签页、分屏、Quick Start、状态栏与右键菜单逻辑。
3. 避免 `WebView` 带来的额外进程/内存/输入法/焦点/合成层复杂度。
4. 保持整体视觉是“纯 Avalonia 宿主下的 Fluent 桌面终端”，而不是“桌面壳包了一层网页终端”。
5. 在迁移过程中保留明确回滚路径，不做一次性 Big Bang 替换。

## 非目标

1. 本轮不接入 WezTerm 全项目，也不引入 Rust runtime。
2. 本轮不重做 SSH 连接模型，不改 `SSH.NET` 的业务接入方式。
3. 本轮不删除现有 `Iciclecreek` 路线；旧后端应保留一段时间作为 fallback。
4. 本轮不扩展云同步、session recording、terminal multiplexer 等高级特性。

## 关键调研结论

### 关于 `WebView + xterm.js`

它不是“糟糕”的技术选型。它的优点非常明确：

- 生态最成熟
- ANSI/DEC 行为与兼容性最好
- 资料、issue、插件和竞品实践最多

但它对本项目的缺点同样明确：

- 会引入独立浏览器宿主与额外内存占用
- IME、焦点、拖拽、右键菜单、透明层级、DPI 与 Avalonia 合成之间会产生边界问题
- 视觉一致性要靠 Web/CSS 和 Avalonia 双栈协同维护
- 终端与桌面控件的交互边界变多，调试复杂度上升

结论：`xterm.js` 是“主流且靠谱”，不是“差”，但它不符合本项目“纯桌面 Fluent 终端控件”的偏好。

### 关于 `XtermSharp`

`XtermSharp` 的历史影响力确实比 `Iciclecreek.Avalonia.Terminal` 更高，但它仍然存在几个问题：

- 主要价值在 parser / emulator 层，不是专为 Avalonia 定制的现代桌面控件方案
- 当前社区热度、版本节奏和长期演进确定性都不够强
- 引入后仍然要自己补大量渲染、输入、选择、clipboard、resize 和高 DPI 适配工作

结论：相比当前 fork，它不构成足够强的“长期主路线”。

### 关于 `emacs-libvterm`

`emacs-libvterm` 的价值不在于“拿来直接复用”，而在于它验证了一种成熟架构：

- `libvterm` 负责 terminal core
- 原生 bridge 负责宿主 ABI 边界
- 上层 host UI 自己实现渲染与交互

这正好对应本项目想走的结构：

- `libvterm` / native shim
- C# interop
- Avalonia renderer/control

注意事项：

- 它是架构参考，不是直接依赖目标
- 它的 GPL 约束意味着不能把它当作可直接复制的宿主层实现来源

## 方案对比

### 设计点 A：终端核心路线

#### 方案 A1：继续沿用 `Iciclecreek`，并自行重构 Parsing / Rendering

优点：

- 可以最大化复用现有 `SshTerminalPane`
- 不引入 native build 体系
- 短期改造成本最低

缺点：

- 依旧站在弱生态基础上演进
- 终端兼容性、渲染正确性和 escape sequence 行为最终仍要自己扛
- AI 可以辅助重构，但无法替代“长期有验证的 terminal core”

结论：适合临时止血，不适合做项目主路线。

#### 方案 A2：切到 `WebView + xterm.js`

优点：

- 行业主流
- 兼容性最好
- 最容易快速接近 Termius / Tabby 的行为一致性

缺点：

- 引入浏览器宿主
- 内存和合成层成本更高
- 与 Fluent 桌面 UI 的交互边界增多

结论：如果目标是“最快达到高兼容终端”，它是保守且稳妥的路线；如果目标是“纯桌面 Avalonia 终端控件”，它不是最佳审美与架构匹配。

#### 方案 A3：采用 `libvterm native core + C# interop + Avalonia renderer`

优点：

- terminal core 使用成熟原生库，行为正确性高于自研 parser
- 渲染层与交互层仍由 Avalonia 自控
- 没有 WebView 额外宿主成本
- 长期可演进为项目自有终端控件能力

缺点：

- 需要建立 native build、ABI、P/Invoke 与 packaging 体系
- Unicode、宽字符、selection、scrollback、IME、dirty region 等都要自己打磨
- 首次落地复杂度显著高于 WebView

**专家推荐：A3**

理由：它兼顾“长期可控性”和“纯桌面体验”，技术难度高，但方向正确；本项目已经具备较完整的宿主 UI 和会话层，值得把难度集中投入在一个可长期复用的终端底座上。

### 设计点 B：上游源码管理方式

#### 方案 B1：以 Git submodule 引入 `libvterm`

优点：

- upstream 边界清晰
- 更新来源明确

缺点：

- 对日常开发、CI、克隆体验更差
- 子模块状态容易漂移
- 不利于“带少量 shim 与补丁一起管理”

#### 方案 B2：在仓库内 vendored snapshot 管理 upstream 源码

优点：

- 版本固定、仓库自洽
- 更适合和自定义 native shim 一起维护
- 对团队和 CI 更友好

缺点：

- 需要自己记录 upstream version / license / patch note

**专家推荐：B2**

结论：本仓库先不要上 Git submodule；采用 repo 内 vendored snapshot 更稳。

### 设计点 C：C# 与 `libvterm` 的边界

#### 方案 C1：C# 直接 `P/Invoke` raw `libvterm` API

优点：

- 理论上层数最少

缺点：

- 原生结构体、回调、内存所有权、位字段和 ABI 细节会直接泄露到 C#
- 调试和跨平台兼容性更差

#### 方案 C2：先写一层 native shim，再由 C# 调 shim

优点：

- 可以把 ABI 稳定成“专为 C# interop 设计”的接口
- 方便封装 dirty row、screen cell、cursor state、resize、mouse / key 模式等数据
- 更利于以后替换底层 terminal core

缺点：

- 需要自己维护一小层 C bridge

**专家推荐：C2**

结论：不要让 C# 直接面对 raw `libvterm`；应当由 native shim 稳定边界。

### 设计点 D：项目拆分粒度

#### 方案 D1：2 个项目

- `Native+Interop`
- `Avalonia`

优点：

- 项目数少

缺点：

- 原生 ABI、托管 interop、UI 渲染职责混杂
- 不利于单独测试和以后复用

#### 方案 D2：3 个项目

- `SkylarkTerminal.Terminal.Native`
- `SkylarkTerminal.Terminal.Interop`
- `SkylarkTerminal.Terminal.Avalonia`

优点：

- 职责清晰
- 便于独立测试
- 更适合 staged rollout 和后续演化

缺点：

- 初始工程组织更复杂

**专家推荐：D2**

结论：采用 3 子项目拆分。

### 设计点 E：迁移策略

#### 方案 E1：直接替换 `SshTerminalPane`

优点：

- 路径最短

缺点：

- 风险最高
- 出问题时没有低成本回滚点

#### 方案 E2：引入 backend adapter，分阶段切换

优点：

- 可保留 `LegacyIciclecreek` fallback
- 便于逐步验证输入、渲染、性能和发布包
- 更适合当前仓库已经存在大量 UI 宿主逻辑的情况

缺点：

- 迁移期会同时维护两条 terminal backend

**专家推荐：E2**

结论：先做 adapter 和 feature flag，再逐步切换默认后端。

## 最终决策

本轮设计最终确定为：

1. 不走 `WebView + xterm.js` 主路线。
2. 不继续押注 `Iciclecreek` 或 `XtermSharp` 作为长期核心。
3. 采用 `libvterm native core + native shim + C# interop + Avalonia renderer/control`。
4. 在仓库内新增 3 个子项目：
   - `src/SkylarkTerminal.Terminal.Native`
   - `src/SkylarkTerminal.Terminal.Interop`
   - `src/SkylarkTerminal.Terminal.Avalonia`
5. 先不引入 Git submodule，采用 vendored upstream snapshot。
6. 保留现有 `Iciclecreek` 路线作为 rollback backend，直到新后端通过完整验证。

## 子项目设计

### 1. `SkylarkTerminal.Terminal.Native`

职责：

- 管理 vendored `libvterm` 源码
- 提供 native shim
- 负责本地构建脚本、RID 产物与 license metadata

建议目录：

```text
src/SkylarkTerminal.Terminal.Native/
  SkylarkTerminal.Terminal.Native.csproj
  vendor/libvterm/
  native/include/skylark_terminal_bridge.h
  native/src/skylark_terminal_bridge.c
  build/BuildNative.ps1
  build/build-native.sh
  README.md
```

输出：

- `skylark_terminal.dll` 或等价命名的 native library

### 2. `SkylarkTerminal.Terminal.Interop`

职责：

- `DllImport` / `LibraryImport`
- `SafeHandle`
- `TerminalEngine`
- managed screen buffer / cursor state / dirty row projection
- 负责把 native 回调整理成稳定的 C# API

建议目录：

```text
src/SkylarkTerminal.Terminal.Interop/
  SkylarkTerminal.Terminal.Interop.csproj
  Native/SkylarkTerminalNative.cs
  Native/VTermHandle.cs
  Model/TerminalCell.cs
  Model/TerminalCursorState.cs
  Model/TerminalScreenSnapshot.cs
  Engine/TerminalEngine.cs
  Engine/TerminalChangeSet.cs
```

### 3. `SkylarkTerminal.Terminal.Avalonia`

职责：

- Avalonia 终端控件
- glyph layout、selection、cursor、scrollback、clipboard、IME / key translation
- 把 interop 层暴露的 snapshot / dirty region 转成高效重绘

建议目录：

```text
src/SkylarkTerminal.Terminal.Avalonia/
  SkylarkTerminal.Terminal.Avalonia.csproj
  Controls/LibvtermTerminalView.cs
  Input/TerminalKeyEncoder.cs
  Rendering/TerminalGlyphRunCache.cs
  Rendering/TerminalRenderState.cs
  Selection/TerminalSelectionModel.cs
```

## 应用层集成设计

### 可直接复用的部分

- `ConnectionConfig`
- `ISshTerminalSession`
- `SshConnectionService`
- `SessionRegistryService`
- `WorkspaceTabItemViewModel`
- `QuickStartOverlay`
- `InfoBar`、右键菜单外壳、标签拖拽分屏系统

### 必须替换或重构的部分

- `RowStripedTerminalView`
- 当前对 `TerminalHost.Terminal.Write(...)` 的直接输出灌入
- 当前 code-behind 中与具体控件耦合的 selection / copy / clear / resize 逻辑
- 当前键盘输入与 terminal mode 的局部处理逻辑

### 建议集成方式

应用层新增一个 backend adapter，而不是让 `SshTerminalPane` 直接绑定某个具体控件类型：

```text
SshTerminalPane
  -> ITerminalViewHostFactory
  -> ITerminalViewHost
  -> LibvtermTerminalView / LegacyIciclecreekTerminalView

ISshTerminalSession.OutputReceived
  -> ITerminalViewHost.FeedOutput(...)

LibvtermTerminalView.SendRequested
  -> SshTerminalPane
  -> ISshTerminalSession.SendAsync(...)
```

这样做的好处：

- `QuickStartOverlay` 和连接状态逻辑不必推倒重来
- backend 可以平滑切换
- 回滚成本低

## 为什么不是“全部逻辑都重来”

不是。

如果走 `libvterm` 路线，真正要重写的是“terminal component stack”：

- parser / emulator 接入边界
- screen buffer projection
- renderer
- selection / clipboard / input encoder

但下面这些逻辑大多仍然能保留：

- SSH 连接与会话生命周期
- tab / split / workspace
- Quick Start
- 连接异常提示
- 右键菜单命令语义
- 当前应用的 DI / MVVM 外壳

所以这是“终端内核与控件层重构”，不是“整个 SSH 客户端逻辑推倒重来”。

## 风险

1. Native build 与 RID packaging 复杂度上升，尤其是 `win-x64` 发布链路。
2. `PublishTrimmed=true` 下需要确认 interop 入口和 native asset 不被错误裁剪。
3. Unicode、双宽字符、combining characters、emoji、alt screen、scroll region 都可能出现细节 bug。
4. Avalonia 文本测量与 glyph cache 如果做得不好，会导致重绘抖动和 CPU 占用偏高。
5. IME、右键菜单、剪贴板、鼠标选择与 bracketed paste 都要重新过一遍交互边界。
6. `emacs-libvterm` 只能做架构参考，不能作为可直接搬运的宿主代码来源。

## 风险缓解

1. 第一阶段先支持 `win-x64`，不要一开始追求所有 RID。
2. 保留 `LegacyIciclecreek` backend，直到 libvterm 路线通过手工验收和发布验证。
3. 用 native shim 固定 ABI，避免 C# 直接绑定 raw `libvterm`。
4. 先完成“可用 + 正确”，再做更激进的 render optimization。
5. 在 `tests/SkylarkTerminal.Tests` 中优先覆盖：
   - resize
   - wide char
   - cursor movement
   - selection text extraction
   - key encoding
   - fallback backend selection

## 回滚方案

1. 在应用层保留 `TerminalBackendKind.LegacyIciclecreek`。
2. 默认后端切换前，新旧 backend 并存。
3. 如果 `libvterm` 方案在发布包、内存或输入法行为上出现不可接受问题，可以只改 DI 配置或 feature flag，回退到旧 backend。
4. 旧 fork 的删除应单独作为后续里程碑，不纳入本轮首批实施。

## 验证清单

### 功能正确性

- 可以正常打开 SSH tab 并连接
- 普通文本输入、Enter、Backspace、Ctrl+C、Ctrl+L、方向键、Home/End 正常
- 终端 resize 后远端 TTY 大小同步
- copy / paste / clear screen / reconnect 正常
- `vim` / `less` / `top` 等 alt screen 程序至少具备基础可用性

### 渲染与体验

- 字体、行高、光标、selection 与当前 Fluent 视觉一致
- Quick Start overlay 与连接态 overlay 不受影响
- split pane、tab drag、窗口缩放时无明显闪烁
- 大量输出场景无明显卡顿或输入延迟

### 发布与运维

- `dotnet build SkylarkTerminal.slnx` 正常
- `win-x64` 发布产物包含 native library
- trimming 后不丢失 interop 必需入口
- fallback backend 可以通过配置切回

## 结论

对于当前项目，最合理的路线不是继续缝补 `Iciclecreek`，也不是直接退回 `WebView + xterm.js`，而是建立一套真正可控的终端底座：

- `libvterm` 负责成熟 terminal core
- native shim 负责稳定 ABI
- C# interop 负责托管边界
- Avalonia control 负责最终 Fluent 桌面体验

这条路线的初期投入更大，但它是唯一同时满足“纯桌面感、长期可控性、避免 WebView、保留现有 SSH 业务层”的方案。
