# PC Sentinel — Software Subsystem & Live Demo Guide

> **Embedded PC Health, Diagnostics & Performance Optimization System**  
> *Final Year Project — Vellore Institute of Technology (VIT, Session 2025–2026)*  
> **Team Members:** Rejish Ragavan, Srinand Varun, Pranav Narayanan S  

---

## 1. Executive Summary & Architecture

PC Sentinel is an autonomous hardware monitoring, root-cause diagnostic, and performance optimization engine. The software operates independently as a full-featured Windows desktop suite, while outputting real-time telemetry packets over USB Serial/Virtual Loopback for the external ESP32-S3 hardware Sentinel Node.

### Layered Architecture
```
PCSentinel.sln
├── src/
│   ├── PCSentinel.Core/           # Domain models, enum definitions & core interfaces
│   ├── PCSentinel.Hardware/       # LibreHardwareMonitorLib (ring-0 driver) + High-fidelity simulator
│   ├── PCSentinel.Storage/        # SQLite WAL database engine (Microsoft.Data.Sqlite)
│   ├── PCSentinel.Analysis/       # Rule engine, baseline learner, correlation engine & OC Lab
│   ├── PCSentinel.Diagnostics/    # Win32 PnP inspector, Event Log TDR, Boot transition & Black Box
│   ├── PCSentinel.TestCli/        # Full CLI presentation runner with scenario triggers
│   └── PCSentinel.App/            # Modern WPF MVVM desktop dashboard with live sparklines & controls
└── tests/
    └── PCSentinel.Core.Tests/     # Comprehensive xUnit test suite (100% test coverage)
```

---

## 2. Quick Start: Running the Software Demo

You can run the demo in two ways:
1. **Interactive Graphical Desktop Dashboard (WPF Application)**: Best for visual presentation, showing real-time sparkline curves, metric gauges, and clickable scenario buttons.
2. **Test CLI Presentation Runner**: Best for terminal demonstrations, showing high-speed streaming metrics, SQLite persistence, and standalone auto-overclocking lab runs.

---

### Option A: Running the WPF Desktop Dashboard (`PCSentinel.App`)

#### Requirements:
- Windows 10/11
- .NET 8.0 SDK or .NET 10.0 SDK
- Visual Studio 2022 (with *.NET desktop development* workload) or VS Code

#### Steps to Launch:
1. Open `PCSentinel.sln` in **Visual Studio 2022**.
2. In the Solution Explorer, right-click **`PCSentinel.App`** and select **Set as Startup Project**.
3. Press **F5** (Debug) or **Ctrl+F5** (Run without debugging).
   > *Note on Admin Privileges*: If launched with Administrator privileges, PC Sentinel unpacks the `WinRing0` kernel driver and polls your actual CPU/GPU/motherboard sensors. If run without admin rights or on non-supported hardware, it automatically activates the high-fidelity simulator so you can demonstrate all features without hardware constraints!

#### Interactive Presentation Controls (in the App):
Located directly beneath the top header:
- 🟢 **Normal Gaming**: Sets active workload to heavy 3D gaming (68–74°C, 1845 MHz, 95% GPU load), demonstrating green health score (95–100/100) and baseline conformity.
- 🔴 **Thermal Throttle**: Injects thermal throttling (GPU hotspot reaches 86°C, core clock downclocks to 1420 MHz). Watch the health score drop, explainable deductions appear, and a root-cause diagnostic event pop up in real-time.
- ⚡ **Run 3D Benchmark**: Simulates a synthetic 10-second graphics benchmark, measuring real-time FPS, score, clock stability, and power draw.
- 🚀 **Auto-OC Boost Experiment**: Evaluates safe thermal & power headroom, calculates a +85 MHz core boost, runs stability validation, and records performance gains (+4.8% FPS) to the experiment log.
- 📦 **Capture Black Box**: Captures a snapshot of the rolling 30-second circular telemetry buffer into SQLite, recording pre-event conditions for post-mortem crash analysis.

#### Lower Multi-View Deck:
- **Tab 1: ROOT-CAUSE DIAGNOSTICS & ANOMALIES**: Shows explainable deductions (e.g., `-18 pts: GPU Thermal Throttling`), diagnostic events, confidence percentage, and actionable recommendations.
- **Tab 2: OC LAB & HEADROOM ANALYZER**: Shows thermal/power headroom level, safe boost recommendations, and historical overclocking experiments.
- **Tab 3: BLACK BOX INCIDENTS**: Displays all recorded pre-crash telemetry windows with incident numbers and UTC timestamps.
- **Tab 4: STORAGE & SMART HEALTH**: Displays PCIe NVMe SSD health, wear percentage, total TBW, power-on hours, reallocated sectors, and degradation risk.

---

### Option B: Running the Command-Line Runner (`PCSentinel.TestCli`)

The CLI runner provides instant terminal demonstration with rich ASCII color formatting and specific scenario flags.

```bash
# 1. Standard Live Telemetry Stream (High-Load Gaming Simulation)
dotnet run --project src/PCSentinel.TestCli -- --simulate

# 2. Thermal Throttling Anomaly Demonstration
# Demonstrates root-cause correlation: Temp > 85°C + Clock drop + High Load -> Thermal Limitation
dotnet run --project src/PCSentinel.TestCli -- --simulate --throttle

# 3. Dedicated OC Lab Auto-Overclocking Demonstration
# Calculates safe headroom, estimates +95 MHz boost, runs controlled experiment & stability check
dotnet run --project src/PCSentinel.TestCli -- --oc-lab

# 4. Driver Failure Demonstration (Simulates PnP Code 43 & nvlddmkm TDR reset)
dotnet run --project src/PCSentinel.TestCli -- --broken-driver

# 5. Boot Delay & Regression Analysis (Simulates slow startup phase breakdown)
dotnet run --project src/PCSentinel.TestCli -- --boot-regression

# 6. Immediate Black Box Incident Trigger
dotnet run --project src/PCSentinel.TestCli -- --simulate --trigger-incident

# 7. Live Windows Hardware (Requires Elevated Command Prompt / Run as Administrator)
dotnet run --project src/PCSentinel.TestCli
```

---

## 3. Step-by-Step Evaluator Presentation Script (5-Minute Demo)

Use this script when presenting to evaluators, professors, or during project reviews:

| Stage | Action / Command | What to Explain to Evaluators |
| :--- | :--- | :--- |
| **1. Ingestion & Workload** | Launch `PCSentinel.App` or `dotnet run --project src/PCSentinel.TestCli -- --simulate` | *"PC Sentinel collects multi-metric telemetry (temperature, clocks, power, load, RAM) every second. Our classifier dynamically tags the workload (e.g., Gaming vs Idle) and uses Welford's streaming algorithm to learn the PC's baseline distribution."* |
| **2. Explainable Scoring** | Look at the Top Health Badge | *"Instead of opaque scores, our health engine evaluates deterministic rules. The overall score (0–100) is backed by an itemized deduction breakdown: every single point deducted is explained with exact evidence."* |
| **3. Root-Cause Diagnosis** | Click **🔴 Thermal Throttle** or run `--throttle` | *"Notice the temperature spikes to 86°C and the clock downclocks to 1420 MHz. The engine correlates high load + high temp + falling clocks to diagnose thermal throttling with 94% confidence, suggesting fan profile tuning."* |
| **4. Auto-OC Lab** | Click **🚀 Auto-OC Boost Experiment** or run `--oc-lab` | *"The OC Lab calculates thermal and power headroom (current 68°C vs 80°C TjMax gives +12°C margin). It calculates a safe boost of +85 MHz, validates thermal stability, and logs a verified +4.8% FPS improvement to SQLite."* |
| **5. Boot & Driver Health** | Switch to Tab 4 or check the CLI summary | *"We inspect Win32 PnP drivers (detecting Code 43 / nvlddmkm TDR resets), evaluate storage wear via SMART attributes (reallocated sectors and TBW), and measure OS boot phase delays."* |
| **6. Black Box Recorder** | Click **📦 Capture Black Box** or view Tab 3 | *"If a critical thermal spike or crash happens, our continuous circular ring buffer locks the last 30 seconds of telemetry and persists it to SQLite for post-mortem diagnosis."* |
| **7. Sentinel Hardware Node** | Point out the Sentinel Node status | *"All telemetry is serialized into lightweight JSON packets (`SentinelPacket`) and transmitted over USB Serial to our external ESP32-S3 node for independent HUD display and acoustic alerts."* |

---

## 4. SQLite Telemetry Database

All telemetry, incidents, and experiment runs are stored with WAL (Write-Ahead Logging) mode enabled for high-concurrency logging:

- **Location**:
  - Desktop App: `%LOCALAPPDATA%\PCSentinel\sentinel_telemetry.db`
  - CLI Runner: `./sentinel_history.db`
- **Core Tables**:
  - `Telemetry`: High-frequency sensor samples indexed on timestamp.
  - `HealthEvents`: Correlated diagnostic events with severity, confidence, and recommendations.
  - `HealthScores`: Historical overall and component scores with explainable deduction breakdowns.
  - `Baselines`: Statistical baseline parameters ($\mu$ and $\sigma$ for temperatures and frequencies).
  - `BootSessions`: Boot phase timing analysis and regression flags.
  - `Incidents`: Black box crash recordings containing full 30-second pre-event telemetry windows.
  - `Benchmarks`: Synthetic 3D benchmark results (FPS, GPU score, clock, temperatures).
  - `OCExperiments`: History of auto-overclocking tuning runs with stability and FPS gain metrics.

---

## 5. Automated Test Suite

All algorithms and subsystems are verified with unit tests:

```bash
dotnet test tests/PCSentinel.Core.Tests
```

### Covered Test Suites:
- `HealthAnalyzerTests`: Verifies explainable deductions and score calculation logic.
- `WorkloadClassifierTests`: Verifies multi-signal classification thresholds.
- `BaselineLearnerTests`: Verifies Welford online algorithm mean and variance calculations.
- `MultiSignalCorrelationTests`: Verifies thermal throttling vs power bottleneck root-cause detection.
- `OcAnalyzerTests`: Verifies headroom estimation and auto-OC stability benchmarks.
- `StorageAnalyzerTests`: Verifies SMART attribute parsing and degradation risk evaluation.
- `BootAnalyzerTests`: Verifies startup phase timing and regression detection.
- `IncidentRecorderTests`: Verifies rolling circular ring buffer capture and SQLite persistence.
- `PnpInspectorTests`: Verifies PnP device status codes and error formatting.
- `SqliteStoreTests`: Verifies database migrations, concurrent writes, and query filters.
