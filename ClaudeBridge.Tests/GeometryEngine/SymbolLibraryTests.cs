using ClaudeBridge.Core.Bridge.Models;
using ClaudeBridge.GeometryEngine;

namespace ClaudeBridge.Tests.GeometryEngine;

public class SymbolLibraryTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"symbol-library-{Guid.NewGuid():N}.json");

    private static GeometrySignature SampleSignature() =>
        ShapeSignature.Compute(SyntheticSymbols.CameraLike("s", new Point2D(0, 0)));

    [Fact]
    public async Task SaveAndList_SessionScope_IsVisibleWithoutTouchingDisk()
    {
        var library = new SymbolLibrary(personalLibraryFilePath: _tempFile);

        await library.SaveAsync(new SaveSignatureAsCategoryInput(SampleSignature(), "telecamera", "session"));
        var result = await library.ListAsync();

        var category = Assert.Single(result.Categories);
        Assert.Equal("telecamera", category.Label);
        Assert.Equal("session", category.Scope);
        Assert.False(File.Exists(_tempFile));
    }

    [Fact]
    public async Task SaveAndList_PersonalLibraryScope_PersistsToFile()
    {
        var library = new SymbolLibrary(personalLibraryFilePath: _tempFile);

        await library.SaveAsync(new SaveSignatureAsCategoryInput(SampleSignature(), "telecamera", "personal_library"));

        Assert.True(File.Exists(_tempFile));

        // Una nuova istanza (es. una sessione futura) deve ritrovare la categoria salvata su file.
        var reopened = new SymbolLibrary(personalLibraryFilePath: _tempFile);
        var result = await reopened.ListAsync();

        var category = Assert.Single(result.Categories);
        Assert.Equal("telecamera", category.Label);
        Assert.Equal("personal_library", category.Scope);
    }

    [Fact]
    public async Task FindSignaturesByLabel_ReturnsMostRecentFirst()
    {
        var library = new SymbolLibrary(personalLibraryFilePath: _tempFile);
        var older = new SaveSignatureAsCategoryInput(SampleSignature(), "telecamera", "personal_library");
        var newer = new SaveSignatureAsCategoryInput(SampleSignature(), "telecamera", "personal_library");

        await library.SaveAsync(older, DateTimeOffset.UtcNow.AddDays(-1), CancellationToken.None);
        await library.SaveAsync(newer, DateTimeOffset.UtcNow, CancellationToken.None);

        var signatures = await library.FindSignaturesByLabelAsync("telecamera");

        Assert.Equal(2, signatures.Count);
    }

    [Fact]
    public async Task FindSignaturesByLabel_UnknownLabel_ReturnsEmpty()
    {
        var library = new SymbolLibrary(personalLibraryFilePath: _tempFile);
        await library.SaveAsync(new SaveSignatureAsCategoryInput(SampleSignature(), "telecamera", "personal_library"));

        var signatures = await library.FindSignaturesByLabelAsync("estintore");

        Assert.Empty(signatures);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}
