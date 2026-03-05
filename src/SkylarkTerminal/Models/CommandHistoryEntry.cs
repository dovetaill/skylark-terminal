using System;

namespace SkylarkTerminal.Models;

public sealed class CommandHistoryEntry
{
    public DateTime Timestamp { get; init; }

    public string Command { get; init; } = string.Empty;
}
