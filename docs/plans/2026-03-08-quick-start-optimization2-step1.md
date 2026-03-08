# Quick Start 起始页优化2 - 架构设计与技术方案确认（Step 1）

- 日期：2026-03-08
- 项目：`SkylarkTerminal`（Avalonia + FluentAvalonia）
- 阶段：纯方案确认（不改业务代码）
- 目标：完成 Quick Start 起始页优化2 的技术决策收敛，为后续实施提供单一执行基线

## 1. 任务背景与问题定义

本轮聚焦以下 4 个用户可见问题：

1. Quick Start 中“定位到 Hosts 资产”按钮体验无效。
2. Light 主题下终端会话区域仍呈暗色。
3. 终端行背景需要做相邻色分层（Dark/Light 都要成立）。
4. 终端字体希望具备 IDE 风格的多色高亮观感。

## 2. Step 1 信息收集结论

### 2.1 当前代码实现结论

1. “定位 Hosts”目前仅执行 `ShowHostsAssetsCommand`，只切 pane，不保证：
   - 左侧资产栏可见
   - 具体 Host 节点被选中
   - 树路径展开和滚动定位
2. Light 主题终端暗色的直接原因在 `ApplyShellPalette` 浅色分支：`ShellTerminalBrush` 仍为深色值（`#FF10141A`）。
3. 终端输出链路是 `session output -> queue -> Terminal.Write` 原样透传，未做本地行背景/语法着色策略。

### 2.2 版本与官方实践核验（截至 2026-03-08）

1. Avalonia 稳定线：`11.3.12`；预览线：`12.0.0-preview2`。
2. FluentAvaloniaUI NuGet 当前：`2.5.0`（项目已使用）。
3. Iciclecreek.Avalonia.Terminal 当前：`1.0.7`（项目已使用）。
4. FluentAvalonia 推荐在应用主题层开启系统主题/强调色跟随（`PreferSystemTheme` / `PreferUserAccentColor`）。

### 2.3 终端库可扩展性核验（本地反射）

1. `TerminalView` 非 sealed，`Render(DrawingContext)` 为可 override。
2. `Terminal.Buffer`、`TerminalBuffer`、`BufferLine`、`BufferCell` 公开可读，可支撑渲染级扩展。
3. 结论：本项目可采用“内部 fork + 渲染层扩展”路线。

### 2.4 参考素材状态

- `designs/Snipaste_2026-03-08_09-28-33.png` 存在并已用于现状判断。
- 任务描述中的 `Snipaste_2026-03-08_09-37-30.png` 当前仓库未找到（后续若补充可作为色板与层次对齐依据）。

## 3. 已确认技术决策（最终）

| 设计点 | 选择 |
|---|---|
| 1. Hosts 定位交互 | B：真实定位到 Host（展开路径 + 选中 + 滚动到可视区） |
| 2. 终端主题策略 | B：统一 Terminal token 色板体系 |
| 3. 行背景分层 | A：在终端渲染层实现相邻色行分层 |
| 4. 字体多色策略 | C：ANSI 优先 + 无 ANSI 时 fallback 本地高亮 |
| 5. 工程落地 | B：内部 fork（最小必要边界） |
| 6. fallback 粒度 | C：L1 全局基础规则 + L2 shell 感知增强 |
| 7. 色彩策略 | B：对比度约束 + 自动派生；视觉参考 C（偏 IDE 层次），总体贴近 Win11/HexHub |
| 8. 定位反馈 | B：目标 Host 节点短时高亮（约 1.2s）+ 保持选中 |
| 9. fork 边界 | B：`src/ThirdParty/...Fork` 最小子集 + 上游同步清单 |

## 4. 目标架构方案

## 4.1 Quick Start -> Hosts 真实定位链路

新增一条明确的 `LocateHostFromQuickStart` 交互链（命令名可在实施时最终定名）：

1. 保证 `IsAssetsPanelVisible = true`。
2. 切换 `SelectedAssetsPane = AssetsPaneKind.Hosts`。
3. 根据 Quick Start 上下文确定目标 Host：
   - 优先当前点击卡片对应连接
   - 次级为当前搜索结果首项
4. 在树形模式下展开祖先路径并选中目标节点。
5. 在平铺模式下设置选中并滚动到可视区。
6. 对目标节点触发 1.2s transient highlight（淡入淡出），保留最终选中态。

## 4.2 Terminal 统一 token 色板体系

将终端视觉资源统一收敛为 token（Dark/Light 双字典），至少包含：

- `Terminal.Background`
- `Terminal.Foreground.Primary`
- `Terminal.Foreground.Muted`
- `Terminal.RowStripe.Even`
- `Terminal.RowStripe.Odd`
- `Terminal.Syntax.*`（keyword/string/number/path/error/warn/success/comment 等）
- `Terminal.Selection`
- `Terminal.Cursor`

要求：

1. 所有终端相关 UI（Terminal 本体、Quick Start overlay、连接中 overlay）只消费 token，不再写死局部色值。
2. 主题切换只变 token，不改终端业务逻辑。

## 4.3 终端渲染层实现路线（内部 fork）

目录规划（规划稿，非当前改动）：

- `src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork/`
- `src/ThirdParty/.../UPSTREAM.md`（记录上游 commit/tag 与同步步骤）
- 主项目引用从 NuGet 切到本地项目引用（实施阶段执行）

改造原则：

1. 仅修改“渲染与着色相关”最小必要模块。
2. 保持公共 API 尽量兼容，降低上层改造面。
3. 避免改动 PTY/输入事件/会话管理路径。

## 4.4 ANSI 优先 + fallback 高亮

渲染优先级：

1. 若单元格已有 ANSI 颜色信息：直接按 ANSI 渲染（允许调色板 remap）。
2. 若无 ANSI：应用 fallback 规则。

fallback 分层：

1. `L1`（全局基础）：`command`、`path`、`ip/host`、`number`、`error/warn`。
2. `L2`（shell 感知增强）：仅在识别为 bash/zsh/pwsh 等 shell 语境时启用更细规则（变量、重定向、pipe、引号段等）。

## 4.5 色彩与可读性策略

视觉目标：Win11 Fluent 质感 + IDE 层次感，整体不透明。

约束：

1. Dark/Light 双模式都必须维持层次分明但不过饱和。
2. 自动对比度校验（文本 vs 背景），不达门槛时自动调明度/饱和度。
3. 行分层仅做“弱对比”邻近色，不抢内容前景。

## 5. 分阶段实施计划（执行导向）

1. Phase A：Quick Start 定位闭环
   - 完成 Hosts 真实定位 + 反馈高亮
   - 验证树形/平铺两视图一致性
2. Phase B：主题 token 收敛
   - 终端相关颜色全部改为 token 消费
   - 修复 Light 终端暗色问题
3. Phase C：内部 fork 接入
   - 引入最小 fork 边界与上游追踪文档
   - 建立可编译、可运行基线
4. Phase D：渲染增强
   - 行背景相邻色分层
   - ANSI remap + fallback L1/L2
5. Phase E：调色与验收
   - Dark/Light 双模式调色
   - 可读性与性能回归

## 6. 风险与缓解

1. 风险：fork 维护负担上升。
   - 缓解：限制改动面 + `UPSTREAM.md` 同步规范。
2. 风险：fallback 误判导致“错误高亮”。
   - 缓解：ANSI 优先；fallback 只在无 ANSI 时触发；支持快速关闭开关。
3. 风险：行分层影响 selection/cursor 可见性。
   - 缓解：selection/cursor 层级优先级高于 stripe，专门做对比测试。
4. 风险：Light 模式调色不稳定。
   - 缓解：token 单源 + 对比度自动校准。

## 7. 验收标准（对应原始 4 个诉求）

1. Quick Start “定位 Hosts”可稳定定位到目标 Host（含滚动、选中、短时高亮）。
2. Light 主题下终端背景与文本符合浅色视觉，不再出现暗底错配。
3. Dark/Light 模式下均可见行级相邻色分层，滚动与选择不闪烁不串色。
4. 终端文本呈现多色层次：
   - ANSI 输出保持正确
   - 无 ANSI 时出现可控的 IDE 风格 fallback 高亮
5. 关键交互无回归：输入、粘贴、复制、resize、session reconnect。

## 8. 非目标（本轮不做）

1. 不重构 SSH 会话核心协议栈。
2. 不引入透明壳或 Mica 混合渲染（维持不透明策略）。
3. 不在本轮引入多端（移动端）适配。

## 9. 参考来源

- Avalonia Releases: https://github.com/AvaloniaUI/Avalonia/releases
- Avalonia Theme Variants: https://docs.avaloniaui.net/docs/guides/styles-and-resources/how-to-use-theme-variants
- Avalonia `Application.RequestedThemeVariant`: https://api-docs.avaloniaui.net/docs/P_Avalonia_Application_RequestedThemeVariant
- FluentAvalonia README: https://github.com/amwx/FluentAvalonia/blob/master/README.md
- FluentAvalonia sample `App.axaml`: https://github.com/amwx/FluentAvalonia/blob/master/samples/FAControlsGallery/App.axaml
- NuGet `FluentAvaloniaUI`: https://www.nuget.org/packages/FluentAvaloniaUI
- NuGet `Iciclecreek.Avalonia.Terminal`: https://www.nuget.org/packages/Iciclecreek.Avalonia.Terminal

