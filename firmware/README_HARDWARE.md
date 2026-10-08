# ESP32-S3 Sentinel Node — Hardware Wiring & Flashing Guide

> **PC Sentinel Physical Telemetry & Alert Node**  
> Companion embedded microcontroller for the PC Sentinel software suite.

---

## 1. Component Bill of Materials (BOM)

| Component | Specification | Purpose |
| :--- | :--- | :--- |
| **Microcontroller** | ESP32-S3 DevKitC-1 (N8R8 / N16R8) | USB-CDC serial receiver, display & LED driver |
| **Display** | 1.3" I2C Monochrome OLED (128×64, SH1106 / SSD1306) | Displays real-time Health Score, thermals, and alert HUD |
| **RGB Status Indicator** | WS2812B NeoPixel (Built-in GPIO 48 or external) | Visual status (Green = Nominal, Amber = Warning, Red = Critical) |
| **Acoustic Beeper** | 5V / 3.3V Active Piezo Buzzer | Audible alarm on thermal throttle or crash events |
| **Navigation Button** | Push Button (or built-in BOOT button GPIO 0) | Cycles display pages (HUD -> Thermals -> Alert Diagnostic) |
| **Interface Cable** | USB-C to USB-A data cable | Transmits JSON telemetry frames and powers the node |

---

## 2. GPIO Pinout & Wiring Table

Connect your components to the ESP32-S3 DevKit as follows:

```
 ESP32-S3 DevKit              Peripheral
┌─────────────────┐         ┌────────────────────────┐
│             3V3 ├─────────┤ VCC (OLED & Buzzer)    │
│             GND ├─────────┤ GND (All devices)      │
│         GPIO 21 ├─────────┤ SDA (1.3" I2C OLED)    │
│         GPIO 22 ├─────────┤ SCL (1.3" I2C OLED)    │
│         GPIO 48 ├─────────┤ DIN (NeoPixel RGB LED) │
│         GPIO 14 ├─────────┤ Positive (+) of Buzzer │
│          GPIO 0 ├─────────┤ BOOT / Page Cycle btn  │
└─────────────────┘         └────────────────────────┘
```

---

## 3. How to Flash the Firmware

### Option A: Using PlatformIO (Recommended)
1. Open Visual Studio Code with the **PlatformIO IDE** extension installed.
2. Open the `firmware/` folder in VS Code.
3. Connect your ESP32-S3 board to your PC via USB-C.
4. Click the **PlatformIO: Upload** arrow icon in the bottom status bar (or run `pio run --target upload`).
5. Open the Serial Monitor at **115200 baud** to confirm:
   ```
   [INIT] PC SENTINEL HARDWARE NODE ONLINE
   ```

### Option B: Using Arduino IDE
1. In the Arduino IDE Library Manager, install:
   - `ArduinoJson` (by Benoit Blanchon, v7.x)
   - `Adafruit SSD1306` (by Adafruit)
   - `Adafruit GFX Library` (by Adafruit)
   - `Adafruit NeoPixel` (by Adafruit)
2. Select Board: **ESP32S3 Dev Module**.
3. Enable USB CDC on Boot: **Tools -> USB CDC On Boot -> Enabled**.
4. Open `firmware/src/main.cpp`, verify, and click **Upload**.

---

## 4. Connecting to PC Sentinel Software

1. Once flashed, plug the ESP32-S3 into your Windows PC.
2. In Windows **Device Manager**, note the COM port under *Ports (COM & LPT)* (e.g. `COM5`).
3. Run the PC Sentinel CLI:
   ```bash
   dotnet run --project src/PCSentinel.TestCli -- --simulate --com=COM5
   ```
4. Or launch the WPF App `PCSentinel.App`—it will stream telemetry to the node, rendering the live Health Score, CPU/GPU temperatures, and glowing Green/Yellow/Red LED!
