using System;

namespace SkylarkTerminal.Models;

public sealed class QuickStartRecentConnection
{
    public QuickStartRecentConnection(
        string assetId,
        string displayName,
        string host,
        string username,
        int port,
        DateTimeOffset lastUsedAt)
    {
        AssetId = assetId;
        DisplayName = displayName;
        Host = host;
        Username = username;
        Port = port;
        LastUsedAt = lastUsedAt;
    }

    public string AssetId { get; }

    public string DisplayName { get; }

    public string Host { get; }

    public string Username { get; }

    public int Port { get; }

    public DateTimeOffset LastUsedAt { get; }

    public string EndpointLabel => $"{Username}@{Host}:{Port}";

    public string LastUsedLabel => $"最近使用 {LastUsedAt:MM-dd HH:mm}";

    public bool Matches(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               Host.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               Username.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               Port.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
