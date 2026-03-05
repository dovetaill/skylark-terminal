using SkylarkTerminal.Models;
using System.Collections.Generic;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockAssetCatalogService : IAssetCatalogService
{
    public Dictionary<AssetsPaneKind, List<AssetNode>> GetAssets()
    {
        return new Dictionary<AssetsPaneKind, List<AssetNode>>
        {
            [AssetsPaneKind.Hosts] =
            [
                new AssetNode(
                    "hosts-prod",
                    "Production",
                    "Group",
                    [
                        new AssetNode("host-core-gw", "core-gateway", "SSH Host"),
                        new AssetNode("host-bastion", "bastion-jump", "SSH Host"),
                    ]),
                new AssetNode(
                    "hosts-stage",
                    "Staging",
                    "Group",
                    [
                        new AssetNode("host-stage-api", "stage-api", "SSH Host"),
                        new AssetNode("host-stage-db", "stage-db", "SSH Host"),
                    ]),
            ],
            [AssetsPaneKind.Sftp] =
            [
                new AssetNode(
                    "sftp-prod",
                    "prod-files",
                    "SFTP Root",
                    [
                        new AssetNode("sftp-prod-www", "/var/www", "Directory"),
                        new AssetNode("sftp-prod-logs", "/var/log", "Directory"),
                    ]),
                new AssetNode("sftp-stage", "stage-files", "SFTP Root"),
            ],
            [AssetsPaneKind.Keys] =
            [
                new AssetNode("key-rsa", "id_rsa_prod", "PrivateKey"),
                new AssetNode("key-ed", "id_ed25519_stage", "PrivateKey"),
                new AssetNode("key-cert", "ops-cert.pub", "PublicKey"),
            ],
            [AssetsPaneKind.Tools] =
            [
                new AssetNode("tool-snp", "Deploy Snippets", "SnippetGroup"),
                new AssetNode("tool-his", "Command History", "History"),
            ],
        };
    }
}
