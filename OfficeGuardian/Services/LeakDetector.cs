using OfficeGuardian.Data;
using OfficeGuardian.Models;

namespace OfficeGuardian.Services;

public class LeakDetector
{
    private readonly DatabaseContext _db;
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromHours(4);
    private const double MemoryLeakThresholdMB = 500;
    private const double ThreadGrowthThreshold = 0.30;

    public LeakDetector(DatabaseContext db)
    {
        _db = db;
    }

    public List<LeakWarning> Analyze(List<ProcessLog> currentLogs)
    {
        var warnings = new List<LeakWarning>();
        var now = DateTime.Now;

        foreach (var log in currentLogs)
        {
            var history = _db.GetRecentLogs(log.ProcessName, LookbackWindow);

            if (history.Count < 2) continue;

            var first = history.First();
            var last = history.Last();

            // 1. Memory leak detection
            if (last.MemoryMB - first.MemoryMB > MemoryLeakThresholdMB)
            {
                warnings.Add(new LeakWarning
                {
                    Timestamp = now,
                    ProcessName = log.ProcessName,
                    WarningType = "MemoryWarning",
                    Message = $"内存持续增长: {first.MemoryMB}MB -> {last.MemoryMB}MB (增长 {last.MemoryMB - first.MemoryMB:F0}MB, 阈值 {MemoryLeakThresholdMB}MB)",
                    CurrentValue = last.MemoryMB - first.MemoryMB,
                    ThresholdValue = MemoryLeakThresholdMB
                });
            }

            // 2. Thread growth detection
            var firstThreads = first.ThreadCount;
            var lastThreads = last.ThreadCount;
            if (firstThreads > 0)
            {
                var growthRatio = (double)(lastThreads - firstThreads) / firstThreads;
                if (growthRatio > ThreadGrowthThreshold)
                {
                    warnings.Add(new LeakWarning
                    {
                        Timestamp = now,
                        ProcessName = log.ProcessName,
                        WarningType = "ThreadWarning",
                        Message = $"线程数异常增长: {firstThreads} -> {lastThreads} (增长 {growthRatio * 100:F0}%, 阈值 {ThreadGrowthThreshold * 100}%)",
                        CurrentValue = Math.Round(growthRatio * 100, 2),
                        ThresholdValue = ThreadGrowthThreshold * 100
                    });
                }
            }

            // 3. Handle growth detection
            var firstHandle = first.HandleCount;
            var lastHandle = last.HandleCount;
            // Check if handle count keeps increasing with no decrease in 4 hours
            if (lastHandle > firstHandle && IsMonotonicIncreasing(history.Select(h => h.HandleCount).ToList()))
            {
                warnings.Add(new LeakWarning
                {
                    Timestamp = now,
                    ProcessName = log.ProcessName,
                    WarningType = "HandleLeakWarning",
                    Message = $"句柄持续增长无回落: {firstHandle} -> {lastHandle} (增长 {lastHandle - firstHandle}, 4小时内无下降)",
                    CurrentValue = lastHandle - firstHandle,
                    ThresholdValue = 0
                });
            }
        }

        return warnings;
    }

    private static bool IsMonotonicIncreasing(List<int> values)
    {
        if (values.Count < 2) return false;
        // Use last N samples (every ~60s for 4h = ~240 samples, check last 60)
        var sample = values.TakeLast(60).ToList();
        for (int i = 1; i < sample.Count; i++)
        {
            if (sample[i] < sample[i - 1]) return false;
        }
        return true;
    }
}
