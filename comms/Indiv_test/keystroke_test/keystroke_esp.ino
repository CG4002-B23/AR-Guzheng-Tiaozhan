#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <WiFiClientSecure.h>
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

WiFiClientSecure espClient;
PubSubClient client(espClient);

#define DEVICE_ID "FB_001"

// ============================================================================
// MESSAGE TRACKING STRUCTURES
// ============================================================================
struct SentMessage {
  unsigned long sentTime;
  String messageId;
  String status;
};

struct MessageStats {
  unsigned long totalReceived = 0;
  unsigned long totalSent = 0;
};

// ============================================================================
// GLOBAL VARIABLES
// ============================================================================
#define MAX_TRACKED_MESSAGES 50
SentMessage sentMessages[MAX_TRACKED_MESSAGES];
int messageIndex = 0;
MessageStats stats;

unsigned long lastStatusTime = 0;
const unsigned long STATUS_INTERVAL = 10000;
unsigned long triggerCounter = 0;

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
  Serial.println();
  Serial.println(repeatChar('=', 60));
  Serial.println("ESP32 Keyboard Trigger Statistics:");
  Serial.println(repeatChar('=', 60));
  Serial.printf("Triggers Received:   %lu\n", stats.totalReceived);
  Serial.printf("Responses Sent:      %lu\n", stats.totalSent);
  
  Serial.println("\nRecent Triggers (last 5):");
  int start = (messageIndex - 5 + MAX_TRACKED_MESSAGES) % MAX_TRACKED_MESSAGES;
  for (int i = 0; i < 5; i++) {
    int idx = (start + i) % MAX_TRACKED_MESSAGES;
    if (sentMessages[idx].sentTime > 0) {
      Serial.print("  ");
      Serial.print(sentMessages[idx].messageId);
      Serial.print(": ");
      Serial.println(sentMessages[idx].status);
    }
  }
  Serial.println(repeatChar('=', 60));
}

// ============================================================================
// MESSAGE TRACKING
// ============================================================================
void trackSentMessage(const String& messageId) {
  sentMessages[messageIndex].sentTime = millis();
  sentMessages[messageIndex].messageId = messageId;
  sentMessages[messageIndex].status = "sent";
  
  stats.totalSent++;
  messageIndex = (messageIndex + 1) % MAX_TRACKED_MESSAGES;
}

// ============================================================================
// KEYBOARD TRIGGER HANDLER
// ============================================================================
void handleKeyboardTrigger(JsonDocument& doc) {
  triggerCounter++;
  
  const char* originalEventId = doc["event_id"] | "unknown";
  const char* keyPressed = doc["key_pressed"] | "?";
  const char* fullInput = doc["full_input"] | "";
  int counter = doc["counter"] | 0;
  
  Serial.println("\n=== KEYBOARD TRIGGER RECEIVED ===");
  Serial.printf("Original Event ID: %s\n", originalEventId);
  Serial.printf("Key Pressed: '%c'\n", keyPressed[0]);
  Serial.printf("Full Input: '%s'\n", fullInput);
  Serial.printf("Trigger Counter: %d\n", counter);
  Serial.printf("ESP32 Counter: %lu\n", triggerCounter);
  
  String responseId = "ESP32_MODIFIED_" + String(millis()) + "_" + String(triggerCounter);
  
  StaticJsonDocument<512> responseDoc;
  
  // Copy original data
  responseDoc["device_id"] = DEVICE_ID;
  responseDoc["packet_type"] = "MODIFIED_SENSOR_DATA";
  responseDoc["batch_id"] = responseId;
  responseDoc["original_event_id"] = originalEventId;
  responseDoc["key_pressed"] = keyPressed;
  responseDoc["full_input"] = fullInput;
  responseDoc["trigger_counter"] = counter;
  responseDoc["esp32_counter"] = triggerCounter;
  responseDoc["timestamp"] = millis();
  
  // modifications
  int modificationValue = random(1, 100);
  responseDoc["esp32_processed"] = true;
  responseDoc["modification_time_ms"] = random(1, 5);
  responseDoc["modified_value"] = modificationValue;
  responseDoc["original_value"] = counter;  // Show original for comparison
  responseDoc["modification_type"] = "value_doubled";
  responseDoc["new_value"] = counter * 2;   // Simple transformation to show modification
  
  String output;
  serializeJson(responseDoc, output);
  
  trackSentMessage(responseId);
  
  Serial.println("\n--- ESP32 MODIFICATIONS MADE ---");
  Serial.printf("  Original counter: %d\n", counter);
  Serial.printf("  Modified to: %d (counter * 2)\n", counter * 2);
  Serial.printf("  Random value added: %d\n", modificationValue);
  Serial.printf("  esp32_processed flag: true\n");
  Serial.printf("  modification_time_ms: %d\n", responseDoc["modification_time_ms"].as<int>());
  Serial.printf("Response ID: %s\n", responseId.c_str());
  Serial.println("--------------------------------");
  
  if (client.publish(TOPIC_SEND, output.c_str())) {
    Serial.printf(">>> MODIFIED DATA SENT BACK TO LAPTOP\n");
    digitalWrite(2, HIGH);
    delay(50);
    digitalWrite(2, LOW);
  } else {
    Serial.printf("Failed to send modified data\n");
  }
  
  Serial.println("================================\n");
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
  Serial.printf("IP Address: %s\n", WiFi.localIP().toString().c_str());
}

void setup_tls() {
  espClient.setCACert(ca_cert);
  espClient.setCertificate(client_cert);
  espClient.setPrivateKey(client_key);
}

void reconnect() {
  while (!client.connected()) {
    Serial.print("Connecting to MQTT...");
    
    String clientId = DEVICE_ID;
    clientId += String(random(0xffff), HEX);
    
    if (client.connect(clientId.c_str())) {
      Serial.println("Connected");
      client.subscribe(TOPIC_RECEIVE);
      
      StaticJsonDocument<128> connMsg;
      connMsg["device_id"] = DEVICE_ID;
      connMsg["packet_type"] = "CONNECT";
      connMsg["status"] = "ready";
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
void mqtt_callback(char* topic, byte* payload, unsigned int length) {
  StaticJsonDocument<512> doc;
  DeserializationError error = deserializeJson(doc, payload, length);
  
  if (error) {
    Serial.printf("JSON parse error: %s\n", error.c_str());
    return;
  }
  
  if (strcmp(topic, TOPIC_RECEIVE) == 0) {
    const char* packetType = doc["packet_type"] | "";
    const char* source = doc["source"] | "";
    
    if (strcmp(packetType, "KEYBOARD_TRIGGER") == 0 || 
        strcmp(source, "laptop_keyboard") == 0) {
      
      stats.totalReceived++;
      handleKeyboardTrigger(doc);
    }
    
    digitalWrite(2, HIGH);
    delay(10);
    digitalWrite(2, LOW);
  }
}

// ============================================================================
// SETUP AND MAIN LOOP
// ============================================================================
void setup() {
  pinMode(2, OUTPUT);
  digitalWrite(2, LOW);
  
  setup_wifi();
  setup_tls();
  
  client.setClient(espClient);
  client.setServer(mqtt_broker, mqtt_port);
  client.setCallback(mqtt_callback);
  client.setBufferSize(2048);
  client.setKeepAlive(60);
  
  reconnect();
  
  randomSeed(analogRead(0));
  
  Serial.println();
  Serial.println(repeatChar('=', 60));
  Serial.println("ESP32 KEYBOARD TRIGGER PROCESSOR");
  Serial.println(repeatChar('=', 60));
  Serial.println("Waiting for keyboard triggers from laptop...");
  Serial.println("Commands: stats, clear");
  Serial.println(repeatChar('=', 60));
}

void loop() {
  if (!client.connected()) reconnect();
  client.loop();
  
  unsigned long currentMillis = millis();
  
  if (currentMillis - lastStatusTime >= STATUS_INTERVAL) {
    printStatistics();
    lastStatusTime = currentMillis;
  }
  
  if (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    
    if (command == "stats") {
      printStatistics();
    } else if (command == "clear") {
      memset(sentMessages, 0, sizeof(sentMessages));
      stats = MessageStats();
      messageIndex = 0;
      triggerCounter = 0;
      Serial.println("Statistics cleared");
    }
  }
  
  delay(1);
}