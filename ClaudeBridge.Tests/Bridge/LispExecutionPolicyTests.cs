using ClaudeBridge.Core.Bridge;

namespace ClaudeBridge.Tests.Bridge;

public class LispExecutionPolicyTests
{
    // Piano di Test §2, Scenario M: un'espressione che richiederebbe accesso al filesystem o
    // l'esecuzione di comandi di sistema deve essere rifiutata PRIMA della conferma UI.
    [Theory]
    [InlineData("(open \"C:\\\\Windows\\\\system.ini\" \"r\")")]
    [InlineData("(startapp \"cmd.exe\")")]
    [InlineData("(vl-file-delete \"important.dwg\")")]
    [InlineData("(write-line \"leaked\" (open \"out.txt\" \"w\"))")]
    public void IsAllowed_DangerousExpression_IsBlockedWithReason(string expression)
    {
        var allowed = LispExecutionPolicy.IsAllowed(expression, out var reason);

        Assert.False(allowed);
        Assert.NotNull(reason);
    }

    // Piano di Test §2, Scenario M: un'espressione LISP legittima (manipolazione di un'entità)
    // non deve essere bloccata dalla policy.
    [Theory]
    [InlineData("(command \"_ZOOM\" \"_E\")")]
    [InlineData("(setq ent (entlast)) (entmod (subst (cons 62 1) (assoc 62 (entget ent)) (entget ent)))")]
    [InlineData("(command \"_MOVE\" (ssget \"L\") \"\" '(0 0 0) '(10 0 0))")]
    public void IsAllowed_LegitimateExpression_IsNotBlocked(string expression)
    {
        var allowed = LispExecutionPolicy.IsAllowed(expression, out var reason);

        Assert.True(allowed);
        Assert.Null(reason);
    }

    [Fact]
    public void IsAllowed_FunctionNameAsSubstringOfLongerIdentifier_IsNotBlocked()
    {
        // "openings-count" non è una chiamata a "open", solo un identificatore che la contiene.
        var allowed = LispExecutionPolicy.IsAllowed("(setq openings-count 3)", out var reason);

        Assert.True(allowed);
        Assert.Null(reason);
    }
}
