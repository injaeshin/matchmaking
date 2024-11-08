namespace MatchMaking.Common;

public static class Converter
{
    private static readonly Dictionary<MatchMode, string> _matchModeToString;
    private static readonly Dictionary<string, MatchMode> _stringToMatchMode;

    static Converter()
    {
        _matchModeToString = Enum.GetValues(typeof(MatchMode))
            .Cast<MatchMode>()
            .ToDictionary(mode => mode, mode => mode.ToString());

        _stringToMatchMode = _matchModeToString.ToDictionary(kv => kv.Value, kv => kv.Key);
    }

    public static string ToFastString(this MatchMode matchMode)
    {
        if (!_matchModeToString.TryGetValue(matchMode, out var value))
        {
            return _matchModeToString[MatchMode.None];
        }

        return value;
    }

    public static MatchMode ToMatchMode(this string name)
    {
        if (!_stringToMatchMode.TryGetValue(name, out var value))
        {
            return MatchMode.None;
        }

        return value;
    }
}
