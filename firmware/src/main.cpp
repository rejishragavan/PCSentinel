#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <Adafruit_NeoPixel.h>
#include <ArduinoJson.h>

// ==============================================================================
//                         PC SENTINEL — FIRMWARE v2.0                          //
//               ESP32-S3 Hardware Health & Diagnostics Node                     //
//    Features: Process Culprit Attribution + Bidirectional Auto-Overclocking   //
// ==============================================================================

#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
#define OLED_RESET    -1
#define OLED_ADDR     0x3C

// Hardware Pin Configuration (ESP32-S3 DevKitC-1)
#define PIN_OLED_SDA  21
#define PIN_OLED_SCL  22
#define PIN_NEOPIXEL  48  // Built-in WS2812 RGB LED
#define NUM_PIXELS    1
#define PIN_BUZZER    14  // Piezo Buzzer pin (Active high)
#define PIN_BUTTON    0   // Boot button (GPIO 0, active LOW with internal pull-up)

Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);
Adafruit_NeoPixel pixel(NUM_PIXELS, PIN_NEOPIXEL, NEO_GRB + NEO_KHZ800);

// Telemetry State Cache
struct SentinelState {
    int score = 100;
    float cpuTemp = 0.0f;
    float gpuTemp = 0.0f;
    float cpuLoad = 0.0f;
    float gpuLoad = 0.0f;
    char workload[24] = "IDLE";
    char ledColor[16] = "GREEN";
    bool triggerBuzzer = false;
    char alertTitle[48] = "";
    char alertSeverity[16] = "Info";
    bool hasActiveAlert = false;
    char culprit[32] = "None (Idle)";
    float culpritCpu = 0.0f;
    char ocMode[32] = "Stock Baseline";
    unsigned long lastPacketTime = 0;
};

// Hardware Overclocking State
struct LastOcAck {
    bool received = false;
    char target[16] = "GPU";
    char status[24] = "STABLE";
    float gain = 0.0f;
    char message[48] = "";
    unsigned long timestamp = 0;
};

SentinelState g_state;
LastOcAck g_lastOcAck;

// Screen indexing:
// 0 = HUD Overview
// 1 = Thermals & Frequencies
// 2 = Culprit Task Attribution (Clock Spikes & Throttling)
// 3 = Overclocking Lab Menu (Select & Trigger CPU, GPU, RAM)
int g_currentScreen = 0;
const int TOTAL_SCREENS = 4;

int g_selectedOcTarget = 0; // 0 = GPU, 1 = CPU, 2 = RAM, 3 = ALL
const char* const OC_TARGET_NAMES[] = { "gpu", "cpu", "ram", "all" };
const char* const OC_TARGET_LABELS[] = { "GPU Core (+85M)", "CPU All-Core (+150M)", "RAM DDR5 (XMP 3.0)", "ALL Components" };

// Button Interaction State Machine (Short click vs Long press hold)
unsigned long g_buttonPressStartTime = 0;
bool g_buttonIsPressed = false;
bool g_longPressHandled = false;
const unsigned long LONG_PRESS_THRESHOLD_MS = 900; // Hold 0.9s to trigger Overclock

void setupHardware();
void parseSerialTelemetry();
void triggerHardwareOverclock(const char* target);
void updateDisplay();
void updateLed();
void handleBuzzer();
void drawScreenHUD();
void drawScreenThermals();
void drawScreenCulprits();
void drawScreenOcMenu();
void drawScreenDisconnected();

void setup() {
    Serial.begin(115200);
    delay(500);

    setupHardware();
    g_state.lastPacketTime = 0;
}

void loop() {
    // 1. Read Serial Packets (Telemetry or OC Acknowledgement)
    parseSerialTelemetry();

    // 2. Handle Button (Short Press: navigate/cycle, Long Press: trigger Overclock)
    int reading = digitalRead(PIN_BUTTON);
    if (reading == LOW && !g_buttonIsPressed) {
        g_buttonIsPressed = true;
        g_buttonPressStartTime = millis();
        g_longPressHandled = false;
    }
    else if (reading == LOW && g_buttonIsPressed) {
        // Button held down
        if (!g_longPressHandled && (millis() - g_buttonPressStartTime >= LONG_PRESS_THRESHOLD_MS)) {
            g_longPressHandled = true;
            if (g_currentScreen == 3) {
                // Trigger Hardware Overclock on selected target!
                triggerHardwareOverclock(OC_TARGET_NAMES[g_selectedOcTarget]);
            }
        }
    }
    else if (reading == HIGH && g_buttonIsPressed) {
        // Button released
        unsigned long pressDuration = millis() - g_buttonPressStartTime;
        g_buttonIsPressed = false;

        if (!g_longPressHandled && pressDuration > 40) {
            // Short press
            if (g_currentScreen == 3) {
                // In OC Menu: advance target cursor
                g_selectedOcTarget = (g_selectedOcTarget + 1) % 4;
            } else {
                // In other screens: cycle screens
                g_currentScreen = (g_currentScreen + 1) % TOTAL_SCREENS;
            }
        }
    }

    // 3. Update Visuals & Acoustics
    updateDisplay();
    updateLed();
    handleBuzzer();

    delay(20);
}

void setupHardware() {
    // Initialize I2C OLED
    Wire.begin(PIN_OLED_SDA, PIN_OLED_SCL);
    if (display.begin(SSD1306_SWITCHCAPVCC, OLED_ADDR)) {
        display.clearDisplay();
        display.setTextColor(SSD1306_WHITE);
        display.setTextSize(1);
        display.setCursor(18, 12);
        display.println("PC SENTINEL");
        display.setCursor(12, 28);
        display.println("HARDWARE NODE");
        display.setCursor(6, 44);
        display.println("Waiting for USB host...");
        display.display();
    }

    // Initialize NeoPixel
    pixel.begin();
    pixel.setBrightness(40);
    pixel.setPixelColor(0, pixel.Color(0, 0, 150)); // Initializing Blue
    pixel.show();

    // Initialize Buzzer & Button
    pinMode(PIN_BUZZER, OUTPUT);
    digitalWrite(PIN_BUZZER, LOW);
    pinMode(PIN_BUTTON, INPUT_PULLUP);
}

void parseSerialTelemetry() {
    if (Serial.available() > 0) {
        String line = Serial.readStringUntil('\n');
        line.trim();
        if (line.length() == 0) return;

        JsonDocument doc;
        DeserializationError err = deserializeJson(doc, line);
        if (err) return; // Ignore malformed frames

        const char* type = doc["type"] | "status";
        g_state.lastPacketTime = millis();

        if (strcmp(type, "status") == 0) {
            g_state.score = doc["score"] | 100;
            g_state.cpuTemp = doc["cpu_t"] | doc["cpuTemp"] | 0.0f;
            g_state.gpuTemp = doc["gpu_t"] | doc["gpuTemp"] | 0.0f;
            g_state.cpuLoad = doc["cpu_l"] | doc["cpuLoad"] | 0.0f;
            g_state.gpuLoad = doc["gpu_l"] | doc["gpuLoad"] | 0.0f;
            strncpy(g_state.workload, doc["workload"] | "ACTIVE", sizeof(g_state.workload) - 1);
            strncpy(g_state.ledColor, doc["led"] | doc["ledColor"] | "GREEN", sizeof(g_state.ledColor) - 1);
            g_state.triggerBuzzer = doc["beep"] | doc["triggerBuzzer"] | false;

            if (doc.containsKey("culprit")) {
                strncpy(g_state.culprit, doc["culprit"] | "None (Idle)", sizeof(g_state.culprit) - 1);
            }
            g_state.culpritCpu = doc["culprit_cpu"] | 0.0f;
            if (doc.containsKey("oc_mode")) {
                strncpy(g_state.ocMode, doc["oc_mode"] | "Stock", sizeof(g_state.ocMode) - 1);
            }
            g_state.hasActiveAlert = false;
        }
        else if (strcmp(type, "oc_ack") == 0) {
            g_lastOcAck.received = true;
            strncpy(g_lastOcAck.target, doc["target"] | "GPU", sizeof(g_lastOcAck.target) - 1);
            strncpy(g_lastOcAck.status, doc["status"] | "STABLE", sizeof(g_lastOcAck.status) - 1);
            g_lastOcAck.gain = doc["gain"] | 0.0f;
            strncpy(g_lastOcAck.message, doc["msg"] | "Boost Applied", sizeof(g_lastOcAck.message) - 1);
            g_lastOcAck.timestamp = millis();

            // Double beep on OC Acknowledgment
            digitalWrite(PIN_BUZZER, HIGH);
            delay(90);
            digitalWrite(PIN_BUZZER, LOW);
            delay(50);
            digitalWrite(PIN_BUZZER, HIGH);
            delay(110);
            digitalWrite(PIN_BUZZER, LOW);
        }
        else if (strcmp(type, "alert") == 0) {
            g_state.score = doc["score"] | 70;
            strncpy(g_state.alertTitle, doc["title"] | doc["alert_title"] | "Hardware Anomaly", sizeof(g_state.alertTitle) - 1);
            strncpy(g_state.alertSeverity, doc["severity"] | "Warning", sizeof(g_state.alertSeverity) - 1);
            strncpy(g_state.ledColor, "RED", sizeof(g_state.ledColor) - 1);
            g_state.triggerBuzzer = true;
            g_state.hasActiveAlert = true;
        }
    }
}

void triggerHardwareOverclock(const char* target) {
    // Send command JSON packet to host over USB serial
    JsonDocument cmdDoc;
    cmdDoc["cmd"] = "overclock";
    cmdDoc["target"] = target;
    serializeJson(cmdDoc, Serial);
    Serial.println();

    // Immediate tactile feedback beep
    digitalWrite(PIN_BUZZER, HIGH);
    delay(70);
    digitalWrite(PIN_BUZZER, LOW);

    // NeoPixel purple pulse
    pixel.setPixelColor(0, pixel.Color(190, 0, 255));
    pixel.show();

    g_lastOcAck.received = true;
    strncpy(g_lastOcAck.target, target, sizeof(g_lastOcAck.target) - 1);
    strncpy(g_lastOcAck.status, "TUNING HOST...", sizeof(g_lastOcAck.status) - 1);
    g_lastOcAck.gain = 0.0f;
    g_lastOcAck.timestamp = millis();
}

void updateDisplay() {
    display.clearDisplay();

    // If host has not sent telemetry in > 3.5 seconds, display disconnected screen
    if (g_state.lastPacketTime == 0 || (millis() - g_state.lastPacketTime > 3500)) {
        drawScreenDisconnected();
    } else {
        switch (g_currentScreen) {
            case 0: drawScreenHUD(); break;
            case 1: drawScreenThermals(); break;
            case 2: drawScreenCulprits(); break;
            case 3: drawScreenOcMenu(); break;
            default: drawScreenHUD(); break;
        }
    }

    display.display();
}

void drawScreenHUD() {
    display.setTextSize(1);
    display.setCursor(0, 0);
    display.print("SENTINEL | ");
    display.print(g_state.workload);

    display.drawLine(0, 9, 127, 9, SSD1306_WHITE);

    // Health Score Big Gauge
    display.setCursor(4, 18);
    display.print("HEALTH SCORE");
    display.setTextSize(3);
    display.setCursor(4, 32);
    display.print(g_state.score);
    display.setTextSize(1);
    display.print("/100");

    // Mini Thermals on Right
    display.setCursor(80, 22);
    display.print("CPU:");
    display.print((int)g_state.cpuTemp);
    display.print("C");

    display.setCursor(80, 36);
    display.print("GPU:");
    display.print((int)g_state.gpuTemp);
    display.print("C");

    // Progress bar for score
    int barWidth = (g_state.score * 120) / 100;
    display.drawRect(4, 56, 120, 6, SSD1306_WHITE);
    display.fillRect(4, 56, barWidth, 6, SSD1306_WHITE);
}

void drawScreenThermals() {
    display.setTextSize(1);
    display.setCursor(0, 0);
    display.println("THERMALS & LOADS");
    display.drawLine(0, 9, 127, 9, SSD1306_WHITE);

    display.setCursor(0, 16);
    display.print("CPU Temp: ");
    display.print(g_state.cpuTemp, 1);
    display.print(" C");

    display.setCursor(0, 28);
    display.print("CPU Load: ");
    display.print((int)g_state.cpuLoad);
    display.print(" %");

    display.setCursor(0, 40);
    display.print("GPU Temp: ");
    display.print(g_state.gpuTemp, 1);
    display.print(" C");

    display.setCursor(0, 52);
    display.print("GPU Load: ");
    display.print((int)g_state.gpuLoad);
    display.print(" %");
}

void drawScreenCulprits() {
    display.setTextSize(1);
    display.setCursor(0, 0);
    display.println("CULPRIT PROCESSES");
    display.drawLine(0, 9, 127, 9, SSD1306_WHITE);

    if (strstr(g_state.culprit, "None") != NULL) {
        display.setCursor(0, 20);
        display.println("System Nominal");
        display.setCursor(0, 34);
        display.println("No background tasks");
        display.setCursor(0, 46);
        display.println("causing clock drops.");
    } else {
        display.setCursor(0, 16);
        display.print("SUSPECT TASK:");
        display.setCursor(0, 28);
        display.println(g_state.culprit);

        display.setCursor(0, 44);
        display.print("CPU Usage: ");
        display.print(g_state.culpritCpu, 1);
        display.println("%");
        display.setCursor(0, 54);
        display.println("Attributed Throttle");
    }
}

void drawScreenOcMenu() {
    display.setTextSize(1);
    display.setCursor(0, 0);
    display.println("OC LAB MENU [HOLD]");
    display.drawLine(0, 9, 127, 9, SSD1306_WHITE);

    // If an ack was recently received, render confirmation banner
    if (g_lastOcAck.received && (millis() - g_lastOcAck.timestamp < 3500)) {
        display.setCursor(0, 15);
        display.print(">> APPLIED: ");
        display.println(g_lastOcAck.target);
        display.setCursor(0, 28);
        display.print("Gain: +");
        display.print(g_lastOcAck.gain, 1);
        display.println("% FPS");
        display.setCursor(0, 42);
        display.println(g_lastOcAck.status);
        display.setCursor(0, 54);
        display.println(g_lastOcAck.message);
        return;
    }

    for (int i = 0; i < 4; i++) {
        int y = 14 + (i * 12);
        display.setCursor(0, y);
        if (i == g_selectedOcTarget) {
            display.print("> ");
        } else {
            display.print("  ");
        }
        display.print(OC_TARGET_LABELS[i]);
    }
}

void drawScreenDisconnected() {
    display.setTextSize(1);
    display.setCursor(20, 10);
    display.println("PC SENTINEL");
    display.drawLine(0, 22, 127, 22, SSD1306_WHITE);

    display.setCursor(6, 32);
    display.println("HOST DISCONNECTED");
    display.setCursor(2, 48);
    display.println("Waiting for USB link...");
}

void updateLed() {
    if (g_state.lastPacketTime == 0 || (millis() - g_state.lastPacketTime > 3500)) {
        // Pulsing Blue when disconnected
        int brightness = (sin(millis() / 300.0) + 1.0) * 40;
        pixel.setPixelColor(0, pixel.Color(0, 0, brightness));
    }
    else if (g_lastOcAck.received && (millis() - g_lastOcAck.timestamp < 3500)) {
        // Cyan / Purple celebratory pulse on Overclock execution
        pixel.setPixelColor(0, pixel.Color(160, 32, 240));
    }
    else if (strcmp(g_state.ledColor, "RED") == 0) {
        // Red flashing on throttle or crash
        bool flash = (millis() / 250) % 2;
        pixel.setPixelColor(0, flash ? pixel.Color(255, 0, 0) : pixel.Color(60, 0, 0));
    }
    else if (strcmp(g_state.ledColor, "YELLOW") == 0) {
        pixel.setPixelColor(0, pixel.Color(240, 160, 0)); // Amber warning
    }
    else {
        pixel.setPixelColor(0, pixel.Color(0, 220, 20));  // Emerald green nominal
    }
    pixel.show();
}

void handleBuzzer() {
    static unsigned long buzzerStartTime = 0;
    static bool buzzerActive = false;

    if (g_state.triggerBuzzer && !buzzerActive) {
        buzzerActive = true;
        buzzerStartTime = millis();
        digitalWrite(PIN_BUZZER, HIGH);
    }

    if (buzzerActive && (millis() - buzzerStartTime > 300)) {
        digitalWrite(PIN_BUZZER, LOW);
        buzzerActive = false;
        g_state.triggerBuzzer = false; // Reset trigger
    }
}
