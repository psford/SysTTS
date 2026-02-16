namespace SysTTS.Settings;

public class SourceSettings
{
    public string? Voice { get; set; }
    public string[]? Filters { get; set; }
    public int Priority { get; set; } = 3;
}
