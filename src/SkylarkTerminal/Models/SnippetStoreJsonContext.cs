using System.Text.Json.Serialization;

namespace SkylarkTerminal.Models;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SnippetStoreDocument))]
internal partial class SnippetStoreJsonContext : JsonSerializerContext
{
}
