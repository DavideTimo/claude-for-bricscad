using System.Text.RegularExpressions;

namespace ClaudeBridge.SimulatedBridge;

/// <summary>Pattern con wildcard * e ? per i nomi di blocco (Tool Contract, find_blocks), case-insensitive.</summary>
internal static class WildcardMatcher
{
    public static bool IsMatch(string pattern, string value)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }
}
