namespace VTStudioToolBox.Models;

public enum AnalyticsEventType
{
    AppLaunch,
    ToolUsage
}

public sealed record AnalyticsEvent
{
    public AnalyticsEventType Type { get; init; }
    public string DeviceId { get; init; } = "";
    public long Timestamp { get; init; }
    public string? ToolName { get; init; }
    public HardwareInfo? Hardware { get; init; }
}
