using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using System.Collections.Generic;
using System.Linq;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockAssetCatalogService : IAssetCatalogService
{
    public Dictionary<AssetsPaneKind, List<AssetNode>> GetAssets()
    {
        var assets = new Dictionary<AssetsPaneKind, List<AssetNode>>
        {
            [AssetsPaneKind.Hosts] =
            [
                new FolderNode(
                    "hosts-prod",
                    "Production",
                    "Environment",
                    [
                        new ConnectionNode("host-core-gw", "core-gateway", "10.32.0.21", "ops", 22),
                        new ConnectionNode("host-bastion", "bastion-jump", "10.32.0.10", "admin", 22),
                    ]),
                new FolderNode(
                    "hosts-stage",
                    "Staging",
                    "Environment",
                    [
                        new ConnectionNode("host-stage-api", "stage-api", "10.12.3.48", "devops", 2202),
                        new ConnectionNode("host-stage-db", "stage-db", "10.12.3.52", "dba", 22),
                    ]),
                new FolderNode(
                    "hosts-shared",
                    "Shared Services",
                    "Environment",
                    [
                        new FolderNode(
                            "hosts-shared-obs",
                            "Observability",
                            "Group",
                            [
                                new ConnectionNode("host-prometheus", "prometheus", "10.7.0.31", "monitor", 22),
                                new ConnectionNode("host-grafana", "grafana", "10.7.0.41", "monitor", 22),
                            ]),
                    ]),
            ],
            [AssetsPaneKind.Sftp] =
            [
                new FolderNode(
                    "sftp-prod",
                    "prod-files",
                    "SFTP Root",
                    [
                        new ConnectionNode("sftp-prod-www", "web-root", "10.32.0.21", "deploy", 22, "SFTP Connection"),
                        new FolderNode(
                            "sftp-prod-archive",
                            "archive",
                            "Directory",
                            [
                                new ConnectionNode("sftp-archive-cold", "cold-storage", "10.32.0.38", "backup", 22, "SFTP Connection"),
                            ]),
                    ]),
                new FolderNode(
                    "sftp-stage",
                    "stage-files",
                    "SFTP Root",
                    [
                        new ConnectionNode("sftp-stage-assets", "stage-assets", "10.12.3.48", "deploy", 22, "SFTP Connection"),
                    ]),
            ],
            [AssetsPaneKind.Keys] =
            [
                new FolderNode(
                    "keys-ssh",
                    "SSH Keys",
                    "Vault",
                    [
                        new ConnectionNode("key-rsa", "id_rsa_prod", "vault-prod", "security", 443, "PrivateKey"),
                        new ConnectionNode("key-ed", "id_ed25519_stage", "vault-stage", "security", 443, "PrivateKey"),
                        new ConnectionNode("key-cert", "ops-cert.pub", "pki-center", "security", 443, "PublicKey"),
                    ]),
            ],
            [AssetsPaneKind.Tools] =
            [
                new FolderNode(
                    "tool-automation",
                    "Automation",
                    "Toolbox",
                    [
                        new ConnectionNode("tool-snp", "Deploy Snippets", "snippet-host", "tooling", 22, "SnippetGroup"),
                        new ConnectionNode("tool-his", "Command History", "history-host", "tooling", 22, "History"),
                    ]),
            ],
        };

        var hostCount = assets[AssetsPaneKind.Hosts]
            .SelectMany(Flatten)
            .OfType<ConnectionNode>()
            .Count();
        RuntimeLogger.Info("assets", $"Loaded mock assets. hosts={hostCount}");
        return assets;
    }

    private static IEnumerable<AssetNode> Flatten(AssetNode node)
    {
        yield return node;
        foreach (var child in node.Children.SelectMany(Flatten))
        {
            yield return child;
        }
    }
}
