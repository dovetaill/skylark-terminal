using SkylarkTerminal.Models;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockSshConnectionService : ISshConnectionService
{
    public Task<bool> ConnectAsync(ConnectionConfig config)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(config.Host));
    }

    public Task DisconnectAsync(string connectionId)
    {
        return Task.CompletedTask;
    }

    public Task RunCommandAsync(string connectionId, string command)
    {
        return Task.CompletedTask;
    }
}
