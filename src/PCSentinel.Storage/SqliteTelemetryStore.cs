using Microsoft.Data.Sqlite;
using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Storage;

/// <summary>
/// High-performance local time-series store implemented with Microsoft.Data.Sqlite.
/// Stores hardware telemetry snapshots, diagnostic events, scores, and baseline profiles.
/// </summary>
public sealed class SqliteTelemetryStore : ITelemetryStore
{
    private readonly string _connectionString;

    public SqliteTelemetryStore(string? databasePath = null)
    {
        var path = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCSentinel",
            "sentinel_telemetry.db");

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        // Turn on WAL mode for fast concurrent write & read performance
        await using (var walCmd = conn.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
            await walCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var schemaSql = @"
            CREATE TABLE IF NOT EXISTS Telemetry (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                CpuTemp REAL,
                CpuClock REAL,
                CpuLoad REAL,
                CpuPower REAL,
                GpuTemp REAL,
                GpuHotspot REAL,
                GpuClock REAL,
                GpuLoad REAL,
                GpuPower REAL,
                GpuFan REAL,
                RamUsedGb REAL,
                RamLoadPercent REAL
            );

            CREATE INDEX IF NOT EXISTS IX_Telemetry_Timestamp ON Telemetry (Timestamp DESC);

            CREATE TABLE IF NOT EXISTS HealthEvents (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                Component TEXT NOT NULL,
                Severity TEXT NOT NULL,
                EventType TEXT NOT NULL,
                Title TEXT NOT NULL,
                Evidence TEXT NOT NULL,
                Confidence REAL NOT NULL,
                Recommendation TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_HealthEvents_Timestamp ON HealthEvents (Timestamp DESC);

            CREATE TABLE IF NOT EXISTS HealthScores (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                OverallScore INTEGER NOT NULL,
                CpuScore INTEGER NOT NULL,
                GpuScore INTEGER NOT NULL,
                MemoryScore INTEGER NOT NULL,
                StorageScore INTEGER NOT NULL,
                DeductionsJson TEXT
            );

            CREATE TABLE IF NOT EXISTS Baselines (
                Id TEXT PRIMARY KEY,
                ProfileName TEXT NOT NULL,
                Workload TEXT NOT NULL,
                SampleCount INTEGER NOT NULL,
                AvgCpuTemp REAL NOT NULL,
                StdDevCpuTemp REAL NOT NULL,
                AvgCpuClock REAL NOT NULL,
                AvgCpuPower REAL NOT NULL,
                AvgGpuTemp REAL NOT NULL,
                StdDevGpuTemp REAL NOT NULL,
                AvgGpuClock REAL NOT NULL,
                AvgGpuPower REAL NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = schemaSql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task StoreTelemetrySampleAsync(TelemetrySample sample, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO Telemetry (
                Id, Timestamp, CpuTemp, CpuClock, CpuLoad, CpuPower,
                GpuTemp, GpuHotspot, GpuClock, GpuLoad, GpuPower, GpuFan,
                RamUsedGb, RamLoadPercent
            ) VALUES (
                @Id, @Timestamp, @CpuTemp, @CpuClock, @CpuLoad, @CpuPower,
                @GpuTemp, @GpuHotspot, @GpuClock, @GpuLoad, @GpuPower, @GpuFan,
                @RamUsedGb, @RamLoadPercent
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", sample.Id.ToString());
        cmd.Parameters.AddWithValue("@Timestamp", sample.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@CpuTemp", (object?)sample.CpuTemperatureC ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CpuClock", (object?)sample.CpuClockMhz ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CpuLoad", (object?)sample.CpuLoadPercent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CpuPower", (object?)sample.CpuPowerWatts ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GpuTemp", (object?)sample.GpuTemperatureC ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GpuHotspot", (object?)sample.GpuHotspotC ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GpuClock", (object?)sample.GpuClockMhz ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GpuLoad", (object?)sample.GpuLoadPercent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GpuPower", (object?)sample.GpuPowerWatts ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GpuFan", (object?)sample.GpuFanPercent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RamUsedGb", (object?)sample.RamUsedGb ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RamLoadPercent", (object?)sample.RamLoadPercent ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task StoreHealthEventAsync(HealthEvent healthEvent, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO HealthEvents (
                Id, Timestamp, Component, Severity, EventType, Title, Evidence, Confidence, Recommendation
            ) VALUES (
                @Id, @Timestamp, @Component, @Severity, @EventType, @Title, @Evidence, @Confidence, @Recommendation
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", healthEvent.Id.ToString());
        cmd.Parameters.AddWithValue("@Timestamp", healthEvent.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@Component", healthEvent.Component.ToString());
        cmd.Parameters.AddWithValue("@Severity", healthEvent.Severity.ToString());
        cmd.Parameters.AddWithValue("@EventType", healthEvent.Type.ToString());
        cmd.Parameters.AddWithValue("@Title", healthEvent.Title);
        cmd.Parameters.AddWithValue("@Evidence", healthEvent.Evidence);
        cmd.Parameters.AddWithValue("@Confidence", healthEvent.Confidence);
        cmd.Parameters.AddWithValue("@Recommendation", healthEvent.Recommendation);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task StoreHealthScoreAsync(HealthScore score, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO HealthScores (
                Id, Timestamp, OverallScore, CpuScore, GpuScore, MemoryScore, StorageScore, DeductionsJson
            ) VALUES (
                @Id, @Timestamp, @Overall, @Cpu, @Gpu, @Mem, @Storage, @Deductions
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", score.Id.ToString());
        cmd.Parameters.AddWithValue("@Timestamp", score.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@Overall", score.OverallScore);
        cmd.Parameters.AddWithValue("@Cpu", score.CpuScore);
        cmd.Parameters.AddWithValue("@Gpu", score.GpuScore);
        cmd.Parameters.AddWithValue("@Mem", score.MemoryScore);
        cmd.Parameters.AddWithValue("@Storage", score.StorageScore);
        cmd.Parameters.AddWithValue("@Deductions", System.Text.Json.JsonSerializer.Serialize(score.Deductions));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task StoreBaselineAsync(WorkloadBaseline baseline, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT OR REPLACE INTO Baselines (
                Id, ProfileName, Workload, SampleCount,
                AvgCpuTemp, StdDevCpuTemp, AvgCpuClock, AvgCpuPower,
                AvgGpuTemp, StdDevGpuTemp, AvgGpuClock, AvgGpuPower, UpdatedAt
            ) VALUES (
                @Id, @ProfileName, @Workload, @SampleCount,
                @AvgCpuTemp, @StdDevCpuTemp, @AvgCpuClock, @AvgCpuPower,
                @AvgGpuTemp, @StdDevGpuTemp, @AvgGpuClock, @AvgGpuPower, @UpdatedAt
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", baseline.Id.ToString());
        cmd.Parameters.AddWithValue("@ProfileName", baseline.ProfileName);
        cmd.Parameters.AddWithValue("@Workload", baseline.Workload.ToString());
        cmd.Parameters.AddWithValue("@SampleCount", baseline.SampleCount);
        cmd.Parameters.AddWithValue("@AvgCpuTemp", baseline.AvgCpuTempC);
        cmd.Parameters.AddWithValue("@StdDevCpuTemp", baseline.StdDevCpuTempC);
        cmd.Parameters.AddWithValue("@AvgCpuClock", baseline.AvgCpuClockMhz);
        cmd.Parameters.AddWithValue("@AvgCpuPower", baseline.AvgCpuPowerW);
        cmd.Parameters.AddWithValue("@AvgGpuTemp", baseline.AvgGpuTempC);
        cmd.Parameters.AddWithValue("@StdDevGpuTemp", baseline.StdDevGpuTempC);
        cmd.Parameters.AddWithValue("@AvgGpuClock", baseline.AvgGpuClockMhz);
        cmd.Parameters.AddWithValue("@AvgGpuPower", baseline.AvgGpuPowerW);
        cmd.Parameters.AddWithValue("@UpdatedAt", baseline.UpdatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelemetrySample>> GetRecentSamplesAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM Telemetry ORDER BY Timestamp DESC LIMIT @Count;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Count", count);

        var list = new List<TelemetrySample>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new TelemetrySample
            {
                Id = Guid.Parse(reader.GetString(0)),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1)),
                CpuTemperatureC = reader.IsDBNull(2) ? null : reader.GetDouble(2),
                CpuClockMhz = reader.IsDBNull(3) ? null : reader.GetDouble(3),
                CpuLoadPercent = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                CpuPowerWatts = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                GpuTemperatureC = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                GpuHotspotC = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                GpuClockMhz = reader.IsDBNull(8) ? null : reader.GetDouble(8),
                GpuLoadPercent = reader.IsDBNull(9) ? null : reader.GetDouble(9),
                GpuPowerWatts = reader.IsDBNull(10) ? null : reader.GetDouble(10),
                GpuFanPercent = reader.IsDBNull(11) ? null : reader.GetDouble(11),
                RamUsedGb = reader.IsDBNull(12) ? null : reader.GetDouble(12),
                RamLoadPercent = reader.IsDBNull(13) ? null : reader.GetDouble(13)
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<HealthEvent>> GetRecentEventsAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM HealthEvents ORDER BY Timestamp DESC LIMIT @Count;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Count", count);

        var list = new List<HealthEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new HealthEvent
            {
                Id = Guid.Parse(reader.GetString(0)),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1)),
                Component = Enum.Parse<ComponentType>(reader.GetString(2)),
                Severity = Enum.Parse<EventSeverity>(reader.GetString(3)),
                Type = Enum.Parse<HealthEventType>(reader.GetString(4)),
                Title = reader.GetString(5),
                Evidence = reader.GetString(6),
                Confidence = reader.GetDouble(7),
                Recommendation = reader.GetString(8)
            });
        }

        return list;
    }

    public async Task<HealthScore?> GetLatestHealthScoreAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM HealthScores ORDER BY Timestamp DESC LIMIT 1;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var json = reader.IsDBNull(7) ? "[]" : reader.GetString(7);
            var deductions = System.Text.Json.JsonSerializer.Deserialize<List<ScoreDeduction>>(json) ?? new();

            return new HealthScore
            {
                Id = Guid.Parse(reader.GetString(0)),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1)),
                OverallScore = reader.GetInt32(2),
                CpuScore = reader.GetInt32(3),
                GpuScore = reader.GetInt32(4),
                MemoryScore = reader.GetInt32(5),
                StorageScore = reader.GetInt32(6),
                Deductions = deductions
            };
        }

        return null;
    }

    public async Task<WorkloadBaseline?> GetBaselineAsync(WorkloadType workload, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM Baselines WHERE Workload = @Workload ORDER BY UpdatedAt DESC LIMIT 1;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Workload", workload.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new WorkloadBaseline
            {
                Id = Guid.Parse(reader.GetString(0)),
                ProfileName = reader.GetString(1),
                Workload = Enum.Parse<WorkloadType>(reader.GetString(2)),
                SampleCount = reader.GetInt32(3),
                AvgCpuTempC = reader.GetDouble(4),
                StdDevCpuTempC = reader.GetDouble(5),
                AvgCpuClockMhz = reader.GetDouble(6),
                AvgCpuPowerW = reader.GetDouble(7),
                AvgGpuTempC = reader.GetDouble(8),
                StdDevGpuTempC = reader.GetDouble(9),
                AvgGpuClockMhz = reader.GetDouble(10),
                AvgGpuPowerW = reader.GetDouble(11),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(12))
            };
        }

        return null;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
