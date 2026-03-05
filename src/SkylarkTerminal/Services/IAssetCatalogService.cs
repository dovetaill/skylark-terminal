using SkylarkTerminal.Models;
using System.Collections.Generic;

namespace SkylarkTerminal.Services;

public interface IAssetCatalogService
{
    Dictionary<AssetsPaneKind, List<AssetNode>> GetAssets();
}
