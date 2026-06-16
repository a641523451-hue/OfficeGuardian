using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OfficeGuardian.Data;
using OfficeGuardian.Services;
using OfficeGuardian.ViewModels;
using OfficeGuardian.Views;

namespace OfficeGuardian;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private ProcessMonitorService? _monitorService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var dbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data", "officeguardian.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton<IConfiguration>(config);

        // Data
        services.AddSingleton(sp => new DatabaseContext(dbPath));

        // Services
        services.AddSingleton<CpuUsageCalculator>();
        services.AddSingleton<LeakDetector>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<ProcessMonitorService>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>();

        // Views
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        // Start monitoring
        _monitorService = _serviceProvider.GetRequiredService<ProcessMonitorService>();
        _monitorService.Start();

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitorService?.Stop();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
