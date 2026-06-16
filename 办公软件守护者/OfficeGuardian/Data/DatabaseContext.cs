using Microsoft.Data.Sqlite;
using OfficeGuardian.Models;

namespace OfficeGuardian.Data;

public class DatabaseContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public DatabaseContext(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        Initialize();
    }

    private void Initialize()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ProcessLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                ProcessName TEXT NOT NULL,
                MemoryMB REAL NOT NULL,
                PrivateMemoryMB REAL NOT NULL,
                CpuPercent REAL NOT NULL,
                ThreadCount INTEGER NOT NULL,
                HandleCount INTEGER NOT NULL,
                GdiObjects INTEGER NOT NULL,
                UserObjects INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS LeakWarnings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                ProcessName TEXT NOT NULL,
                WarningType TEXT NOT NULL,
                Message TEXT NOT NULL,
                CurrentValue REAL NOT NULL,
                ThresholdValue REAL NOT NULL
            );
        """;
        cmd.ExecuteNonQuery();
    }

    public void InsertProcessLog(ProcessLog log)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ProcessLogs (Timestamp, ProcessName, MemoryMB, PrivateMemoryMB, CpuPercent, ThreadCount, HandleCount, GdiObjects, UserObjects)
            VALUES (@ts, @pn, @mem, @pmem, @cpu, @tc, @hc, @gdi, @uo)
        """;
        cmd.Parameters.AddWithValue("@ts", log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@pn", log.ProcessName);
        cmd.Parameters.AddWithValue("@mem", log.MemoryMB);
        cmd.Parameters.AddWithValue("@pmem", log.PrivateMemoryMB);
        cmd.Parameters.AddWithValue("@cpu", log.CpuPercent);
        cmd.Parameters.AddWithValue("@tc", log.ThreadCount);
        cmd.Parameters.AddWithValue("@hc", log.HandleCount);
        cmd.Parameters.AddWithValue("@gdi", log.GdiObjects);
        cmd.Parameters.AddWithValue("@uo", log.UserObjects);
        cmd.ExecuteNonQuery();
    }

    public void InsertLeakWarning(LeakWarning warning)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO LeakWarnings (Timestamp, ProcessName, WarningType, Message, CurrentValue, ThresholdValue)
            VALUES (@ts, @pn, @wt, @msg, @cv, @tv)
        """;
        cmd.Parameters.AddWithValue("@ts", warning.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@pn", warning.ProcessName);
        cmd.Parameters.AddWithValue("@wt", warning.WarningType);
        cmd.Parameters.AddWithValue("@msg", warning.Message);
        cmd.Parameters.AddWithValue("@cv", warning.CurrentValue);
        cmd.Parameters.AddWithValue("@tv", warning.ThresholdValue);
        cmd.ExecuteNonQuery();
    }

    public List<ProcessLog> GetRecentLogs(string processName, TimeSpan lookback)
    {
        var since = DateTime.Now - lookback;
        var logs = new List<ProcessLog>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM ProcessLogs 
            WHERE ProcessName = @pn AND Timestamp >= @since 
            ORDER BY Timestamp ASC
        """;
        cmd.Parameters.AddWithValue("@pn", processName);
        cmd.Parameters.AddWithValue("@since", since.ToString("yyyy-MM-dd HH:mm:ss"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(MapLog(reader));
        }
        return logs;
    }

    public List<ProcessLog> GetLogsForDate(string processName, DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var logs = new List<ProcessLog>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM ProcessLogs 
            WHERE ProcessName = @pn AND Timestamp >= @start AND Timestamp < @end 
            ORDER BY Timestamp ASC
        """;
        cmd.Parameters.AddWithValue("@pn", processName);
        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd HH:mm:ss"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(MapLog(reader));
        }
        return logs;
    }

    public List<ProcessLog> GetAllLogsForDate(DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var logs = new List<ProcessLog>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM ProcessLogs 
            WHERE Timestamp >= @start AND Timestamp < @end 
            ORDER BY Timestamp ASC
        """;
        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd HH:mm:ss"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(MapLog(reader));
        }
        return logs;
    }

    public List<ProcessLog> GetRecentLogsAllProcesses(TimeSpan lookback)
    {
        var since = DateTime.Now - lookback;
        var logs = new List<ProcessLog>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM ProcessLogs 
            WHERE Timestamp >= @since 
            ORDER BY ProcessName, Timestamp ASC
        """;
        cmd.Parameters.AddWithValue("@since", since.ToString("yyyy-MM-dd HH:mm:ss"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(MapLog(reader));
        }
        return logs;
    }

    public Dictionary<string, List<ProcessLog>> GetDailyLogs(DateTime date)
    {
        var logs = GetAllLogsForDate(date);
        return logs.GroupBy(l => l.ProcessName)
                   .ToDictionary(g => g.Key, g => g.ToList());
    }

    public List<LeakWarning> GetWarningsForDate(DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var warnings = new List<LeakWarning>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM LeakWarnings 
            WHERE Timestamp >= @start AND Timestamp < @end 
            ORDER BY Timestamp ASC
        """;
        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd HH:mm:ss"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            warnings.Add(MapWarning(reader));
        }
        return warnings;
    }

    public List<LeakWarning> GetRecentWarnings(TimeSpan lookback)
    {
        var since = DateTime.Now - lookback;
        var warnings = new List<LeakWarning>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM LeakWarnings 
            WHERE Timestamp >= @since 
            ORDER BY Timestamp DESC
        """;
        cmd.Parameters.AddWithValue("@since", since.ToString("yyyy-MM-dd HH:mm:ss"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            warnings.Add(MapWarning(reader));
        }
        return warnings;
    }

    private static ProcessLog MapLog(SqliteDataReader reader)
    {
        return new ProcessLog
        {
            Id = reader.GetInt32(0),
            Timestamp = DateTime.Parse(reader.GetString(1)),
            ProcessName = reader.GetString(2),
            MemoryMB = reader.GetDouble(3),
            PrivateMemoryMB = reader.GetDouble(4),
            CpuPercent = reader.GetDouble(5),
            ThreadCount = reader.GetInt32(6),
            HandleCount = reader.GetInt32(7),
            GdiObjects = reader.GetInt32(8),
            UserObjects = reader.GetInt32(9)
        };
    }

    private static LeakWarning MapWarning(SqliteDataReader reader)
    {
        return new LeakWarning
        {
            Id = reader.GetInt32(0),
            Timestamp = DateTime.Parse(reader.GetString(1)),
            ProcessName = reader.GetString(2),
            WarningType = reader.GetString(3),
            Message = reader.GetString(4),
            CurrentValue = reader.GetDouble(5),
            ThresholdValue = reader.GetDouble(6)
        };
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
