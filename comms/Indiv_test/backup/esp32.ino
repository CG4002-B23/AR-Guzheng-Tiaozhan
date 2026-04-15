#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <WiFiClientSecure.h>
#include "config.h"

// ============================================================================
// CONFIGURATION
// ============================================================================
const char* ssid = "iPhone (67)";           // WiFi network name
const char* password = "12345qwert";         // WiFi password
const char* mqtt_broker = "172.20.10.4";     // IP address of MQTT broker
const int mqtt_port = 8883;                   // MQTT port with TLS

const char* TOPIC_SEND = "esp32/scan";       // Topic for sending sensor data
const char* TOPIC_RECEIVE = "laptop/to_esp32"; // Topic for receiving predictions
const char* TOPIC_STATUS = "esp32/status";    // Topic for status updates

WiFiClientSecure espClient;    // Secure WiFi client for TLS
PubSubClient client(espClient); // MQTT client using secure connection

// Sampling configuration
#define SAMPLE_FREQUENCY 30      // Samples per second
#define SAMPLES_PER_BATCH 5       // Number of samples per batch
#define SAMPLE_INTERVAL_MS (1000 / SAMPLE_FREQUENCY)  // Time between samples

#define DEVICE_ID "FB_001"        // Unique identifier for this ESP32

// ============================================================================
// DATA STRUCTURES
// ============================================================================
/**
 * Structure to hold dummy sensor data for testing
 * Mimics actual IMU sensor readings
 */
struct DummyData {
  float ax, ay, az;              // Accelerometer readings
  float gx, gy, gz;              // Gyroscope readings
  unsigned long timestamp;        // Timestamp when sample was taken
};

/**
 * Structure to track a single sent message through the system
 * Used for verification and latency measurement
 */
struct SentMessage {
  unsigned long sentTime;         // Time when message was sent
  bool responded;                 // Whether response has been received
  String batchId;                  // Unique batch identifier
  int sampleCount;                 // Number of samples in this batch
  unsigned long responseTime;      // Time taken to receive response
  String status;                    // Current status (pending/received/lost)
};

/**
 * Structure to maintain overall transmission statistics
 */
struct MessageStats {
  unsigned long totalSent = 0;          // Total batches sent
  unsigned long totalReceived = 0;       // Total responses received
  unsigned long totalLost = 0;           // Total messages lost
  unsigned long totalMismatched = 0;     // Total ID mismatches
  unsigned long totalRoundTrip = 0;       // Sum of all round trip times
  unsigned long minRoundTrip = 999999;    // Minimum round trip time
  unsigned long maxRoundTrip = 0;         // Maximum round trip time
};

// ============================================================================
// GLOBAL VARIABLES
// ============================================================================
#define MAX_TRACKED_MESSAGES 100          // Maximum messages to track in history
SentMessage sentMessages[MAX_TRACKED_MESSAGES];  // Circular buffer for tracking
int messageIndex = 0;                      // Current index in circular buffer
MessageStats stats;                         // Overall statistics

unsigned long batchCounter = 0;             // Sequential batch counter
unsigned long lastSampleTime = 0;           // Time of last sample generation
unsigned long lastStatusTime = 0;           // Time of last status display
const unsigned long STATUS_INTERVAL = 10000; // Status display interval (10 seconds)

#define MAX_BATCH_SIZE 10                    // Maximum samples per batch
DummyData sampleBuffer[MAX_BATCH_SIZE];      // Buffer for current batch
int sampleCount = 0;                          // Number of samples in current batch

float simulatedAngle = 0;                     // For generating smooth fake data

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

/**
Print current statistics to Serial monitor
Displays sent/received counts, loss rates, and round trip times
 */
void printStatistics() {
  float lossRate = stats.totalSent > 0 ? (stats.totalLost * 100.0 / stats.totalSent) : 0;
  float mismatchRate = stats.totalSent > 0 ? (stats.totalMismatched * 100.0 / stats.totalSent) : 0;
  float avgRoundTrip = stats.totalReceived > 0 ? stats.totalRoundTrip / stats.totalReceived : 0;
  
  Serial.println();
  Serial.println(repeatChar('=', 60));
  Serial.println("Message Verification Statistics:");
  Serial.println(repeatChar('=', 60));
  Serial.printf("Batches Sent:        %lu\n", stats.totalSent);
  Serial.printf("Responses Received:  %lu\n", stats.totalReceived);
  Serial.printf("Lost Messages:       %lu (%.1f%%)\n", stats.totalLost, lossRate);
  Serial.printf("ID Mismatches:       %lu (%.1f%%)\n", stats.totalMismatched, mismatchRate);
  
  if (stats.totalReceived > 0) {
    Serial.println("\nRound Trip Times:");
    Serial.printf("  Min: %lu ms\n", stats.minRoundTrip);
    Serial.printf("  Max: %lu ms\n", stats.maxRoundTrip);
    Serial.printf("  Avg: %.1f ms\n", avgRoundTrip);
  }
  
  Serial.println("\nRecent Messages (last 5):");
  int start = (messageIndex - 5 + MAX_TRACKED_MESSAGES) % MAX_TRACKED_MESSAGES;
  for (int i = 0; i < 5; i++) {
    int idx = (start + i) % MAX_TRACKED_MESSAGES;
    if (sentMessages[idx].sentTime > 0) {
      Serial.print("  ");
      Serial.print(sentMessages[idx].batchId);
      Serial.print(": ");
      Serial.print(sentMessages[idx].status);
      if (sentMessages[idx].responded) {
        Serial.printf(" (%lums)", sentMessages[idx].responseTime);
      }
      Serial.println();
    }
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

/**
Find a sent message by its ID and verify the response
Calculates round trip time and updates statistics
 */
bool findAndVerifyMessage(const String& receivedId) {
  unsigned long now = millis();
  
  // Look for pending message with matching ID
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
      return true;
    }
  }
  
  // Check if this is a duplicate response
  for (int i = 0; i < MAX_TRACKED_MESSAGES; i++) {
    if (sentMessages[i].batchId == receivedId) {
      if (sentMessages[i].responded) {
        Serial.printf("Duplicate response for %s\n", receivedId.c_str());
      }
      return true;
    }
  }
  
  // No matching ID found
  Serial.printf("Unknown ID received: %s\n", receivedId.c_str());
  stats.totalMismatched++;
  return false;
}

/** Check for messages that have been pending too long
Marks them as lost and updates statistics
 */
void checkForLostMessages() {
  unsigned long now = millis();
  int lostCount = 0;
  
  for (int i = 0; i < MAX_TRACKED_MESSAGES; i++) {
    if (!sentMessages[i].responded && 
        sentMessages[i].sentTime > 0 && 
        (now - sentMessages[i].sentTime > 10000)) {  // 10 second timeout
      
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
// FAKE DATA GENERATION
// ============================================================================
DummyData generateDummyData() {
  DummyData data;
  
  // Update angle for smooth sine wave generation
  simulatedAngle += 0.2;
  if (simulatedAngle > TWO_PI) simulatedAngle -= TWO_PI;
  
  // Generate accelerometer data
  data.ax = sin(simulatedAngle) * 0.8 + random(-30, 30) / 1000.0;
  data.ay = cos(simulatedAngle * 0.7) * 0.6 + random(-30, 30) / 1000.0;
  data.az = 0.98 + sin(simulatedAngle * 1.3) * 0.15;
  
  // Generate gyroscope data
  data.gx = sin(simulatedAngle * 2.0) * 45.0 + random(-100, 100) / 10.0;
  data.gy = cos(simulatedAngle * 1.5) * 35.0 + random(-100, 100) / 10.0;
  data.gz = sin(simulatedAngle) * 25.0 + random(-100, 100) / 10.0;
  
  data.timestamp = millis();
  
  return data;
}

// Add a sample to the current batch buffer
void addSampleToBatch(const DummyData& sample) {
  if (sampleCount < MAX_BATCH_SIZE) {
    sampleBuffer[sampleCount] = sample;
    sampleCount++;
  }
}

/**
Send the batch via MQTT
Creates JSON payload
 */
void sendBatch() {
  if (sampleCount == 0 || !client.connected()) return;
  
  // Create JSON document with appropriate size
  StaticJsonDocument<4096> doc;
  
  batchCounter++;
  String batchId = "ESP32_BATCH_" + String(millis()) + "_" + String(batchCounter);
  
  // Add metadata to JSON
  doc["device_id"] = DEVICE_ID;
  doc["packet_type"] = "SENSOR_DATA";
  doc["batch_id"] = batchId;
  doc["batch_counter"] = batchCounter;
  doc["batch_size"] = sampleCount;
  doc["sample_rate"] = SAMPLE_FREQUENCY;
  doc["timestamp"] = millis();
  
  // Create array for samples
  JsonArray samplesArray = doc.createNestedArray("samples");
  
  // Add each sample to the array
  for (int i = 0; i < sampleCount; i++) {
    JsonObject sample = samplesArray.createNestedObject();
    sample["ts"] = sampleBuffer[i].timestamp;
    // Use serialized to avoid quotes around numbers (saves space)
    sample["ax"] = serialized(String(sampleBuffer[i].ax, 3));
    sample["ay"] = serialized(String(sampleBuffer[i].ay, 3));
    sample["az"] = serialized(String(sampleBuffer[i].az, 3));
    sample["gx"] = serialized(String(sampleBuffer[i].gx, 2));
    sample["gy"] = serialized(String(sampleBuffer[i].gy, 2));
    sample["gz"] = serialized(String(sampleBuffer[i].gz, 2));
  }
  
  // Serialize JSON to string
  String output;
  serializeJson(doc, output);
  
  // Track this message for verification
  trackSentMessage(batchId, sampleCount);
  
  Serial.printf("\nSending batch %s (%d samples)\n", batchId.c_str(), sampleCount);
  
  // Publish and indicate with LED
  if (client.publish(TOPIC_SEND, output.c_str())) {
    Serial.printf("Sent batch %s\n", batchId.c_str());
    digitalWrite(2, HIGH);
    delay(5);
    digitalWrite(2, LOW);
  } else {
    Serial.printf("Failed to send batch %s\n", batchId.c_str());
  }
  
  // Clear the batch buffer
  sampleCount = 0;
}

// ============================================================================
// WIFI AND MQTT SETUP
// ============================================================================
/**
 * Connect to WiFi network
 * Waits until connection is established
 */
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

/**
 Configure TLS certificates for secure MQTT connection
 */
void setup_tls() {
  espClient.setCACert(ca_cert);
  espClient.setCertificate(client_cert);
  espClient.setPrivateKey(client_key);
}

/**
 Reconnect to MQTT broker if connection is lost
 Attempts to reconnect and resubscribe to topics
 */
void reconnect() {
  while (!client.connected()) {
    Serial.print("Connecting to MQTT...");
    
    String clientId = DEVICE_ID;
    clientId += String(random(0xffff), HEX);
    
    if (client.connect(clientId.c_str())) {
      Serial.println("Connected");
      client.subscribe(TOPIC_RECEIVE);
      
      // Send connection announcement
      StaticJsonDocument<128> connMsg;
      connMsg["device_id"] = DEVICE_ID;
      connMsg["packet_type"] = "CONNECT";
      connMsg["timestamp"] = millis();
      
      String output;
      serializeJson(connMsg, output);
      client.publish(TOPIC_SEND, output.c_str());
      
    } else {
      Serial.printf("Failed, rc=%d\n", client.state());
      delay(2000);
    }
  }
}

// ============================================================================
// MQTT CALLBACK
// ============================================================================
// Handle incoming MQTT messages
// Called automatically when message arrives on subscribed topic

void mqtt_callback(char* topic, byte* payload, unsigned int length) {
  StaticJsonDocument<512> doc;
  DeserializationError error = deserializeJson(doc, payload, length);
  
  if (error) return;
  
  if (strcmp(topic, TOPIC_RECEIVE) == 0) {
    const char* responseId = doc["response_id"];
    
    if (responseId) {
      Serial.printf("Response received: %s\n", responseId);
      
      bool verified = findAndVerifyMessage(String(responseId));
      
      if (verified) {
        int prediction = doc["prediction"] | -1;
        float confidence = doc["confidence"] | 0;
        
        if (prediction != -1) {
          Serial.printf("   Prediction: %d, Confidence: %.2f\n", 
                       prediction, confidence);
        }
      }
    }
    
    // Visual indication with LED
    digitalWrite(2, HIGH);
    delay(10);
    digitalWrite(2, LOW);
  }
}

// ============================================================================
// SETUP AND MAIN LOOP
// ============================================================================
/**
 * Arduino setup function - runs once at startup
 * Initializes hardware, connects to WiFi, sets up MQTT
 */
void setup() {
  pinMode(2, OUTPUT);  // Initialize LED
  
  setup_wifi();
  setup_tls();
  
  client.setClient(espClient);
  client.setServer(mqtt_broker, mqtt_port);
  client.setCallback(mqtt_callback);
  client.setBufferSize(4096);   // Increase buffer for large JSON messages
  client.setKeepAlive(60);      // Set keepalive interval
  
  reconnect();
  
  randomSeed(analogRead(0));  // Seed random number generator
  
  Serial.println();
  Serial.println(repeatChar('=', 60));
  Serial.println("ESP32 Message Verification System");
  Serial.println(repeatChar('=', 60));
  Serial.println("Commands: stats, check, flush, clear");
  Serial.println(repeatChar('=', 60));
}

//Handles MQTT communication, data generation, and user commands
// ensures that when disconnected, it tries to reconnect back.
void loop() {
  // Maintain MQTT connection
  if (!client.connected()) reconnect();
  client.loop();
  
  unsigned long currentMillis = millis();
  
  // Generate new sample at specified frequency
  if (currentMillis - lastSampleTime >= SAMPLE_INTERVAL_MS) {
    DummyData data = generateDummyData();
    addSampleToBatch(data);
    lastSampleTime = currentMillis;
  }
  
  // Send batch when enough samples collected
  if (sampleCount >= SAMPLES_PER_BATCH) {
    sendBatch();
  }
  
  // Display statistics periodically
  if (currentMillis - lastStatusTime >= STATUS_INTERVAL) {
    printStatistics();
    lastStatusTime = currentMillis;
  }
  
  // Handle serial commands
  if (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    
    if (command == "stats") {
      printStatistics();
    } else if (command == "check") {
      checkForLostMessages();
    } else if (command == "flush") {
      if (sampleCount > 0) {
        sendBatch();
        Serial.println("Batch flushed");
      }
    } else if (command == "clear") {
      // Reset all statistics and tracking
      memset(sentMessages, 0, sizeof(sentMessages));
      stats = MessageStats();
      messageIndex = 0;
      Serial.println("Statistics cleared");
    }
  }
  
  delay(1);  // delay to prevent watchdog issues (crash etc.)
}