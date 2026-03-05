using System.Collections.Generic;

namespace SkylarkTerminal.Models;

public sealed class FolderNode : AssetNode
{
    public FolderNode(
        string id,
        string name,
        string kind = "Folder",
        IEnumerable<AssetNode>? children = null)
        : base(id, name, kind, children)
    {
    }
}
