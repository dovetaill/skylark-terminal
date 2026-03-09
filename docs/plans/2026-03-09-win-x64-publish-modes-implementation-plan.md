# Win-x64 Publish Modes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 为 Windows x64 增加独立的调试发布脚本，并让 `Release` 不再生成 fork 终端项目的 PDB。

**Architecture:** 继续保留脚本驱动的发布方式。项目层负责控制 `Release` 与 `Debug` 的符号策略；脚本层负责分别产出发布版和调试版可运行目录与 zip。

**Tech Stack:** `bash`, `.NET 10`, `dotnet publish`

---

### Task 1: 禁用 fork 项目的 Release 符号输出

**Files:**
- Modify: `src/ThirdParty/Iciclecreek.Avalonia.Terminal.Fork/Iciclecreek.Avalonia.Terminal.Fork.csproj`

**Steps:**
1. 为 `Release` 增加条件化 `PropertyGroup`。
2. 设置 `DebugSymbols=false` 与 `DebugType=none`。
3. 保持 `Debug` 配置不变。

### Task 2: 新增调试发布脚本

**Files:**
- Create: `build-win-x64-debug.sh`

**Steps:**
1. 复制现有发布脚本结构。
2. 将 `CONFIG` 固定为 `Debug`。
3. 将根目录压缩包名设为 `publish-debug.zip`，避免覆盖 `publish.zip`。
4. 保留 `--no-single-file` 行为，保持调用方式一致。

### Task 3: 验证发布结果

**Files:**
- Verify only

**Steps:**
1. 运行 `./build-win-x64.sh`。
2. 确认 `src/SkylarkTerminal/bin/Release/net10.0/win-x64/publish` 中无 `Iciclecreek.Avalonia.Terminal.Fork.pdb`。
3. 运行 `./build-win-x64-debug.sh`。
4. 确认 `src/SkylarkTerminal/bin/Debug/net10.0/win-x64/publish` 中有 `Iciclecreek.Avalonia.Terminal.Fork.pdb`。
5. 确认根目录同时存在 `publish.zip` 与 `publish-debug.zip`。
