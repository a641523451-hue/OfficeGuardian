using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OfficeGuardian.Data;
using OfficeGuardian.Models;
using OfficeGuardian.Services;
using SkiaSharp;
using System.Windows;
using System.Windows.Input;

namespace OfficeGuardian.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly DatabaseContext _db;
    private readonly ProcessMonitorService _monitorService;
    private readonly ReportService _reportService;
    private readonly AlertService _alertService;
    private Timer? _refreshTimer;

    // Current status fields
    private string _statusText = "等待扫描...";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private string _wpsMemory = "--";
    public string WpsMemory { get => _wpsMemory; set => SetProperty(ref _wpsMemory, value); }

    private string _wpsCpu = "--";
    public string WpsCpu { get => _wpsCpu; set => SetProperty(ref _wpsCpu, value); }

    private string _wpsThreads = "--";
    public string WpsThreads { get => _wpsThreads; set => SetProperty(ref _wpsThreads, value); }

    private string _wpsHandles = "--";
    public string WpsHandles { get => _wpsHandles; set => SetProperty(ref _wpsHandles, value); }

    private string _memoryTrendDescription = "等待数据...";
    public string MemoryTrendDescription { get => _memoryTrendDescription; set => SetProperty(ref _memoryTrendDescription, value); }

    private string _cpuTrendDescription = "等待数据...";
    public string CpuTrendDescription { get => _cpuTrendDescription; set => SetProperty(ref _cpuTrendDescription, value); }

    private string _threadTrendDescription = "等待数据...";
    public string ThreadTrendDescription { get => _threadTrendDescription; set => SetProperty(ref _threadTrendDescription, value); }

    private string _lastScanTime = "--";
    public string LastScanTime { get => _lastScanTime; set => SetProperty(ref _lastScanTime, value); }

    private int _warningCount;
    public int WarningCount { get => _warningCount; set => SetProperty(ref _warningCount, value); }

    private string _aiAnalysisResult = "点击按钮导出AI分析数据";
    public string AiAnalysisResult { get => _aiAnalysisResult; set => SetProperty(ref _aiAnalysisResult, value); }

    private string _reportResult = "";
    public string ReportResult { get => _reportResult; set => SetProperty(ref _reportResult, value); }

    private bool _isAiDataReady;
    public bool IsAiDataReady { get => _isAiDataReady; set => SetProperty(ref _isAiDataReady, value); }

    // Recent warnings
    public ObservableCollection<LeakWarning> RecentWarnings { get; } = new();

    // Chart data
    public ObservableCollection<DateTimePoint> MemoryPoints { get; } = new();
    public ObservableCollection<DateTimePoint> CpuPoints { get; } = new();
    public ObservableCollection<DateTimePoint> ThreadPoints { get; } = new();

    // LiveCharts2 Series
    public ISeries[] MemorySeries { get; set; }
    public ISeries[] CpuSeries { get; set; }
    public ISeries[] ThreadSeries { get; set; }

    // Axis
    public Axis[] MemoryAxis { get; set; }
    public Axis[] CpuAxis { get; set; }
    public Axis[] ThreadAxis { get; set; }

    // Commands
    public ICommand GenerateReportCommand { get; }
    public ICommand ExportAiCommand { get; }

    public DashboardViewModel(DatabaseContext db, ProcessMonitorService monitorService,
        ReportService reportService, AlertService alertService)
    {
        _db = db;
        _monitorService = monitorService;
        _reportService = reportService;
        _alertService = alertService;

        GenerateReportCommand = new RelayCommand(async () => await GenerateReportAsync());
        ExportAiCommand = new RelayCommand(ExportAiData);

        // Initialize chart series
        MemorySeries = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = MemoryPoints,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(30)),
                GeometrySize = 0,
                LineSmoothness = 0.3,
                Name = "内存 (MB)"
            }
        };

        CpuSeries = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = CpuPoints,
                Stroke = new SolidColorPaint(SKColors.OrangeRed) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColors.OrangeRed.WithAlpha(30)),
                GeometrySize = 0,
                LineSmoothness = 0.3,
                Name = "CPU (%)"
            }
        };

        ThreadSeries = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = ThreadPoints,
                Stroke = new SolidColorPaint(SKColors.MediumSeaGreen) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColors.MediumSeaGreen.WithAlpha(30)),
                GeometrySize = 0,
                LineSmoothness = 0.3,
                Name = "线程"
            }
        };

        MemoryAxis = new Axis[]
        {
            new DateTimeAxis(TimeSpan.FromMinutes(10), date => date.ToString("HH:mm"))
            {
                LabelsRotation = 45,
                Name = "时间",
                TextSize = 11
            },
            new Axis { Name = "MB", TextSize = 11 }
        };

        CpuAxis = new Axis[]
        {
            new DateTimeAxis(TimeSpan.FromMinutes(10), date => date.ToString("HH:mm"))
            {
                LabelsRotation = 45,
                Name = "时间",
                TextSize = 11
            },
            new Axis { Name = "%", TextSize = 11, MinLimit = 0 }
        };

        ThreadAxis = new Axis[]
        {
            new DateTimeAxis(TimeSpan.FromMinutes(10), date => date.ToString("HH:mm"))
            {
                LabelsRotation = 45,
                Name = "时间",
                TextSize = 11
            },
            new Axis { Name = "线程数", TextSize = 11, MinLimit = 0 }
        };

        // Subscribe to monitor events
        _monitorService.OnScanCompleted += OnScanData;
        _monitorService.OnWarningsDetected += OnWarnings;

        // Auto-refresh UI every 5 seconds
        _refreshTimer = new Timer(_ => RefreshFromDb(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private void OnScanData(List<ProcessLog> logs)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LastScanTime = DateTime.Now.ToString("HH:mm:ss");
            StatusText = $"运行中 - 上次扫描: {LastScanTime}";
            UpdateCurrentStatus(logs);
        });
    }

    private void OnWarnings(List<LeakWarning> warnings)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var w in warnings)
            {
                RecentWarnings.Insert(0, w);
                if (RecentWarnings.Count > 50) RecentWarnings.RemoveAt(RecentWarnings.Count - 1);
            }
            WarningCount = RecentWarnings.Count;
        });
    }

    private void UpdateCurrentStatus(List<ProcessLog> logs)
    {
        var wpsLog = logs.FirstOrDefault(l => l.ProcessName.StartsWith("wps", StringComparison.OrdinalIgnoreCase)
            && !l.ProcessName.Contains("cloud") && !l.ProcessName.Contains("center"));
        if (wpsLog != null)
        {
            WpsMemory = $"{wpsLog.MemoryMB:F0} MB";
            WpsCpu = $"{wpsLog.CpuPercent:F1}%";
            WpsThreads = wpsLog.ThreadCount.ToString();
            WpsHandles = wpsLog.HandleCount.ToString();

            // Check memory alert
            if (AlertService.CheckMemoryAlert(wpsLog.MemoryMB))
            {
                _alertService.ShowDirectAlert(wpsLog.MemoryMB, wpsLog.ProcessName);
            }
        }
    }

    private void RefreshFromDb()
    {
        try
        {
            var lookback = TimeSpan.FromHours(4);
            var wpsLogs = _db.GetRecentLogs("wps.exe", lookback);

            Application.Current.Dispatcher.Invoke(() =>
            {
                MemoryPoints.Clear();
                CpuPoints.Clear();
                ThreadPoints.Clear();

                foreach (var log in wpsLogs)
                {
                    MemoryPoints.Add(new DateTimePoint(log.Timestamp, log.MemoryMB));
                    CpuPoints.Add(new DateTimePoint(log.Timestamp, log.CpuPercent));
                    ThreadPoints.Add(new DateTimePoint(log.Timestamp, log.ThreadCount));
                }

                // Update descriptions
                if (wpsLogs.Count > 0)
                {
                    var memValues = wpsLogs.Select(l => l.MemoryMB).ToList();
                    var cpuValues = wpsLogs.Select(l => l.CpuPercent).ToList();
                    var threadValues = wpsLogs.Select(l => l.ThreadCount).ToList();

                    MemoryTrendDescription = $"当前: {memValues.Last():F0} MB | 最高: {memValues.Max():F0} MB | 最低: {memValues.Min():F0} MB";
                    CpuTrendDescription = $"当前: {cpuValues.Last():F2}% | 峰值: {cpuValues.Max():F2}% | 平均: {cpuValues.Average():F2}%";
                    ThreadTrendDescription = $"当前: {threadValues.Last()} | 最高: {threadValues.Max()} | 最低: {threadValues.Min()}";
                }
            });
        }
        catch { /* db may be busy */ }
    }

    private async Task GenerateReportAsync()
    {
        try
        {
            var path = await _reportService.GenerateDailyReportAsync(DateTime.Now.Date);
            ReportResult = $"报告已生成: {path}";
        }
        catch (Exception ex)
        {
            ReportResult = $"生成失败: {ex.Message}";
        }
    }

    private void ExportAiData(object? parameter)
    {
        var processName = parameter as string ?? "wps.exe";
        var json = _reportService.ExportAiAnalysisData(processName);

        var prompt = $"""
请分析以下WPS监控数据：

{json}

判断：
1 是否存在内存泄漏
2 是否存在句柄泄漏
3 是否存在CPU异常
4 给出优化建议
""";

        AiAnalysisResult = prompt;
        IsAiDataReady = true;

        // Copy to clipboard
        try
        {
            System.Windows.Clipboard.SetText(prompt);
            StatusText = "AI分析数据已复制到剪贴板！";
        }
        catch { }
    }
}
