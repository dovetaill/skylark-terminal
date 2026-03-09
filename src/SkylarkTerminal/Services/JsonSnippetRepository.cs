using SkylarkTerminal.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public sealed class JsonSnippetRepository : ISnippetRepository
{
    private readonly string filePath;

    public JsonSnippetRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        this.filePath = filePath;
    }

    public async Task<IReadOnlyList<SnippetCategory>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var document = await JsonSerializer.DeserializeAsync(
                stream,
                SnippetStoreJsonContext.Default.SnippetStoreDocument,
                cancellationToken).ConfigureAwait(false);

            return document?.ToRuntimeModel() ?? [];
        }
        catch
        {
            File.Move(filePath, filePath + ".broken", true);
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyList<SnippetCategory> categories, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                SnippetStoreDocument.FromRuntime(categories),
                SnippetStoreJsonContext.Default.SnippetStoreDocument,
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, filePath, true);
    }
}
