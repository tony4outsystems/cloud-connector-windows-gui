namespace CloudConnectorGui.App;

public static class SelfUpdateIntervals
{
    public const string Daily = "daily";
    public const string Weekly = "weekly";
    public const string Monthly = "monthly";
    public const string Off = "off";

    public static readonly string[] All =
    [
        Daily,
        Weekly,
        Monthly,
        Off
    ];

    public static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            Off => Off,
            Weekly => Weekly,
            Monthly => Monthly,
            _ => Daily
        };
    }
}
