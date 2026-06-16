using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OfficeGuardian.Data;
using OfficeGuardian.Models;

namespace OfficeGuardian.Services;

public class ProcessMonitorService
{
    private readonly DatabaseContext _db;
    private readonly CpuUsageCalculator _cpuCalc;
    private readonly LeakDetector _leakDetector;
    private readonly AlertService _alertService;
    private readonly string[] _targetProcesses;
    private Timer? _timer;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(60);

    public event Action<List<ProcessLog>>? OnScanCompleted;
    public event Action<List<LeakWarning>>? OnWarningsDetected;

    public ProcessMonitorService(DatabaseContext db, CpuUsageCalculator cpuCalc, 
        LeakDetector leakDetector, AlertService alertService,
        IConfiguration configuration)
    {
        _db = db;
        _cpuCalc = cpuCalc;
        _leakDetector = leakDetector;
        _alertService = alertService;
        _targetProcesses = configuration.GetSection("WatchProcesses").Get<string[]>()
            ?? Array.Empty<string>();
    }

    public void Start()
    {
        _timer = new Timer(async _ => await ScanAsync(), null, TimeSpan.Zero, ScanInterval);
    }

    public void Stop()
    {
        _timer?.Dispose();
    }

    private async Task ScanAsync()
    {
        var logs = new List<ProcessLog>();
        var now = DateTime.Now;

        foreach (var procName in _targetProcesses)
        {
            try
            {
                var processes = Process.GetProcessesByName(procName);
                foreach (var proc in processes)
                {
                    try
                    {
                        var memMB = Math.Round(proc.WorkingSet64 / 1024.0 / 1024.0, 2);
                        var privMB = Math.Round(proc.PrivateMemorySize64 / 1024.0 / 1024.0, 2);
                        var cpuPercent = Math.Round(await _cpuCalc.GetCpuUsageAsync(proc), 2);
                        
                        var log = new ProcessLog
                        {
                            Timestamp = now,
                            ProcessName = $"{proc.ProcessName}.exe",
                            MemoryMB = memMB,
                            PrivateMemoryMB = privMB,
                            CpuPercent = cpuPercent,
                            ThreadCount = proc.Threads.Count,
                            HandleCount = proc.HandleCount,
                            GdiObjects = 0,
                            UserObjects = 0
                        };

                        try { log.GdiObjects = GetGdiObjects(proc.Handle); } catch { }
                        try { log.UserObjects = GetUserObjects(proc.Handle); } catch { }

                        logs.Add(log);
                        _db.InsertProcessLog(log);
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch { }
        }

        if (logs.Count > 0)
        {
            OnScanCompleted?.Invoke(logs);

            var warnings = _leakDetector.Analyze(logs);
            foreach (var w in warnings)
            {
                _db.InsertLeakWarning(w);
            }

            if (warnings.Count > 0)
            {
                OnWarningsDetected?.Invoke(warnings);
                _alertService.ShowAlert(warnings);
            }
        }

        await Task.CompletedTask;
    }

    private static int GetGdiObjects(IntPtr hProcess) => 0;
    private static int GetUserObjects(IntPtr hProcess) => 0;
}
