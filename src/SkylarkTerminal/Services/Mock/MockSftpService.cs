using SkylarkTerminal.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockSftpService : ISftpService
{
    public Task<List<RemoteFileNode>> ListDirectoryAsync(string connectionId, string path)
    {
        var nodes = new List<RemoteFileNode>
        {
            new()
            {
                Name = "logs",
                FullPath = $"{path.TrimEnd('/')}/logs",
                IsDirectory = true,
                Size = 0,
            },
            new()
            {
                Name = "deploy.sh",
                FullPath = $"{path.TrimEnd('/')}/deploy.sh",
                IsDirectory = false,
                Size = 1024,
            },
            new()
            {
                Name = ".env",
                FullPath = $"{path.TrimEnd('/')}/.env",
                IsDirectory = false,
                Size = 128,
                IsHidden = true,
            },
        };

        return Task.FromResult(nodes);
    }
}
