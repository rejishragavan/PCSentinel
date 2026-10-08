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

            CREATE TABLE IF NOT EXISTS BootSessions (
                Id TEXT PRIMARY KEY,
                BootId INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                TotalBootDurationMs REAL NOT NULL,
                MainPathBootDurationMs REAL NOT NULL,
                DriverInitDurationMs REAL NOT NULL,
                PostBootDurationMs REAL NOT NULL,
                SmssInitDurationMs REAL NOT NULL,
                IsRegression INTEGER NOT NULL,
                RegressionExplanation TEXT,
                SlowestSubsystem TEXT
            );

            CREATE INDEX IF NOT EXISTS IX_BootSessions_Timestamp ON BootSessions (Timestamp DESC);

            CREATE TABLE IF NOT EXISTS Incidents (
                Id TEXT PRIMARY KEY,
                IncidentNumber INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                TriggerReason TEXT NOT NULL,
                Severity TEXT NOT NULL,
                HealthScore INTEGER NOT NULL,
                PreEventJson TEXT NOT NULL,
                PostEventJson TEXT NOT NULL,
                CorrelatedEventsJson TEXT NOT NULL,
                DegradedDriversJson TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Incidents_Timestamp ON Incidents (Timestamp DESC);

            CREATE TABLE IF NOT EXISTS Benchmarks (
                Id TEXT PRIMARY KEY,
                BenchmarkName TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                DurationSeconds INTEGER NOT NULL,
                Score INTEGER NOT NULL,
                AverageFps REAL NOT NULL,
                AvgCpuTemp REAL NOT NULL,
                AvgGpuTemp REAL NOT NULL,
                AvgGpuClock REAL NOT NULL,
                PeakPower REAL NOT NULL,
                IsOverclocked INTEGER NOT NULL,
                ClockOffsetMhz REAL NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Benchmarks_Timestamp ON Benchmarks (Timestamp DESC);

            CREATE TABLE IF NOT EXISTS OCExperiments (
                Id TEXT PRIMARY KEY,
                ExperimentNumber INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                SettingLabel TEXT NOT NULL,
                GpuClockOffsetMhz REAL NOT NULL,
                GpuVoltageOffsetMv REAL NOT NULL,
                StabilityPassed INTEGER NOT NULL,
                BaselineFps REAL NOT NULL,
                ExperimentFps REAL NOT NULL,
                PerformanceGainPercent REAL NOT NULL,
                MaxTempReachedC REAL NOT NULL,
                Notes TEXT
            );

            CREATE INDEX IF NOT EXISTS IX_OCExperiments_Timestamp ON OCExperiments (Timestamp DESC);

            CREATE TABLE IF NOT EXISTS RecoveryAudits (
                Id TEXT PRIMARY KEY,
                Action TEXT NOT NULL,
                CommandExecuted TEXT NOT NULL,
                IsSuccessful INTEGER NOT NULL,
                ExitCode INTEGER NOT NULL,
                Summary TEXT NOT NULL,
                OutputLog TEXT NOT NULL,
                DurationMs REAL NOT NULL,
                ExecutedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_RecoveryAudits_ExecutedAt ON RecoveryAudits (ExecutedAt DESC);
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

    public async Task StoreBootSessionAsync(BootSession session, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO BootSessions (
                Id, BootId, Timestamp, TotalBootDurationMs, MainPathBootDurationMs,
                DriverInitDurationMs, PostBootDurationMs, SmssInitDurationMs,
                IsRegression, RegressionExplanation, SlowestSubsystem
            ) VALUES (
                @Id, @BootId, @Timestamp, @TotalBoot, @MainPath,
                @DriverInit, @PostBoot, @SmssInit,
                @IsRegression, @RegressionExplanation, @SlowestSubsystem
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", session.Id.ToString());
        cmd.Parameters.AddWithValue("@BootId", session.BootId);
        cmd.Parameters.AddWithValue("@Timestamp", session.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@TotalBoot", session.TotalBootDurationMs);
        cmd.Parameters.AddWithValue("@MainPath", session.MainPathBootDurationMs);
        cmd.Parameters.AddWithValue("@DriverInit", session.DriverInitDurationMs);
        cmd.Parameters.AddWithValue("@PostBoot", session.PostBootDurationMs);
        cmd.Parameters.AddWithValue("@SmssInit", session.SmssInitDurationMs);
        cmd.Parameters.AddWithValue("@IsRegression", session.IsRegression ? 1 : 0);
        cmd.Parameters.AddWithValue("@RegressionExplanation", (object?)session.RegressionExplanation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SlowestSubsystem", session.SlowestSubsystem);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BootSession>> GetBootSessionsAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM BootSessions ORDER BY Timestamp DESC LIMIT @Count;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Count", count);

        var list = new List<BootSession>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new BootSession
            {
                Id = Guid.Parse(reader.GetString(0)),
                BootId = reader.GetInt32(1),
                Timestamp = DateTimeOffset.Parse(reader.GetString(2)),
                TotalBootDurationMs = reader.GetDouble(3),
                MainPathBootDurationMs = reader.GetDouble(4),
                DriverInitDurationMs = reader.GetDouble(5),
                PostBootDurationMs = reader.GetDouble(6),
                SmssInitDurationMs = reader.GetDouble(7),
                IsRegression = reader.GetInt32(8) == 1,
                RegressionExplanation = reader.IsDBNull(9) ? null : reader.GetString(9),
                SlowestSubsystem = reader.IsDBNull(10) ? "None" : reader.GetString(10)
            });
        }

        return list;
    }

    public async Task StoreIncidentAsync(IncidentReport incident, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO Incidents (
                Id, IncidentNumber, Timestamp, TriggerReason, Severity, HealthScore,
                PreEventJson, PostEventJson, CorrelatedEventsJson, DegradedDriversJson
            ) VALUES (
                @Id, @IncidentNumber, @Timestamp, @TriggerReason, @Severity, @HealthScore,
                @PreEventJson, @PostEventJson, @CorrelatedEventsJson, @DegradedDriversJson
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", incident.Id.ToString());
        cmd.Parameters.AddWithValue("@IncidentNumber", incident.IncidentNumber);
        cmd.Parameters.AddWithValue("@Timestamp", incident.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@TriggerReason", incident.TriggerReason);
        cmd.Parameters.AddWithValue("@Severity", incident.Severity.ToString());
        cmd.Parameters.AddWithValue("@HealthScore", incident.HealthScoreAtTrigger);
        cmd.Parameters.AddWithValue("@PreEventJson", System.Text.Json.JsonSerializer.Serialize(incident.PreEventWindow));
        cmd.Parameters.AddWithValue("@PostEventJson", System.Text.Json.JsonSerializer.Serialize(incident.PostEventWindow));
        cmd.Parameters.AddWithValue("@CorrelatedEventsJson", System.Text.Json.JsonSerializer.Serialize(incident.CorrelatedEvents));
        cmd.Parameters.AddWithValue("@DegradedDriversJson", System.Text.Json.JsonSerializer.Serialize(incident.DegradedDrivers));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncidentReport>> GetIncidentsAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM Incidents ORDER BY Timestamp DESC LIMIT @Count;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Count", count);

        var list = new List<IncidentReport>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var preJson = reader.GetString(6);
            var postJson = reader.GetString(7);
            var eventsJson = reader.GetString(8);
            var driversJson = reader.GetString(9);

            list.Add(new IncidentReport
            {
                Id = Guid.Parse(reader.GetString(0)),
                IncidentNumber = reader.GetInt64(1),
                Timestamp = DateTimeOffset.Parse(reader.GetString(2)),
                TriggerReason = reader.GetString(3),
                Severity = Enum.Parse<EventSeverity>(reader.GetString(4)),
                HealthScoreAtTrigger = reader.GetInt32(5),
                PreEventWindow = System.Text.Json.JsonSerializer.Deserialize<List<TelemetrySample>>(preJson) ?? new(),
                PostEventWindow = System.Text.Json.JsonSerializer.Deserialize<List<TelemetrySample>>(postJson) ?? new(),
                CorrelatedEvents = System.Text.Json.JsonSerializer.Deserialize<List<HealthEvent>>(eventsJson) ?? new(),
                DegradedDrivers = System.Text.Json.JsonSerializer.Deserialize<List<DriverInfo>>(driversJson) ?? new()
            });
        }

        return list;
    }

    public async Task StoreBenchmarkResultAsync(BenchmarkResult result, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO Benchmarks (
                Id, BenchmarkName, Timestamp, DurationSeconds, Score, AverageFps,
                AvgCpuTemp, AvgGpuTemp, AvgGpuClock, PeakPower, IsOverclocked, ClockOffsetMhz
            ) VALUES (
                @Id, @BenchmarkName, @Timestamp, @DurationSeconds, @Score, @AverageFps,
                @AvgCpuTemp, @AvgGpuTemp, @AvgGpuClock, @PeakPower, @IsOverclocked, @ClockOffsetMhz
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", result.Id.ToString());
        cmd.Parameters.AddWithValue("@BenchmarkName", result.BenchmarkName);
        cmd.Parameters.AddWithValue("@Timestamp", result.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@DurationSeconds", result.DurationSeconds);
        cmd.Parameters.AddWithValue("@Score", result.Score);
        cmd.Parameters.AddWithValue("@AverageFps", result.AverageFps);
        cmd.Parameters.AddWithValue("@AvgCpuTemp", result.AvgCpuTempC);
        cmd.Parameters.AddWithValue("@AvgGpuTemp", result.AvgGpuTempC);
        cmd.Parameters.AddWithValue("@AvgGpuClock", result.AvgGpuClockMhz);
        cmd.Parameters.AddWithValue("@PeakPower", result.PeakPowerWatts);
        cmd.Parameters.AddWithValue("@IsOverclocked", result.IsOverclocked ? 1 : 0);
        cmd.Parameters.AddWithValue("@ClockOffsetMhz", result.ClockOffsetMhz);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BenchmarkResult>> GetBenchmarkResultsAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM Benchmarks ORDER BY Timestamp DESC LIMIT @Count;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Count", count);

        var list = new List<BenchmarkResult>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new BenchmarkResult
            {
                Id = Guid.Parse(reader.GetString(0)),
                BenchmarkName = reader.GetString(1),
                Timestamp = DateTimeOffset.Parse(reader.GetString(2)),
                DurationSeconds = reader.GetInt32(3),
                Score = reader.GetInt32(4),
                AverageFps = reader.GetDouble(5),
                AvgCpuTempC = reader.GetDouble(6),
                AvgGpuTempC = reader.GetDouble(7),
                AvgGpuClockMhz = reader.GetDouble(8),
                PeakPowerWatts = reader.GetDouble(9),
                IsOverclocked = reader.GetInt32(10) == 1,
                ClockOffsetMhz = reader.GetDouble(11)
            });
        }

        return list;
    }

    public async Task StoreOcExperimentAsync(OcExperiment experiment, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO OCExperiments (
                Id, ExperimentNumber, Timestamp, SettingLabel, GpuClockOffsetMhz,
                GpuVoltageOffsetMv, StabilityPassed, BaselineFps, ExperimentFps,
                PerformanceGainPercent, MaxTempReachedC, Notes
            ) VALUES (
                @Id, @ExperimentNumber, @Timestamp, @SettingLabel, @GpuClockOffsetMhz,
                @GpuVoltageOffsetMv, @StabilityPassed, @BaselineFps, @ExperimentFps,
                @PerformanceGainPercent, @MaxTempReachedC, @Notes
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", experiment.Id.ToString());
        cmd.Parameters.AddWithValue("@ExperimentNumber", experiment.ExperimentNumber);
        cmd.Parameters.AddWithValue("@Timestamp", experiment.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@SettingLabel", experiment.SettingLabel);
        cmd.Parameters.AddWithValue("@GpuClockOffsetMhz", experiment.GpuClockOffsetMhz);
        cmd.Parameters.AddWithValue("@GpuVoltageOffsetMv", experiment.GpuVoltageOffsetMv);
        cmd.Parameters.AddWithValue("@StabilityPassed", experiment.StabilityPassed ? 1 : 0);
        cmd.Parameters.AddWithValue("@BaselineFps", experiment.BaselineFps);
        cmd.Parameters.AddWithValue("@ExperimentFps", experiment.ExperimentFps);
        cmd.Parameters.AddWithValue("@PerformanceGainPercent", experiment.PerformanceGainPercent);
        cmd.Parameters.AddWithValue("@MaxTempReachedC", experiment.MaxTempReachedC);
        cmd.Parameters.AddWithValue("@Notes", experiment.Notes);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OcExperiment>> GetOcExperimentsAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM OCExperiments ORDER BY Timestamp DESC LIMIT @Count;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Count", count);

        var list = new List<OcExperiment>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new OcExperiment
            {
                Id = Guid.Parse(reader.GetString(0)),
                ExperimentNumber = reader.GetInt32(1),
                Timestamp = DateTimeOffset.Parse(reader.GetString(2)),
                SettingLabel = reader.GetString(3),
                GpuClockOffsetMhz = reader.GetDouble(4),
                GpuVoltageOffsetMv = reader.GetDouble(5),
                StabilityPassed = reader.GetInt32(6) == 1,
                BaselineFps = reader.GetDouble(7),
                ExperimentFps = reader.GetDouble(8),
                PerformanceGainPercent = reader.GetDouble(9),
                MaxTempReachedC = reader.GetDouble(10),
                Notes = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
            });
        }

        return list;
    }

    public async Task StoreRecoveryAuditAsync(RecoveryTaskResult task, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO RecoveryAudits (
                Id, Action, CommandExecuted, IsSuccessful, ExitCode,
                Summary, OutputLog, DurationMs, ExecutedAt
            ) VALUES (
                @Id, @Action, @CommandExecuted, @IsSuccessful, @ExitCode,
                @Summary, @OutputLog, @DurationMs, @ExecutedAt
            );
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", task.TaskId);
        cmd.Parameters.AddWithValue("@Action", task.Action.ToString());
        cmd.Parameters.AddWithValue("@CommandExecuted", task.CommandExecuted);
        cmd.Parameters.AddWithValue("@IsSuccessful", task.IsSuccessful ? 1 : 0);
        cmd.Parameters.AddWithValue("@ExitCode", task.ExitCode);
        cmd.Parameters.AddWithValue("@Summary", task.Summary);
        cmd.Parameters.AddWithValue("@OutputLog", task.OutputLog);
        cmd.Parameters.AddWithValue("@DurationMs", task.DurationMs);
        cmd.Parameters.AddWithValue("@ExecutedAt", task.ExecutedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecoveryTaskResult>> GetRecoveryAuditsAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = "SELECT * FROM RecoveryAudits ORDER BY ExecutedAt DESC LIMIT @Count;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Count", count);

        var list = new List<RecoveryTaskResult>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new RecoveryTaskResult
            {
                TaskId = reader.GetString(0),
                Action = Enum.TryParse<RecoveryActionType>(reader.GetString(1), out var act) ? act : RecoveryActionType.None,
                CommandExecuted = reader.GetString(2),
                IsSuccessful = reader.GetInt32(3) == 1,
                ExitCode = reader.GetInt32(4),
                Summary = reader.GetString(5),
                OutputLog = reader.GetString(6),
                DurationMs = reader.GetDouble(7),
                ExecutedAt = DateTime.Parse(reader.GetString(8))
            });
        }

        return list;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
