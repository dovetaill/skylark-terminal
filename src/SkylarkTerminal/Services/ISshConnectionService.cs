using SkylarkTerminal.Models;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface ISshConnectionService
{
    Task<bool> ConnectAsync(ConnectionConfig config);

    Task DisconnectAsync(string connectionId);

    Task RunCommandAsync(string connectionId, string command);
}
