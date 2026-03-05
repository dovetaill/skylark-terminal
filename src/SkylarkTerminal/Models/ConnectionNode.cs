using System;

namespace SkylarkTerminal.Models;

public sealed class ConnectionNode : AssetNode
{
    public ConnectionNode(
        string id,
        string name,
        string host,
        string user,
        int port = 22,
        string kind = "SSH Connection")
        : base(id, name, kind)
    {
        Host = host;
        User = user;
        Port = port;
    }

    public string Host { get; }

    public string User { get; }

    public int Port { get; }

    public override bool Matches(string keyword)
    {
        if (base.Matches(keyword))
        {
            return true;
        }

        return Host.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               User.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               Port.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
