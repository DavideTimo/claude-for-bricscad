using System.Text.Json;
using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.Core.Json;

namespace ClaudeBridge.GeometryEngine;

/// <summary>
/// Libreria di categorie geometriche apprese (Tool Contract, save_signature_as_category /
/// list_learned_categories). Scope "session": solo in memoria, persa a fine processo. Scope
/// "personal_library": persistita su file JSON locale, riusabile tra sessioni e disegni diversi
/// — ma sempre da trattare come ipotesi, mai come certezza (Tool Contract, §6quinquies).
/// </summary>
public sealed class SymbolLibrary(string? personalLibraryFilePath = null)
{
    private readonly List<StoredCategory> _session = [];
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public Task<SaveSignatureAsCategoryOutput> SaveAsync(
        SaveSignatureAsCategoryInput input, CancellationToken ct = default) =>
        SaveAsync(input, DateTimeOffset.UtcNow, ct);

    internal async Task<SaveSignatureAsCategoryOutput> SaveAsync(
        SaveSignatureAsCategoryInput input, DateTimeOffset createdAt, CancellationToken ct)
    {
        var entry = new StoredCategory(input.Signature, input.Label, input.Scope, createdAt);

        if (input.Scope == "personal_library")
        {
            await AppendToPersonalLibraryAsync(entry, ct).ConfigureAwait(false);
        }
        else
        {
            _session.Add(entry);
        }

        return new SaveSignatureAsCategoryOutput(Saved: true, input.Label, input.Scope);
    }

    public async Task<ListLearnedCategoriesOutput> ListAsync(CancellationToken ct = default)
    {
        var persisted = await ReadPersonalLibraryAsync(ct).ConfigureAwait(false);
        var categories = _session
            .Concat(persisted)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new LearnedCategory(c.Label, c.Scope, c.CreatedAt.ToString("O")))
            .ToList();

        return new ListLearnedCategoriesOutput(categories);
    }

    /// <summary>
    /// Espone le firme grezze salvate per un'etichetta, più recenti prima — utile al bridge per
    /// riproporre una categoria appresa come ipotesi di partenza (Riconoscimento simboli, §2,
    /// passo 4). Non fa parte del Tool Contract: è un dettaglio interno di implementazione.
    /// </summary>
    public async Task<IReadOnlyList<GeometrySignature>> FindSignaturesByLabelAsync(
        string label, CancellationToken ct = default)
    {
        var persisted = await ReadPersonalLibraryAsync(ct).ConfigureAwait(false);
        return _session
            .Concat(persisted)
            .Where(c => c.Label == label)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.Signature)
            .ToList();
    }

    private async Task AppendToPersonalLibraryAsync(StoredCategory entry, CancellationToken ct)
    {
        if (personalLibraryFilePath is null)
        {
            _session.Add(entry);
            return;
        }

        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var existing = await ReadPersonalLibraryFileAsync(ct).ConfigureAwait(false);
            existing.Add(entry);
            await using var stream = File.Create(personalLibraryFilePath);
            await JsonSerializer.SerializeAsync(stream, existing, ToolJsonOptions.Default, ct).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<StoredCategory>> ReadPersonalLibraryAsync(CancellationToken ct)
    {
        if (personalLibraryFilePath is null)
        {
            return [];
        }

        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ReadPersonalLibraryFileAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<StoredCategory>> ReadPersonalLibraryFileAsync(CancellationToken ct)
    {
        if (personalLibraryFilePath is null || !File.Exists(personalLibraryFilePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(personalLibraryFilePath);
        var loaded = await JsonSerializer.DeserializeAsync<List<StoredCategory>>(stream, ToolJsonOptions.Default, ct)
            .ConfigureAwait(false);
        return loaded ?? [];
    }

    private sealed record StoredCategory(GeometrySignature Signature, string Label, string Scope, DateTimeOffset CreatedAt);
}
