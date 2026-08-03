using System.Text.Json;
using ClaudeBridge.Core.Tools;

namespace ClaudeBridge.Tests.Tools;

public class ToolSchemaValidatorTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    // Piano di Test §1, "Validazione schema": find_blocks — pattern valido → validazione passa.
    [Fact]
    public void FindBlocks_WithValidPattern_PassesValidation()
    {
        ToolCatalog.TryGet("find_blocks", out var spec);
        var input = Parse("""{ "name_pattern": "TELECAMERA*" }""");

        var exception = Record.Exception(() => ToolSchemaValidator.Validate(spec, input));

        Assert.Null(exception);
    }

    // Piano di Test §1: find_blocks — pattern vuoto passa comunque lo schema (è una stringa
    // valida): la semantica "vuoto = non valido" è responsabilità applicativa del bridge
    // (INVALID_PATTERN), non dello schema (Tool Contract, "Principio di validazione").
    [Fact]
    public void FindBlocks_WithEmptyPattern_StillPassesSchemaValidation()
    {
        ToolCatalog.TryGet("find_blocks", out var spec);
        var input = Parse("""{ "name_pattern": "" }""");

        var exception = Record.Exception(() => ToolSchemaValidator.Validate(spec, input));

        Assert.Null(exception);
    }

    [Fact]
    public void FindBlocks_MissingNamePattern_FailsValidation()
    {
        ToolCatalog.TryGet("find_blocks", out var spec);
        var input = Parse("""{ "layer": "IMPIANTI" }""");

        var ex = Assert.Throws<ToolSchemaValidationException>(() => ToolSchemaValidator.Validate(spec, input));

        Assert.Contains(ex.Errors, e => e.Contains("name_pattern"));
    }

    // Piano di Test §1: get_block_attributes — handle mancante nei parametri → errore di schema.
    [Fact]
    public void GetBlockAttributes_MissingHandle_FailsValidation()
    {
        ToolCatalog.TryGet("get_block_attributes", out var spec);
        var input = Parse("""{}""");

        Assert.Throws<ToolSchemaValidationException>(() => ToolSchemaValidator.Validate(spec, input));
    }

    // Piano di Test §1: run_lisp — chiamata senza campo "reason" → errore di schema.
    [Fact]
    public void RunLisp_MissingReason_FailsValidation()
    {
        ToolCatalog.TryGet("run_lisp", out var spec);
        var input = Parse("""{ "expression": "(command \"_ZOOM\" \"_E\")" }""");

        var ex = Assert.Throws<ToolSchemaValidationException>(() => ToolSchemaValidator.Validate(spec, input));

        Assert.Contains(ex.Errors, e => e.Contains("reason"));
    }

    [Fact]
    public void RunLisp_WithExpressionAndReason_PassesValidation()
    {
        ToolCatalog.TryGet("run_lisp", out var spec);
        var input = Parse("""{ "expression": "(command \"_ZOOM\" \"_E\")", "reason": "Adatta la vista al disegno." }""");

        var exception = Record.Exception(() => ToolSchemaValidator.Validate(spec, input));

        Assert.Null(exception);
    }

    [Fact]
    public void SaveSignatureAsCategory_InvalidScope_FailsValidation()
    {
        ToolCatalog.TryGet("save_signature_as_category", out var spec);
        var input = Parse("""{ "signature": {}, "label": "telecamera", "scope": "global" }""");

        var ex = Assert.Throws<ToolSchemaValidationException>(() => ToolSchemaValidator.Validate(spec, input));

        Assert.Contains(ex.Errors, e => e.Contains("scope"));
    }

    [Fact]
    public void ListLayers_WithEmptyObject_PassesValidation()
    {
        ToolCatalog.TryGet("list_layers", out var spec);
        var input = Parse("""{}""");

        var exception = Record.Exception(() => ToolSchemaValidator.Validate(spec, input));

        Assert.Null(exception);
    }
}
