using SkylarkTerminal.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface ISshConnectionService
{
    Task<bool> ConnectAsync(ConnectionConfig config);

    Task DisconnectAsync(string connectionId);

    Task RunCommandAsync(string connectionId, string command);

    Task<ISshTerminalSession> CreateTerminalSessionAsync(
        ConnectionConfig config,
        CancellationToken cancellationToken = default);
}
