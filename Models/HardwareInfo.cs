namespace VTStudioToolBox.Models;

public sealed record HardwareInfo
{
    public string Cpu { get; init; } = "";
    public string Gpu { get; init; } = "";
    public double RamGb { get; init; }
    public string OsVersion { get; init; } = "";
}
