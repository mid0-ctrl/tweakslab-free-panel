namespace TweaksLabPanel.Core;

public sealed record SystemSpecsSnapshot(
    string OperatingSystem,
    string Processor,
    string ProcessorDetails,
    string GraphicsCard,
    string Memory,
    string Motherboard,
    string Storage,
    double? CpuTempC,
    double? GpuTempC,
    double SystemDriveFreePercent,
    long SystemDriveFreeBytes,
    long SystemDriveTotalBytes,
    string SystemDriveLetter);
