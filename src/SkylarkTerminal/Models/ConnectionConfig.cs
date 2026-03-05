namespace SkylarkTerminal.Models;

public sealed class ConnectionConfig
{
    public string ConnectionId { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 22;

    public string Username { get; init; } = string.Empty;

    public string? Password { get; init; }

    public string? PrivateKeyPath { get; init; }
}
