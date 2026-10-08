# PC Sentinel — Software Subsystem

> **Embedded PC Health, Diagnostics & Performance Optimization System**  
> *Final Year Project — VIT (Session 2025–2026)*  
> **Team:** Rejish Ragavan, Srinand Varun, Pranav Narayanan S  

---

## 1. Solution Architecture

PC Sentinel is designed with a strict layered architecture to decouple sensor drivers from UI and diagnostics:

```
PCSentinel.sln
├── src/
│   ├── PCSentinel.Core/           # Domain models (SensorReading, TelemetrySample, HealthEvent, HealthScore) & interfaces
│   ├── PCSentinel.Hardware/       # LibreHardwareMonitorLib integration + SimulatedSensorProvider fallback
│   ├── PCSentinel.Storage/        # SQLite WAL storage engine (Microsoft.Data.Sqlite)
│   ├── PCSentinel.Analysis/       # Deterministic rule engine, personalized baseline comparison & explainable scoring
│   ├── PCSentinel.TestCli/        # Milestone 1 verification console harness (real-time telemetry proof)
│   └── PCSentinel.App/            # Modern WPF desktop dashboard (MVVM, CommunityToolkit.Mvvm)
└── tests/
    └── PCSentinel.Core.Tests/     # xUnit unit & integration tests
```

---

## 2. Milestone 1: Hardware Telemetry Proof

Before running the full WPF UI, Milestone 1 verifies that telemetry samples are polled, analyzed, displayed in real-time, and persisted to SQLite.

### Running Milestone 1:

#### On Windows (Live Hardware with LibreHardwareMonitor):
Open an **Elevated Command Prompt / PowerShell (Run as Administrator)**:
```bash
dotnet run --project src/PCSentinel.TestCli
```
*(Administrator elevation is required for LibreHardwareMonitorLib to unpack and load the ring-0 kernel driver for CPU TjMax, GPU VRM, and fan speed sensors).*

#### In Simulated / Cross-Platform Mode (Development & Testing):
```bash
# Standard simulated gaming workload
dotnet run --project src/PCSentinel.TestCli -- --simulate

# Simulate thermal throttling incident (triggers warnings, clock downclocking, and health deductions)
dotnet run --project src/PCSentinel.TestCli -- --simulate --throttle
```

---

## 3. SQLite Database Schema

Located by default at `%LOCALAPPDATA%\PCSentinel\sentinel_telemetry.db` (or alongside the CLI runner):
- `Telemetry`: High-throughput time-series metrics (`CpuTemp`, `CpuClock`, `GpuTemp`, `GpuClock`, `RamUsedGb`, etc.) indexed on timestamp.
- `HealthEvents`: Diagnostic events with evidence, root-cause explanations, and confidence scores.
- `HealthScores`: Historical component-level and overall scores with explainable deduction breakdowns.
- `Baselines`: Workload profiles (e.g., Idle, Gaming) with mean and standard deviation for comparison.

---

## 4. Running the WPF Desktop Dashboard

Open `PCSentinel.sln` in **Visual Studio 2022** (.NET 8 or 10 installed) on Windows:
1. Set `PCSentinel.App` as the Startup Project.
2. Build and launch with Debugging (`F5`).
3. If run as Administrator, it polls live hardware; otherwise, it seamlessly falls back to simulation mode with HUD alert controls.

---

## 5. Running the Automated Tests

```bash
dotnet test tests/PCSentinel.Core.Tests
```
