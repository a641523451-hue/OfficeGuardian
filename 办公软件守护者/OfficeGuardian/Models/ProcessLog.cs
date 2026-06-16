namespace OfficeGuardian.Models;

public class ProcessLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public double MemoryMB { get; set; }
    public double PrivateMemoryMB { get; set; }
    public double CpuPercent { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public int GdiObjects { get; set; }
    public int UserObjects { get; set; }
}

public class LeakWarning
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WarningType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double ThresholdValue { get; set; }
}
