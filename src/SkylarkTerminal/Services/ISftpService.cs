using SkylarkTerminal.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface ISftpService
{
    Task<List<RemoteFileNode>> ListDirectoryAsync(string connectionId, string path);
}
