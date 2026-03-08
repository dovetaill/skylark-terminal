using CommunityToolkit.Mvvm.Input;

namespace SkylarkTerminal.Models;

public sealed record ModeActionDescriptor(
    string Id,
    string Glyph,
    string Tooltip,
    IRelayCommand Command,
    bool IsToggle = false);
