#include <WiFi.h>
#include <PubSubClient.h>
#include <WiFiClientSecure.h>
#include "config.h"

const char* wifi_ssid = "iPhone (67)";
const char* wifi_password = "12345qwert";
const char* mqtt_server = "172.20.10.4";
const int mqtt_port = 8883;

const char* TOPIC_RECEIVE = "esp32/file_transfer";
const char* TOPIC_SEND = "esp32/file_chunk";
const char* TOPIC_STATUS = "esp32/file_ready";

WiFiClientSecure wifi_client;
PubSubClient mqtt_client(wifi_client);

void connect_wifi() {
  Serial.begin(115200);
  Serial.print("Connecting to WiFi");
  WiFi.begin(wifi_ssid, wifi_password);
  
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi connected");
}

void setup_tls() {
  wifi_client.setCACert(ca_cert);
  wifi_client.setCertificate(client_cert);
  wifi_client.setPrivateKey(client_key);
}

void message_callback(char* topic, byte* payload, unsigned int length) {
  if (strcmp(topic, TOPIC_RECEIVE) == 0) {
    mqtt_client.publish(TOPIC_SEND, payload, length);
  }
}

// ============================================================================
// RECONNECTION FUNCTION
// ============================================================================
void reconnect() {
  while (!mqtt_client.connected()) {
    Serial.print("Connecting to MQTT...");
    
    String clientId = "ESP32-";
    clientId += String(random(0xffff), HEX);
    
    if (mqtt_client.connect(clientId.c_str())) {
      Serial.println("Connected");
      mqtt_client.subscribe(TOPIC_RECEIVE);
      mqtt_client.publish(TOPIC_STATUS, "ONLINE");
      Serial.println("Ready signal sent");
    } else {
      Serial.printf("Failed, rc=%d\n", mqtt_client.state());
      delay(2000);
    }
  }
}

void setup() {
  connect_wifi();
  setup_tls();
  
  mqtt_client.setServer(mqtt_server, mqtt_port);
  mqtt_client.setCallback(message_callback);
  mqtt_client.setBufferSize(20480);
  
  reconnect();
}

void loop() {
  if (!mqtt_client.connected()) {
    reconnect();
  }
  mqtt_client.loop();
  delay(10);
}