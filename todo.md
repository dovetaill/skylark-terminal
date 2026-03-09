# TODO

## libvterm terminal migration

- [ ] Add `SkylarkTerminal.Terminal.Native` project and wire it into `SkylarkTerminal.slnx`
- [ ] Add `SkylarkTerminal.Terminal.Interop` project and define managed interop boundary
- [ ] Add `SkylarkTerminal.Terminal.Avalonia` project and define terminal control surface
- [ ] Introduce `TerminalBackendKind`, `TerminalBackendOptions`, `ITerminalViewHost`, and `ITerminalViewHostFactory`
- [ ] Vendor `libvterm` source into repo and record upstream version/license
- [ ] Build native shim layer for stable C ABI instead of binding raw `libvterm` directly
- [ ] Add native build scripts for `win-x64`
- [ ] Implement `SafeHandle`, native bindings, and `TerminalEngine`
- [ ] Implement terminal snapshot, dirty-row tracking, and cursor state models
- [ ] Implement Avalonia terminal rendering, selection, clipboard, and key encoding
- [ ] Add backend host factory and keep `LegacyIciclecreek` fallback during migration
- [ ] Refactor `SshTerminalPane` to use backend-neutral terminal host container
- [ ] Verify resize, copy/paste, reconnect, alt-screen apps, and split-pane behavior
- [ ] Validate `dotnet test`, `dotnet build`, and native asset packaging before switching default backend

## docs

- [ ] Keep [docs/plans/2026-03-09-libvterm-terminal-design.md](/home/wwwroot/skylark-terminal/docs/plans/2026-03-09-libvterm-terminal-design.md) in sync with implementation decisions
- [ ] Keep [docs/plans/2026-03-09-libvterm-terminal-implementation-plan.md](/home/wwwroot/skylark-terminal/docs/plans/2026-03-09-libvterm-terminal-implementation-plan.md) in sync with task progress
