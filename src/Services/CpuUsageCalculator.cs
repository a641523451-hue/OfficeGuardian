using System.Diagnostics;

namespace OfficeGuardian.Services;

public class CpuUsageCalculator
{
    private readonly Dictionary<int, (DateTime Time, TimeSpan TotalProcessorTime)> _previousCpu =
        new();

    public async Task<double> GetCpuUsageAsync(Process process)
    {
        var id = process.Id;
        var now = DateTime.Now;
        var currentTotal = process.TotalProcessorTime;

        double cpuDelta, timeDelta, cpuPercent;

        if (_previousCpu.TryGetValue(id, out var prev))
        {
            cpuDelta = (currentTotal - prev.TotalProcessorTime).TotalMilliseconds;
            timeDelta = (now - prev.Time).TotalMilliseconds;
            cpuPercent = timeDelta > 0 ? (cpuDelta / timeDelta) * 100 : 0;
            cpuPercent = Math.Round(cpuPercent / Environment.ProcessorCount, 2);

            _previousCpu[id] = (now, currentTotal);
            return Math.Max(0, cpuPercent);
        }

        // First sample - wait briefly and measure again
        _previousCpu[id] = (now, currentTotal);
        await Task.Delay(1000);
        process.Refresh();
        var nextTotal = process.TotalProcessorTime;
        var nextNow = DateTime.Now;

        cpuDelta = (nextTotal - currentTotal).TotalMilliseconds;
        timeDelta = (nextNow - now).TotalMilliseconds;
        cpuPercent = timeDelta > 0 ? (cpuDelta / timeDelta) * 100 : 0;
        cpuPercent = Math.Round(cpuPercent / Environment.ProcessorCount, 2);

        _previousCpu[id] = (nextNow, nextTotal);
        return Math.Max(0, cpuPercent);
    }

    public static double CalculateAverage(List<double> values) =>
        values.Count > 0 ? Math.Round(values.Average(), 2) : 0;

    public static double CalculateMax(List<double> values) =>
        values.Count > 0 ? Math.Round(values.Max(), 2) : 0;
}
