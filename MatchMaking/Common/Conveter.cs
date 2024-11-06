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
        return _matchModeToString.ContainsKey(matchMode) ? _matchModeToString[matchMode] : _matchModeToString[MatchMode.None];
    }

    public static MatchMode ToMatchMode(this string name)
    {
        return _stringToMatchMode.ContainsKey(name) ? _stringToMatchMode[name] : MatchMode.None;
    }
}
