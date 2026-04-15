#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <WiFiClientSecure.h>
#include <Wire.h>
#include <FastIMU.h>
#include "config.h"

// ============================================================================
// CONFIGURATION
// ============================================================================
const char* ssid = "iPhone (67)";
const char* password = "12345qwert";
const char* mqtt_broker = "172.20.10.4";
const int mqtt_port = 8883;

const char* TOPIC_SEND = "esp32/scan";
const char* TOPIC_RECEIVE = "laptop/to_esp32";
const char* TOPIC_STATUS = "esp32/status";

WiFiClientSecure espClient;
PubSubClient client(espClient);

// IMU Configuration
#define IMU_SDA 21
#define IMU_SCL 22
#define IMU_FREQUENCY 30
#define SAMPLES_PER_BATCH 5
#define IMU_ADDRESS 0x68

MPU6050 IMU;
calData calibration = { 0 };
AccelData accelData;
GyroData gyroData;

// ============================================================================
// DATA STRUCTURES
// ============================================================================
struct IMUData {
  float ax, ay, az;
  float gx, gy, gz;
  float temperature;
  unsigned long timestamp;
};

struct SentMessage {
  unsigned long sentTime;
  bool responded;
  String batchId;
  int sampleCount;
  unsigned long responseTime;
  String status;
};

struct MessageStats {
  unsigned long totalSent = 0;
  unsigned long totalReceived = 0;
  unsigned long totalLost = 0;
  unsigned long totalMismatched = 0;
  unsigned long totalRoundTrip = 0;
  unsigned long minRoundTrip = 999999;
  unsigned long maxRoundTrip = 0;
};

// ============================================================================
// GLOBAL VARIABLES
// ============================================================================
#define MAX_TRACKED_MESSAGES 100
SentMessage sentMessages[MAX_TRACKED_MESSAGES];
int messageIndex = 0;
MessageStats stats;

unsigned long batchCounter = 0;
unsigned long lastSampleTime = 0;
unsigned long lastStatusTime = 0;
const unsigned long SAMPLE_INTERVAL = 1000 / IMU_FREQUENCY;
const unsigned long STATUS_INTERVAL = 10000;

// Batching buffer
#define MAX_BATCH_SIZE 10
IMUData sampleBuffer[MAX_BATCH_SIZE];
int sampleCount = 0;

// Smoothing buffer
#define SMOOTHING_BUFFER_SIZE 3
float smoothAX[SMOOTHING_BUFFER_SIZE] = {0};
float smoothAY[SMOOTHING_BUFFER_SIZE] = {0};
float smoothAZ[SMOOTHING_BUFFER_SIZE] = {0};
int smoothIndex = 0;

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================
String repeatChar(char ch, int count) {
  String result = "";
  for (int i = 0; i < count; i++) {
    result += ch;
  }
  return result;
}

void printStatistics() {
  float lossRate = stats.totalSent > 0 ? (stats.totalLost * 100.0 / stats.totalSent) : 0;
  float mismatchRate = stats.totalSent > 0 ? (stats.totalMismatched * 100.0 / stats.totalSent) : 0;
  float avgRoundTrip = stats.totalReceived > 0 ? stats.totalRoundTrip / stats.totalReceived : 0;
  
  Serial.println();
  Serial.println(repeatChar('=', 60));
  Serial.println("MESSAGE VERIFICATION STATISTICS:");
  Serial.println(repeatChar('=', 60));
  Serial.print("Batches Sent:        ");
  Serial.println(stats.totalSent);
  Serial.print("Responses Received:  ");
  Serial.println(stats.totalReceived);
  Serial.print("Lost Messages:       ");
  Serial.print(stats.totalLost);
  Serial.print(" (");
  Serial.print(lossRate, 1);
  Serial.println("%)");
  Serial.print("ID Mismatches:       ");
  Serial.print(stats.totalMismatched);
  Serial.print(" (");
  Serial.print(mismatchRate, 1);
  Serial.println("%)");
  
  if (stats.totalReceived > 0) {
    Serial.println("\nRound Trip Times:");
    Serial.print("  Min: ");
    Serial.print(stats.minRoundTrip);
    Serial.println(" ms");
    Serial.print("  Max: ");
    Serial.print(stats.maxRoundTrip);
    Serial.println(" ms");
    Serial.print("  Avg: ");
    Serial.print(avgRoundTrip, 1);
    Serial.println(" ms");
  }
  
  Serial.println(repeatChar('=', 60));
}

// ============================================================================
// MESSAGE TRACKING FUNCTIONS
// ============================================================================
void trackSentMessage(const String& batchId, int numSamples) {
  sentMessages[messageIndex].sentTime = millis();
  sentMessages[messageIndex].responded = false;
  sentMessages[messageIndex].batchId = batchId;
  sentMessages[messageIndex].sampleCount = numSamples;
  sentMessages[messageIndex].status = "pending";
  
  stats.totalSent++;
  
  messageIndex = (messageIndex + 1) % MAX_TRACKED_MESSAGES;
}

bool findAndVerifyMessage(const String& receivedId) {
  unsigned long now = millis();
  bool found = false;
  
  for (int i = 0; i < MAX_TRACKED_MESSAGES; i++) {
    if (!sentMessages[i].responded && 
        sentMessages[i].batchId.length() > 0 && 
        sentMessages[i].batchId == receivedId) {
      
      unsigned long responseTime = now - sentMessages[i].sentTime;
      sentMessages[i].responded = true;
      sentMessages[i].responseTime = responseTime;
      sentMessages[i].status = "received";
      
      stats.totalReceived++;
      stats.totalRoundTrip += responseTime;
      if (responseTime < stats.minRoundTrip) stats.minRoundTrip = responseTime;
      if (responseTime > stats.maxRoundTrip) stats.maxRoundTrip = responseTime;
      
      Serial.printf("ID Match: %s - Round trip: %lums\n", 
                    receivedId.c_str(), responseTime);
      found = true;
      break;
    }
  }
  
  if (!found) {
    for (int i = 0; i < MAX_TRACKED_MESSAGES; i++) {
      if (sentMessages[i].batchId == receivedId) {
        if (sentMessages[i].responded) {
          Serial.printf("Duplicate response for %s\n", receivedId.c_str());
        }
        found = true;
        break;
      }
    }
    
    if (!found) {
      Serial.printf("Unknown ID received: %s\n", receivedId.c_str());
      stats.totalMismatched++;
    }
  }
  
  return found;
}

void checkForLostMessages() {
  unsigned long now = millis();
  int lostCount = 0;
  
  for (int i = 0; i < MAX_TRACKED_MESSAGES; i++) {
    if (!sentMessages[i].responded && 
        sentMessages[i].sentTime > 0 && 
        (now - sentMessages[i].sentTime > 10000)) {
      
      sentMessages[i].status = "lost";
      stats.totalLost++;
      lostCount++;
      
      Serial.printf("Lost message: %s\n", sentMessages[i].batchId.c_str());
    }
  }
  
  if (lostCount > 0) {
    Serial.printf("Total lost messages: %d\n", lostCount);
  }
}

// ============================================================================
// IMU FUNCTIONS
// ============================================================================
IMUData smoothData(const IMUData& raw) {
  IMUData smoothed = raw;
  
  smoothAX[smoothIndex] = raw.ax;
  smoothAY[smoothIndex] = raw.ay;
  smoothAZ[smoothIndex] = raw.az;
  
  float sumAX = 0, sumAY = 0, sumAZ = 0;
  for (int i = 0; i < SMOOTHING_BUFFER_SIZE; i++) {
    sumAX += smoothAX[i];
    sumAY += smoothAY[i];
    sumAZ += smoothAZ[i];
  }
  
  smoothed.ax = sumAX / SMOOTHING_BUFFER_SIZE;
  smoothed.ay = sumAY / SMOOTHING_BUFFER_SIZE;
  smoothed.az = sumAZ / SMOOTHING_BUFFER_SIZE;
  
  smoothIndex = (smoothIndex + 1) % SMOOTHING_BUFFER_SIZE;
  return smoothed;
}

bool setupIMU() {
  Serial.println("Initializing MPU6050...");
  Wire.begin(IMU_SDA, IMU_SCL);
  Wire.setClock(400000);
  
  int err = IMU.init(calibration, IMU_ADDRESS);
  if (err != 0) {
    Serial.print("Failed to initialize MPU6050. Error code: ");
    Serial.println(err);
    return false;
  }
  
  IMU.setAccelRange(16);
  IMU.setGyroRange(2000);
  
  Serial.println("MPU6050 initialized");
  return true;
}

IMUData readIMU() {
  IMUData data;
  IMU.update();
  IMU.getAccel(&accelData);
  IMU.getGyro(&gyroData);
  
  data.ax = accelData.accelX;
  data.ay = accelData.accelY;
  data.az = accelData.accelZ;
  data.gx = gyroData.gyroX;
  data.gy = gyroData.gyroY;
  data.gz = gyroData.gyroZ;
  data.temperature = IMU.getTemp();
  data.timestamp = millis();
  
  return data;
}

// ============================================================================
// WIFI AND MQTT SETUP
// ============================================================================
void setup_wifi() {
  Serial.begin(115200);
  delay(100);
  
  Serial.print("Connecting to WiFi");
  WiFi.begin(ssid, password);
  
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  
  Serial.println("\nWiFi connected");
}

void setup_tls() {
  espClient.setCACert(ca_cert);
  espClient.setCertificate(client_cert);
  espClient.setPrivateKey(client_key);
}

void reconnect() {
  while (!client.connected()) {
    Serial.print("Connecting to MQTT...");
    
    String clientId = "ESP32-GAME-";
    clientId += String(random(0xffff), HEX);
    
    if (client.connect(clientId.c_str())) {
      Serial.println("Connected");
      client.subscribe(TOPIC_RECEIVE);
    } else {
      Serial.print("Failed, rc=");
      Serial.print(client.state());
      Serial.println(" retry in 2s");
      delay(2000);
    }
  }
}

// ============================================================================
// MQTT CALLBACK
// ============================================================================
void mqtt_callback(char* topic, byte* payload, unsigned int length) {
  StaticJsonDocument<512> doc;
  DeserializationError error = deserializeJson(doc, payload, length);
  
  if (!error && strcmp(topic, TOPIC_RECEIVE) == 0) {
    const char* responseId = doc["response_id"];
    
    if (responseId) {
      Serial.print("Response received: ");
      Serial.println(responseId);
      
      bool verified = findAndVerifyMessage(String(responseId));
      
      if (verified) {
        int prediction = doc["prediction"] | 0;
        float confidence = doc["confidence"] | 0;
        
        Serial.printf("   Prediction: %d, Confidence: %.2f\n", 
                     prediction, confidence);
      }
    } else {
      Serial.println("Response missing ID field");
    }
    
    digitalWrite(2, HIGH);
    delay(10);
    digitalWrite(2, LOW);
  }
}

// ============================================================================
// BATCHED DATA HANDLING
// ============================================================================
void addSampleToBatch(const IMUData& sample) {
  if (sampleCount < MAX_BATCH_SIZE) {
    sampleBuffer[sampleCount] = sample;
    sampleCount++;
  }
}

void sendBatch() {
  if (sampleCount == 0 || !client.connected()) return;
  
  StaticJsonDocument<4096> doc;
  
  batchCounter++;
  String batchId = "ESP32_BATCH_" + String(millis()) + "_" + String(batchCounter);
  
  doc["device"] = "esp32_game";
  doc["batch_id"] = batchId;
  doc["batch_counter"] = batchCounter;
  doc["batch_size"] = sampleCount;
  doc["sample_rate"] = IMU_FREQUENCY;
  doc["timestamp"] = millis();
  
  JsonArray samplesArray = doc.createNestedArray("samples");
  
  for (int i = 0; i < sampleCount; i++) {
    JsonObject sample = samplesArray.createNestedObject();
    sample["ts"] = sampleBuffer[i].timestamp;
    sample["ax"] = serialized(String(sampleBuffer[i].ax, 3));
    sample["ay"] = serialized(String(sampleBuffer[i].ay, 3));
    sample["az"] = serialized(String(sampleBuffer[i].az, 3));
    sample["gx"] = serialized(String(sampleBuffer[i].gx, 2));
    sample["gy"] = serialized(String(sampleBuffer[i].gy, 2));
    sample["gz"] = serialized(String(sampleBuffer[i].gz, 2));
  }
  
  String output;
  serializeJson(doc, output);
  
  trackSentMessage(batchId, sampleCount);
  
  if (client.publish(TOPIC_SEND, output.c_str())) {
    Serial.printf("Sent batch %s (%d samples)\n", batchId.c_str(), sampleCount);
  }
  
  sampleCount = 0;
}

void collectAndBatchData() {
  IMUData rawData = readIMU();
  IMUData smoothedData = smoothData(rawData);
  addSampleToBatch(smoothedData);
}

// ============================================================================
// SETUP AND MAIN LOOP
// ============================================================================
void setup() {
  pinMode(2, OUTPUT);
  
  setup_wifi();
  
  if (!setupIMU()) {
    Serial.println("IMU failed!");
    while (1) {
      digitalWrite(2, !digitalRead(2));
      delay(100);
    }
  }
  
  setup_tls();
  
  client.setClient(espClient);
  client.setServer(mqtt_broker, mqtt_port);
  client.setCallback(mqtt_callback);
  client.setBufferSize(4096);
  client.setKeepAlive(60);
  
  reconnect();
  
  Serial.println();
  Serial.println(repeatChar('=', 60));
  Serial.println("ESP32 Message Verification System");
  Serial.println(repeatChar('=', 60));
  Serial.println("Commands: stats, check, flush, clear");
  Serial.println(repeatChar('=', 60));
}

void loop() {
  if (!client.connected()) reconnect();
  client.loop();
  
  unsigned long currentMillis = millis();
  
  if (currentMillis - lastSampleTime >= SAMPLE_INTERVAL) {
    collectAndBatchData();
    lastSampleTime = currentMillis;
  }
  
  if (sampleCount >= SAMPLES_PER_BATCH) {
    sendBatch();
  }
  
  if (currentMillis - lastStatusTime >= STATUS_INTERVAL) {
    printStatistics();
    lastStatusTime = currentMillis;
  }
  
  if (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    
    if (command == "stats") {
      printStatistics();
    } else if (command == "check") {
      checkForLostMessages();
    } else if (command == "flush") {
      if (sampleCount > 0) sendBatch();
    } else if (command == "clear") {
      memset(sentMessages, 0, sizeof(sentMessages));
      stats = MessageStats();
      messageIndex = 0;
      Serial.println("Statistics cleared");
    }
  }
  
  delay(1);
}