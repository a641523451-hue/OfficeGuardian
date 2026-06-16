using System.Windows;
using OfficeGuardian.Models;

namespace OfficeGuardian.Services;

public class AlertService
{
    private const double MemoryAlertThresholdMB = 3000;

    public void ShowAlert(List<LeakWarning> warnings)
    {
        var memoryWarnings = warnings.Where(w => w.WarningType == "MemoryWarning").ToList();
        if (memoryWarnings.Count == 0) return;

        foreach (var w in memoryWarnings)
        {
            // Check if current memory exceeds threshold
            if (w.CurrentValue + w.ThresholdValue > MemoryAlertThresholdMB)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"检测到WPS资源占用异常\n\n进程: {w.ProcessName}\n警告: {w.Message}\n\n建议保存文件并重启WPS",
                        "WPS 资源告警",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }
        }
    }

    public static bool CheckMemoryAlert(double currentMemoryMB)
    {
        return currentMemoryMB > MemoryAlertThresholdMB;
    }

    public void ShowDirectAlert(double memoryMB, string processName)
    {
        if (memoryMB > MemoryAlertThresholdMB)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"检测到WPS资源占用异常\n\n进程: {processName}\n当前内存: {memoryMB:F0} MB\n阈值: {MemoryAlertThresholdMB} MB\n\n建议保存文件并重启WPS",
                    "WPS 资源告警",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        }
    }
}
