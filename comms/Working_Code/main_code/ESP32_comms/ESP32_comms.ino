#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <WiFiClientSecure.h>
#include <Wire.h>
#include <MPU6050_light.h>
#include <Adafruit_ADS1X15.h>
#include "config.h"

// ============================================================================
// MQTT CONFIGURATION - USING HOTSPOT
// ============================================================================
const char* ssid = "iPhone (67)";
const char* password = "12345qwert";
//const char* ssid = "4G-MIFI-1FD0";
//const char* password = "1234567890";
const int mqtt_port = 8883;
const char* mqtt_broker = "mqtt-broker.local";  // Resolved via mDNS

//// Static IP configuration for Iphone hotspot
IPAddress local_IP(172, 20, 10, 5);
IPAddress gateway(172, 20, 10, 1);
IPAddress subnet(255, 255, 255, 0);
IPAddress dns(8, 8, 8, 8);

//static IP when using router
// IPAddress local_IP(192, 168, 100, 50);
// IPAddress gateway(192, 168, 100, 1);
// IPAddress subnet(255, 255, 255, 0);
// IPAddress dns(8, 8, 8, 8);

const char* TOPIC_SEND    = "esp32/scan";
const char* TOPIC_RECEIVE = "laptop/to_esp32/FB_002";
const char* DEVICE_ID     = "FB_002";

// ============================================================================
// DATA COLLECTION CONFIGURATION
// ============================================================================
const int INTERVAL = 16;   // 60Hz streaming

// --- Hardware Pins ---
#define LED_PIN 2
#define BUZZER  15

//// left hand flex
//const int16_t FLEX_MIN[] = {0, 700,  500,  500};
//const int16_t FLEX_MAX[] = {0, 26400, 4000, 4000};

// right hand flex
const int16_t FLEX_MIN[] = {0, 6000, 6000, 700};  
const int16_t FLEX_MAX[] = {0, 20000, 26400, 1000};

// ============================================================================
// DATA STRUCTURES
// ============================================================================
struct Sample {
  float f1, f2, f3, ax, ay, az, gx, gy, gz, mag;
};

// ============================================================================
// GLOBAL VARIABLES
// ============================================================================
WiFiClientSecure espClient;
PubSubClient     client(espClient);
MPU6050          mpu(Wire);
Adafruit_ADS1115 ads;

unsigned long lastMillis          = 0;
unsigned long buzzStart           = 0;
unsigned long lastReconnectAttempt = 0;
unsigned long lastImuCheckTime    = 0;
unsigned long sampleCount         = 0;   // running sample counter for window_id

bool isBuzzing    = false;
bool streamEnabled = true;
bool imuReady     = false;
bool wifiConnected = false;
bool mqttConnected = false;
bool mqttReadyForStream = false;  // prevents sampling until MQTT is connected

const unsigned long RECONNECT_INTERVAL = 5000;
const unsigned long IMU_CHECK_INTERVAL = 3000;

// ============================================================================
// SENSOR FUNCTIONS
// ============================================================================
float getFlex(int channel) {
  int index = channel;
  int16_t raw = ads.readADC_SingleEnded(channel);
  float normalized = (float)(raw - FLEX_MIN[index]) /
                     (float)(FLEX_MAX[index] - FLEX_MIN[index]);
  return 1.0 - constrain(normalized, 0.0, 1.0);
}

// ============================================================================
// NON-BLOCKING HAPTICS
// ============================================================================
void startBuzzer(int ms) {
  digitalWrite(BUZZER, HIGH);
  buzzStart = millis();
  isBuzzing = true;
}

void updateBuzzer() {
  if (isBuzzing && (millis() - buzzStart >= 100)) {
    digitalWrite(BUZZER, LOW);
    isBuzzing = false;
  }
}

// ============================================================================
// IMU HEALTH CHECK
// ============================================================================
void checkIMU() {
  if (mpu.begin() != 0) {
    if (imuReady) {
      Serial.println("MPU6050 disconnected");
      imuReady = false;
    }
  } else if (!imuReady) {
    Serial.println("MPU6050 detected! Calibrating");
    delay(1000);
    mpu.calcOffsets();
    Serial.println("IMU calibration done.");
    imuReady = true;
  }
}

// ============================================================================
// SEND A SINGLE TIMESTEP
// Wraps the sample in a "samples" array with exactly 1 element so that the
// Ultra96 sliding-window logic receives the same JSON structure it already
// expects. All existing field names are preserved.
// ============================================================================
void sendSample(const Sample& s) {
  if (!streamEnabled) return;
  if (!mqttReadyForStream) return;  // NEW: don't attempt to send until MQTT is connected

  bool canSend = (WiFi.status() == WL_CONNECTED && client.connected());
  if (!canSend) {
    // Only print occasionally to avoid Serial flood
    static unsigned long lastWarn = 0;
    if (millis() - lastWarn > 5000) {
      Serial.println("WiFi/MQTT not connected or streaming disabled");
      lastWarn = millis();
    }
    return;
  }

  StaticJsonDocument<512> doc;

  sampleCount++;

  String windowId = "TS_" + String(sampleCount);

  doc["device_id"]   = DEVICE_ID;
  doc["window_id"]   = windowId;
  doc["window_size"] = 1;
  doc["gesture_count"] = sampleCount;

  JsonArray samplesArray = doc.createNestedArray("samples");
  JsonObject sample = samplesArray.createNestedObject();

  JsonArray accArray  = sample.createNestedArray("acc");
  accArray.add(s.ax);
  accArray.add(s.ay);
  accArray.add(s.az);

  JsonArray gyroArray = sample.createNestedArray("gyro");
  gyroArray.add(s.gx);
  gyroArray.add(s.gy);
  gyroArray.add(s.gz);

  JsonArray flexArray = sample.createNestedArray("flex");
  flexArray.add(s.f1);
  flexArray.add(s.f2);
  flexArray.add(s.f3);

  sample["mag"] = s.mag;

  String output;
  serializeJson(doc, output);

  // debug message to show 60 samples
  if (client.publish(TOPIC_SEND, output.c_str())) {
    if (sampleCount % 60 == 0) {
      Serial.printf("[%lu] 60 samples sent\n", sampleCount);
    }
    digitalWrite(LED_PIN, !digitalRead(LED_PIN));
  } else {
    Serial.println("Publish failed");
  }
}

// ============================================================================
// MQTT MESSAGE HANDLER
// ============================================================================
void handleTriggerMessage(const JsonDocument& doc) {
  if (!doc.containsKey("target_device")) return;

  const char* target = doc["target_device"];
  if (strcmp(target, DEVICE_ID) != 0) return;

  if (!doc.containsKey("action")) return;

  const char* action = doc["action"];
  if (strcmp(action, "start") == 0) {
    streamEnabled = true;
    Serial.printf("\n*** STREAM ENABLED for %s ***\n", DEVICE_ID);
    startBuzzer(100);
  } else if (strcmp(action, "stop") == 0) {
    streamEnabled = false;
    Serial.printf("\n*** STREAM DISABLED for %s ***\n", DEVICE_ID);
    startBuzzer(100);
  }
}

void mqtt_callback(char* topic, byte* payload, unsigned int length) {
  StaticJsonDocument<1024> doc;
  DeserializationError error = deserializeJson(doc, payload, length);

  if (error) {
    Serial.printf("MQTT parse error: %s\n", error.c_str());
    return;
  }

  if (strcmp(topic, TOPIC_RECEIVE) == 0) {
    handleTriggerMessage(doc);
  }

  digitalWrite(LED_PIN, !digitalRead(LED_PIN));
}

// ============================================================================
// WIFI + TLS + MQTT SETUP
// ============================================================================
void setup_wifi() {
  WiFi.config(local_IP, gateway, subnet, dns);
  WiFi.begin(ssid, password);

  Serial.print("Connecting to WiFi");
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 20) {
    delay(500);
    Serial.print(".");
    attempts++;
  }

  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("\nWiFi connected");
    wifiConnected = true;
    Serial.printf("IP: %s\n", WiFi.localIP().toString().c_str());
  } else {
    Serial.println("\nWiFi connection failed");
    wifiConnected = false;
  }
}

void setup_tls() {
  espClient.setCACert(ca_cert);
  espClient.setCertificate(client_cert);
  espClient.setPrivateKey(client_key);
}

void checkMQTT() {
  if (!wifiConnected) return;

  if (!client.connected()) {
    mqttReadyForStream = false;  // disable streaming while disconnected
    unsigned long now = millis();
    if (now - lastReconnectAttempt > RECONNECT_INTERVAL) {
      lastReconnectAttempt = now;

      client.setServer(mqtt_broker, mqtt_port);

      String clientId = String(DEVICE_ID) + String(random(0xffff), HEX);
      if (client.connect(clientId.c_str())) {
        mqttConnected = true;
        mqttReadyForStream = true;  //enable streaming when MQTT is connected
        Serial.println("Connected to MQTT broker");
        client.subscribe(TOPIC_RECEIVE);

        // CONNECT handshake packet
        StaticJsonDocument<128> connMsg;
        connMsg["device_id"]   = DEVICE_ID;
        connMsg["packet_type"] = "CONNECT";
        String output;
        serializeJson(connMsg, output);
        client.publish(TOPIC_SEND, output.c_str());
      } else {
        mqttConnected = false;
        Serial.printf("MQTT connect failed, rc=%d\n", client.state());
      }
    }
  } else {
    client.loop();
  }
}

// ============================================================================
// SETUP
// ============================================================================
void setup() {
  Serial.begin(115200);
  delay(1000);

  pinMode(LED_PIN, OUTPUT);
  pinMode(BUZZER, OUTPUT);
  digitalWrite(LED_PIN, LOW);
  digitalWrite(BUZZER, LOW);

  Wire.begin(21, 22);
  checkIMU();

  if (!ads.begin()) {
    Serial.println("Failed to initialize ADS1115");
    while(1);
  }
  ads.setGain(GAIN_ONE);

  setup_wifi();
  setup_tls();

  client.setClient(espClient);
  client.setCallback(mqtt_callback);
  client.setBufferSize(2048);

  lastMillis = millis();

  Serial.println("Setup complete - streaming at ~60Hz");
}

// ============================================================================
// MAIN LOOP
// ============================================================================
void loop() {
  mpu.update();
  //updateBuzzer();
  checkMQTT();

  unsigned long nowMs = millis();

  // Periodic IMU check
  if (nowMs - lastImuCheckTime >= IMU_CHECK_INTERVAL) {
    lastImuCheckTime = nowMs;
    checkIMU();
  }

  // Sample and stream at 60Hz
  if (nowMs - lastMillis >= INTERVAL) {
    lastMillis = nowMs;

    if (!imuReady) return;

    Sample s;
    s.f1 = getFlex(1);
    s.f2 = getFlex(2);
    s.f3 = getFlex(3);

// ============================================================================
//// LEFT HAND axis mapping (unchanged from original)
//    s.gx = -constrain(mpu.getGyroX() / 500.0, -1.0, 1.0);
//    s.gy = constrain(mpu.getGyroY() / 500.0, -1.0, 1.0);
//    s.gz = -constrain(mpu.getGyroZ() / 500.0, -1.0, 1.0);
//
//    s.ax = constrain(mpu.getAccX() / 2.0, -1.0, 1.0);
//    s.ay = -constrain(mpu.getAccY() / 2.0, -1.0, 1.0);
//    s.az = constrain(mpu.getAccZ()  / 2.0, -1.0, 1.0);
//    // RIGHT HAND
    s.gx = -constrain(mpu.getGyroX() / 500.0, -1.0, 1.0);
    s.gy = constrain(mpu.getGyroY() / 500.0, -1.0, 1.0);
    s.gz = -constrain(mpu.getGyroZ() / 500.0, -1.0, 1.0);

    s.ax = constrain(mpu.getAccX() / 2.0, -1.0, 1.0);
    s.ay = -constrain(mpu.getAccY() / 2.0, -1.0, 1.0);
    s.az = constrain((mpu.getAccZ() - 1.0) / 2.0, -1.0, 1.0);

    static float grav_x = 0;
    static float grav_y = 0;
    static float grav_z = 0;

    const float ALPHA = 0.8;

    grav_x = ALPHA * grav_x + (1.0 - ALPHA) * s.ax;
    grav_y = ALPHA * grav_y + (1.0 - ALPHA) * s.ay;
    grav_z = ALPHA * grav_z + (1.0 - ALPHA) * s.az;

    s.ax = s.ax - grav_x;
    s.ay = s.ay - grav_y;
    s.az = s.az - grav_z;

    float raw_mag = sqrt(s.ax*s.ax + s.ay*s.ay + s.az*s.az);

    s.ax = constrain(s.ax / 2.0, -1.0, 1.0);
    s.ay = constrain(s.ay / 2.0, -1.0, 1.0);
    s.az = constrain(s.az / 2.0, -1.0, 1.0);

    s.mag = constrain(raw_mag / 2.0, 0.0, 1.0);

    sendSample(s);
  }
}