using System.IO;
using System.Text;
using OfficeGuardian.Data;
using OfficeGuardian.Models;

namespace OfficeGuardian.Services;

public class ReportService
{
    private readonly DatabaseContext _db;
    private readonly string _reportDir;

    public ReportService(DatabaseContext db)
    {
        _db = db;
        _reportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Reports");
        Directory.CreateDirectory(_reportDir);
    }

    public async Task<string> GenerateDailyReportAsync(DateTime date)
    {
        var dailyLogs = _db.GetDailyLogs(date);
        var warnings = _db.GetWarningsForDate(date);
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='zh-CN'><head><meta charset='UTF-8'>");
        sb.AppendLine("<title>WPS 运行状态日报</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Microsoft YaHei', sans-serif; margin: 20px; background: #f5f5f5; }");
        sb.AppendLine("h1 { color: #333; border-bottom: 2px solid #4CAF50; padding-bottom: 10px; }");
        sb.AppendLine("h2 { color: #555; margin-top: 30px; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 10px 0; background: white; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
        sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px 12px; text-align: left; }");
        sb.AppendLine("th { background: #4CAF50; color: white; }");
        sb.AppendLine("tr:nth-child(even) { background: #f9f9f9; }");
        sb.AppendLine(".warning { color: #e74c3c; font-weight: bold; }");
        sb.AppendLine(".normal { color: #27ae60; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>WPS 运行状态日报</h1>");
        sb.AppendLine($"<p>报告日期: {date:yyyy-MM-dd}</p>");
        sb.AppendLine($"<p>生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

        foreach (var (processName, logs) in dailyLogs)
        {
            if (logs.Count == 0) continue;

            var memValues = logs.Select(l => l.MemoryMB).ToList();
            var cpuValues = logs.Select(l => l.CpuPercent).ToList();
            var memMax = memValues.Max();
            var memAvg = Math.Round(memValues.Average(), 2);
            var cpuMax = cpuValues.Max();
            var cpuAvg = Math.Round(cpuValues.Average(), 2);
            var procWarnings = warnings.Where(w => w.ProcessName == processName).ToList();

            sb.AppendLine($"<h2>{processName}</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>指标</th><th>值</th></tr>");
            sb.AppendLine($"<tr><td>最高内存</td><td>{memMax:F0} MB</td></tr>");
            sb.AppendLine($"<tr><td>平均内存</td><td>{memAvg:F0} MB</td></tr>");
            sb.AppendLine($"<tr><td>CPU峰值</td><td>{cpuMax:F2}%</td></tr>");
            sb.AppendLine($"<tr><td>平均CPU</td><td>{cpuAvg:F2}%</td></tr>");
            sb.AppendLine($"<tr><td>采样次数</td><td>{logs.Count}</td></tr>");
            sb.AppendLine("</table>");

            if (procWarnings.Count > 0)
            {
                sb.AppendLine("<h3 class='warning'>异常告警</h3>");
                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>时间</th><th>类型</th><th>消息</th></tr>");
                foreach (var w in procWarnings)
                {
                    sb.AppendLine($"<tr><td>{w.Timestamp:HH:mm:ss}</td><td class='warning'>{w.WarningType}</td><td>{w.Message}</td></tr>");
                }
                sb.AppendLine("</table>");
            }
            else
            {
                sb.AppendLine("<p class='normal'>今日无异常告警</p>");
            }
        }

        sb.AppendLine("</body></html>");

        var fileName = $"DailyReport_{date:yyyy-MM-dd}.html";
        var filePath = Path.Combine(_reportDir, fileName);
        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

        return filePath;
    }

    public string ExportAiAnalysisData(string processName)
    {
        var lookback = TimeSpan.FromHours(4);
        var logs = _db.GetRecentLogs(processName, lookback);
        if (logs.Count < 2) return "{}";

        var first = logs.First();
        var last = logs.Last();
        var cpuValues = logs.Select(l => l.CpuPercent).ToList();
        var cpuAvg = Math.Round(cpuValues.Average(), 2);

        var json = """
{
  "Process": "{{processName}}",
  "MemoryStart": {{first.MemoryMB}},
  "MemoryEnd": {{last.MemoryMB}},
  "CpuAvg": {{cpuAvg}},
  "HandlesStart": {{first.HandleCount}},
  "HandlesEnd": {{last.HandleCount}}
}
""";
        return json;
    }
}
