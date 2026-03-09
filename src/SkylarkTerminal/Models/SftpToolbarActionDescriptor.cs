using CommunityToolkit.Mvvm.Input;

namespace SkylarkTerminal.Models;

public sealed record SftpToolbarActionDescriptor(
    string Id,
    string Glyph,
    string LabelZh,
    string TooltipZh,
    IRelayCommand Command);
