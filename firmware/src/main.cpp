#include <Arduino.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <Adafruit_NeoPixel.h>
#include <ArduinoJson.h>

// ==============================================================================
//                         PC SENTINEL — FIRMWARE v1.0                          //
//               ESP32-S3 Hardware Health & Diagnostics Node                     //
// ==============================================================================

#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
#define OLED_RESET    -1
#define OLED_ADDR     0x3C

// Hardware Pin Configuration (Customizable for target DevKit board)
#define PIN_OLED_SDA  21
#define PIN_OLED_SCL  22
#define PIN_NEOPIXEL  48  // Built-in RGB LED on ESP32-S3 DevKitC-1 (or external WS2812B)
#define NUM_PIXELS    1
#define PIN_BUZZER    14  // Piezo Buzzer pin (Active high)
#define PIN_BUTTON    0   // Boot button to cycle display pages

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
    unsigned long lastPacketTime = 0;
};

SentinelState g_state;
int g_currentScreen = 0; // 0 = HUD Overview, 1 = Thermals & Clocks, 2 = Diagnostics
const int TOTAL_SCREENS = 3;
bool g_lastButtonState = HIGH;
unsigned long g_lastDebounceTime = 0;

void setupHardware();
void parseSerialTelemetry();
void updateDisplay();
void updateLed();
void handleBuzzer();
void drawScreenHUD();
void drawScreenThermals();
void drawScreenAlerts();
void drawScreenDisconnected();

void setup() {
    Serial.begin(115200);
    delay(500);

    setupHardware();
    g_state.lastPacketTime = 0;
}

void loop() {
    // 1. Read Serial Packets
    parseSerialTelemetry();

    // 2. Handle Display Cycle Button
    int reading = digitalRead(PIN_BUTTON);
    if (reading != g_lastButtonState) {
        g_lastDebounceTime = millis();
    }
    if ((millis() - g_lastDebounceTime) > 50) {
        if (reading == LOW && g_lastButtonState == HIGH) {
            g_currentScreen = (g_currentScreen + 1) % TOTAL_SCREENS;
        }
    }
    g_lastButtonState = reading;

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
            g_state.cpuTemp = doc["cpuTemp"] | 0.0f;
            g_state.gpuTemp = doc["gpuTemp"] | 0.0f;
            g_state.cpuLoad = doc["cpuLoad"] | 0.0f;
            g_state.gpuLoad = doc["gpuLoad"] | 0.0f;
            strncpy(g_state.workload, doc["workload"] | "ACTIVE", sizeof(g_state.workload) - 1);
            strncpy(g_state.ledColor, doc["ledColor"] | "GREEN", sizeof(g_state.ledColor) - 1);
            g_state.triggerBuzzer = doc["triggerBuzzer"] | false;
            g_state.hasActiveAlert = false;
        } 
        else if (strcmp(type, "alert") == 0) {
            g_state.score = doc["score"] | 70;
            strncpy(g_state.alertTitle, doc["title"] | "Hardware Anomaly", sizeof(g_state.alertTitle) - 1);
            strncpy(g_state.alertSeverity, doc["severity"] | "Warning", sizeof(g_state.alertSeverity) - 1);
            strncpy(g_state.ledColor, doc["ledColor"] | "RED", sizeof(g_state.ledColor) - 1);
            g_state.triggerBuzzer = doc["triggerBuzzer"] | true;
            g_state.hasActiveAlert = true;
            g_currentScreen = 2; // Auto-jump to alert page
        }
    }
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
            case 2: drawScreenAlerts(); break;
            default: drawScreenHUD(); break;
        }
    }

    display.display();
}

void drawScreenHUD() {
    // Title & Workload Bar
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

void drawScreenAlerts() {
    display.setTextSize(1);
    display.setCursor(0, 0);
    display.println("DIAGNOSTICS / ALERT");
    display.drawLine(0, 9, 127, 9, SSD1306_WHITE);

    if (g_state.hasActiveAlert) {
        display.setCursor(0, 16);
        display.print("[");
        display.print(g_state.alertSeverity);
        display.print("]");

        display.setCursor(0, 30);
        display.println(g_state.alertTitle);

        display.setCursor(0, 52);
        display.print("Action: Inspect GUI");
    } else {
        display.setCursor(4, 24);
        display.println("No Critical Alerts");
        display.setCursor(4, 40);
        display.println("All subsystems OK.");
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
