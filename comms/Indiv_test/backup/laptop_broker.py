import paho.mqtt.client as mqtt
import json
import time
import threading
import socket
import ssl
import os
from collections import defaultdict
import datetime

# ============================================================================
# CONFIGURATION
# ============================================================================
MQTT_BROKER = "localhost"
DEFAULT_PORT = 8883

# Certificate paths for TLS authentication
CA_CERT = "/home/eugene/Desktop/CG4002/mqtt_certs/ca/ca.crt"
CLIENT_CERT = "/home/eugene/Desktop/CG4002/mqtt_certs/client/laptop_broker.crt"
CLIENT_KEY = "/home/eugene/Desktop/CG4002/mqtt_certs/client/laptop_broker.key"

# MQTT topics for communication between components
TOPICS = {
    "subscribe": {
        "esp32_scan": "esp32/scan",
        "ultra96_prediction": "ultra96/prediction", 
        "ultra96_status": "ultra96/status",
        "unity_messages": "unity/to_broker",
        "system_status": "system/status"
    },
    "publish": {
        "to_ultra96": "laptop/to_ultra96",
        "to_esp32": "laptop/to_esp32",
        "to_unity": "ar/visualizer",
        "system_command": "system/command"
    }
}

# ============================================================================
# MESSAGE TRACKING CLASS
# ============================================================================
# Tracks all messages flowing through the system for verification
# Monitors message IDs to ensure end-to-end delivery and measures latency
class MessageTracker:
    def __init__(self, max_history=1000):
        self.sent_messages = {}
        self.received_messages = {}
        self.lost_messages = set()
        self.mismatched_ids = defaultdict(int)
        self.latencies = []
        self.lock = threading.Lock()
        self.max_history = max_history
        self.sequence_check = defaultdict(lambda: -1)
        
    def track_sent(self, msg_id, source, destination, timestamp=None):
        """Record when a message is sent through the broker"""
        with self.lock:
            if timestamp is None:
                timestamp = time.time()
            
            self.sent_messages[msg_id] = {
                "timestamp": timestamp,
                "source": source,
                "destination": destination,
                "status": "sent"
            }
            
            if "ESP32_BATCH" in msg_id:
                try:
                    seq = int(msg_id.split("_")[-1])
                    expected = self.sequence_check["esp32"] + 1
                    if expected > 0 and seq != expected:
                        print(f"Sequence gap for ESP32: expected {expected}, got {seq}")
                        self.mismatched_ids["sequence_gap"] += 1
                    self.sequence_check["esp32"] = seq
                except:
                    pass
            
            if len(self.sent_messages) > self.max_history:
                oldest = min(self.sent_messages.keys(), 
                           key=lambda k: self.sent_messages[k]["timestamp"])
                del self.sent_messages[oldest]
    
    def track_received(self, msg_id, destination, timestamp=None):
        """Record when a message is received and verify against sent messages"""
        with self.lock:
            if timestamp is None:
                timestamp = time.time()
            
            self.received_messages[msg_id] = {
                "timestamp": timestamp,
                "destination": destination
            }
            
            if msg_id in self.sent_messages:
                sent_time = self.sent_messages[msg_id]["timestamp"]
                latency = (timestamp - sent_time) * 1000
                self.latencies.append(latency)
                
                if len(self.latencies) > 1000:
                    self.latencies = self.latencies[-1000:]
                
                self.sent_messages[msg_id]["status"] = "received"
                self.sent_messages[msg_id]["received_at"] = timestamp
                self.sent_messages[msg_id]["latency_ms"] = latency
                
                print(f"ID verified: {msg_id} - Latency: {latency:.1f}ms")
                return True
            else:
                print(f"Unknown ID received: {msg_id}")
                self.mismatched_ids["unknown"] += 1
                return False
    
    def check_for_lost(self, timeout_seconds=10):
        """Identify messages that haven't received a response within timeout"""
        with self.lock:
            now = time.time()
            lost = []
            
            for msg_id, info in self.sent_messages.items():
                if info["status"] == "sent" and (now - info["timestamp"]) > timeout_seconds:
                    info["status"] = "lost"
                    lost.append(msg_id)
                    self.lost_messages.add(msg_id)
            
            if lost:
                print(f"\nLost messages ({len(lost)}):")
                for msg_id in lost[:10]:
                    sent_time = datetime.datetime.fromtimestamp(
                        self.sent_messages[msg_id]["timestamp"]
                    ).strftime("%H:%M:%S")
                    print(f"  {msg_id} (sent at {sent_time})")
                if len(lost) > 10:
                    print(f"  ... and {len(lost) - 10} more")
            
            return lost
    
    def get_statistics(self):
        """Compile and return current transmission statistics"""
        with self.lock:
            total_sent = len(self.sent_messages)
            total_received = len([m for m in self.sent_messages.values() 
                                 if m["status"] == "received"])
            total_lost = len(self.lost_messages)
            total_pending = total_sent - total_received - total_lost
            
            loss_rate = (total_lost / total_sent * 100) if total_sent > 0 else 0
            mismatch_rate = (sum(self.mismatched_ids.values()) / total_sent * 100) if total_sent > 0 else 0
            
            if self.latencies:
                avg_latency = sum(self.latencies) / len(self.latencies)
                min_latency = min(self.latencies)
                max_latency = max(self.latencies)
            else:
                avg_latency = min_latency = max_latency = 0
            
            return {
                "total_sent": total_sent,
                "total_received": total_received,
                "total_lost": total_lost,
                "total_pending": total_pending,
                "loss_rate": loss_rate,
                "mismatch_rate": mismatch_rate,
                "mismatches": dict(self.mismatched_ids),
                "latency": {
                    "avg_ms": avg_latency,
                    "min_ms": min_latency,
                    "max_ms": max_latency,
                    "samples": len(self.latencies)
                }
            }

tracker = MessageTracker()
unity_clients = {}
unity_lock = threading.Lock()
client = None
connected = False

# ============================================================================
# UTILITY FUNCTIONS
# ============================================================================
def get_local_ip():
    """Get the local IP address of this machine"""
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except:
        return "127.0.0.1"

def get_timestamp():
    """Return current time formatted as HH:MM:SS"""
    return time.strftime("%H:%M:%S")

def verify_certificates():
    """Check if all required certificate files exist and are readable"""
    missing_certs = []
    
    if not os.path.exists(CA_CERT):
        missing_certs.append(f"CA Certificate: {CA_CERT}")
    if not os.path.exists(CLIENT_CERT):
        missing_certs.append(f"Client Certificate: {CLIENT_CERT}")
    if not os.path.exists(CLIENT_KEY):
        missing_certs.append(f"Client Key: {CLIENT_KEY}")
    
    if missing_certs:
        print("Missing certificate files:")
        for cert in missing_certs:
            print(f"  - {cert}")
        return False
    
    for cert_file in [CA_CERT, CLIENT_CERT]:
        if not os.access(cert_file, os.R_OK):
            print(f"Cannot read {cert_file} - check permissions")
            return False
    
    if not os.access(CLIENT_KEY, os.R_OK):
        print(f"Cannot read {CLIENT_KEY} - check permissions")
        return False
    
    return True

# ============================================================================
# MQTT CALLBACK FUNCTIONS
# ============================================================================
def on_connect(client, userdata, flags, rc, properties=None):
    """Called when connection to MQTT broker is established"""
    global connected
    
    if rc == 0:
        connected = True
        print(f"Connected to Mosquitto broker at {MQTT_BROKER}:8883")
        
        for topic_name, topic_path in TOPICS["subscribe"].items():
            client.subscribe(topic_path)
            print(f"Subscribed to: {topic_path}")
            
        print("\n" + "="*60)
        print("Message tracking enabled")
        print("="*60)
        print("Commands: stats, lost, track, clear")
        print("="*60)
        
        send_system_status("BROKER_STARTED", {"tls_enabled": True})
    else:
        connected = False
        error_messages = {
            1: "Incorrect protocol version",
            2: "Invalid client identifier",
            3: "Server unavailable",
            4: "Bad username or password",
            5: "Not authorized",
            6: "TLS/SSL error - check certificates"
        }
        print(f"Connection failed: {error_messages.get(rc, 'Unknown error')}")

def on_message(client, userdata, msg):
    """Main message handler - routes messages based on topic"""
    timestamp = get_timestamp()
    
    try:
        data = json.loads(msg.payload.decode())
        
        if msg.topic == TOPICS["subscribe"]["esp32_scan"]:
            handle_esp32_scan(data, timestamp)
        elif msg.topic == TOPICS["subscribe"]["ultra96_prediction"]:
            handle_ultra96_prediction(data, timestamp)
        elif msg.topic == TOPICS["subscribe"]["unity_messages"]:
            handle_unity_message(data, timestamp)
        elif msg.topic == TOPICS["subscribe"]["ultra96_status"]:
            handle_ultra96_status(data, timestamp)
        elif msg.topic == TOPICS["subscribe"]["system_status"]:
            print(f"[{timestamp}] System Status: {data.get('status', 'N/A')}")
            
    except json.JSONDecodeError:
        print(f"[{timestamp}] Invalid JSON: {msg.payload.decode()[:100]}")
    except Exception as e:
        print(f"[{timestamp}] Error: {e}")

def on_disconnect(client, userdata, rc, properties=None):
    """Called when disconnected from MQTT broker"""
    global connected
    connected = False
    if rc != 0:
        print(f"Unexpected disconnection")

# ============================================================================
# MESSAGE HANDLERS
# ============================================================================
def handle_esp32_scan(data, timestamp):
    """Process incoming ESP32 sensor data and forward to Ultra96"""
    msg_id = data.get("batch_id") or data.get("request_id") or data.get("id", "unknown")
    print(f"[{timestamp}] ESP32 Scan - ID: {msg_id}")
    tracker.track_sent(msg_id, "esp32", "ultra96")
    forward_to_ultra96(data, msg_id)

def handle_ultra96_prediction(data, timestamp):
    """Process incoming Ultra96 predictions and forward to ESP32 and Unity"""
    msg_id = data.get("response_id") or data.get("batch_id") or data.get("request_id") or "unknown"
    
    print(f"[{timestamp}] Ultra96 Prediction - ID: {msg_id}")
    
    if msg_id != "unknown":
        tracker.track_received(msg_id, "laptop")
    else:
        print(f"  Response missing ID")
        tracker.mismatched_ids["missing_id"] += 1
    
    forward_to_esp32(data, msg_id)
    forward_to_unity(data, msg_id)

def handle_unity_message(data, timestamp):
    """Process messages received from Unity client"""
    print(f"[{timestamp}] Unity Message: {data.get('type', 'unknown')}")
    client_id = data.get('client_id', 'N/A')
    print(f"  Client ID: {client_id}")
    
    msg_type = data.get("type", "unknown")
    
    if msg_type == "connection":
        with unity_lock:
            unity_clients[client_id] = {
                "connected_at": time.time(),
                "last_seen": time.time(),
                "status": "connected"
            }
        print(f"  Unity client connected: {client_id}")
        print(f"  Total Unity clients: {len(unity_clients)}")
        
        welcome_data = {
            "type": "welcome",
            "client_id": client_id,
            "message": "Connected to AR Gesture System",
            "timestamp": time.time(),
            "broker_ip": get_local_ip(),
            "status": "connected"
        }
        client.publish(TOPICS["publish"]["to_unity"], json.dumps(welcome_data))

def handle_ultra96_status(data, timestamp):
    """Process status updates from Ultra96 and forward to Unity"""
    print(f"[{timestamp}] Ultra96 Status: {data.get('status', 'N/A')}")
    forward_ultra96_status_to_unity(data)

# ============================================================================
# FORWARDING FUNCTIONS
# ============================================================================
def forward_to_ultra96(data, msg_id):
    """Forward ESP32 sensor data to Ultra96 for processing"""
    if not connected:
        return
    
    data["forwarded_at"] = time.time()
    data["original_id"] = msg_id
    
    topic = TOPICS["publish"]["to_ultra96"]
    message = json.dumps(data)
    
    print(f"  Forwarding to Ultra96: ID {msg_id}")
    client.publish(topic, message)

def forward_to_esp32(data, msg_id):
    """Forward Ultra96 predictions back to ESP32 for verification"""
    if not connected:
        return
    
    data["response_id"] = msg_id
    
    topic = TOPICS["publish"]["to_esp32"]
    message = json.dumps(data)
    
    print(f"  Forwarding to ESP32: ID {msg_id}")
    client.publish(topic, message)

def forward_to_unity(data, msg_id):
    """Forward predictions to Unity for visualization"""
    if not connected:
        return
    
    unity_data = {
        "type": "prediction",
        "prediction": data.get("prediction"),
        "confidence": data.get("confidence"),
        "request_id": msg_id,
        "player": data.get("player", 1),
        "timestamp": time.time(),
        "source": "ultra96"
    }
    
    topic = TOPICS["publish"]["to_unity"]
    message = json.dumps(unity_data)
    client.publish(topic, message)

def forward_ultra96_status_to_unity(data):
    """Forward Ultra96 status updates to Unity"""
    if not connected:
        return
    
    status_data = {
        "type": "ultra96_status",
        "status": data.get("status"),
        "active_requests": data.get("active_requests", 0),
        "timestamp": time.time()
    }
    
    client.publish(TOPICS["publish"]["to_unity"], json.dumps(status_data))

def send_system_status(status, extra_data=None):
    """Broadcast system status to all subscribers"""
    if not connected:
        return
    
    status_data = {
        "device": "laptop_broker",
        "status": status,
        "timestamp": time.time(),
        "unity_clients": len(unity_clients)
    }
    
    if extra_data:
        status_data.update(extra_data)
    
    client.publish("system/status", json.dumps(status_data))

# ============================================================================
# TLS CONFIGURATION
# ============================================================================
def setup_tls_connection(client_instance):
    """Configure TLS/SSL for secure MQTT connection"""
    try:
        print("Configuring TLS...")
        print(f"Using CA: {CA_CERT}")
        print(f"Using Cert: {CLIENT_CERT}")
        print(f"Using Key: {CLIENT_KEY}")
        
        client_instance.tls_set(
            ca_certs=CA_CERT,
            certfile=CLIENT_CERT,
            keyfile=CLIENT_KEY,
            cert_reqs=ssl.CERT_REQUIRED,
            tls_version=ssl.PROTOCOL_TLSv1_2
        )
        
        client_instance.tls_insecure_set(True)
        
        print("TLS configured successfully")
        return True
        
    except Exception as e:
        print(f"TLS setup error: {e}")
        return False

def test_tls_connection():
    """Test TLS connectivity before main connection"""
    print("\nTesting TLS connection...")
    test_client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    
    try:
        test_client.tls_set(
            ca_certs=CA_CERT,
            certfile=CLIENT_CERT,
            keyfile=CLIENT_KEY,
            tls_version=ssl.PROTOCOL_TLSv1_2
        )
        test_client.tls_insecure_set(True)
        
        test_client.connect(MQTT_BROKER, 8883, 5)
        test_client.loop_start()
        time.sleep(2)
        test_client.loop_stop()
        test_client.disconnect()
        print("TLS connection test successful")
        return True
        
    except Exception as e:
        print(f"TLS connection test failed: {e}")
        return False

# ============================================================================
# MAIN APPLICATION
# ============================================================================
def main():
    global client, connected, tracker
    
    print("="*60)
    print("Laptop MQTT Broker with Message Verification")
    print("="*60)
    
    print("\nVerifying certificates...")
    if not verify_certificates():
        print("\nCertificate verification failed.")
        return
    
    print("\nAttempting TLS connection...")
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    
    if not setup_tls_connection(client):
        print("\nFailed to setup TLS.")
        return
    
    client.on_connect = on_connect
    client.on_message = on_message
    client.on_disconnect = on_disconnect
    
    print(f"\nConnecting to Mosquitto on port 8883...")
    
    try:
        client.connect(MQTT_BROKER, 8883, 60)
        client.loop_start()
        
        for i in range(10):
            if connected:
                break
            print(f"Waiting for connection... ({i+1}/10)")
            time.sleep(1)
        
        if not connected:
            print("\nFailed to connect to Mosquitto")
            return
        
        print("\nBroker running. Commands: stats, lost, track, latency, clear, quit")
        print("-"*60)
        
        while True:
            try:
                cmd = input("\nbroker> ").strip().lower()
                
                if cmd == "stats":
                    stats = tracker.get_statistics()
                    print("\n" + "="*60)
                    print("Message Verification Statistics")
                    print("="*60)
                    print(f"Batches Sent:        {stats['total_sent']}")
                    print(f"Responses Received:  {stats['total_received']}")
                    print(f"Lost Messages:       {stats['total_lost']} ({stats['loss_rate']:.1f}%)")
                    print(f"ID Mismatches:       {stats['mismatch_rate']:.1f}%")
                    
                    if stats['mismatches']:
                        print("\nMismatch Details:")
                        for k, v in stats['mismatches'].items():
                            print(f"  {k}: {v}")
                    
                    print("\nRecent Messages (last 5):")
                    count = 0
                    for msg_id in list(tracker.sent_messages.keys())[-5:]:
                        info = tracker.sent_messages[msg_id]
                        print(f"  {msg_id}: {info['status']}")
                        count += 1
                    if count == 0:
                        print("  No messages tracked yet")
                    
                    if stats['latency']['samples'] > 0:
                        print(f"\nLatency (ms):")
                        print(f"  Avg: {stats['latency']['avg_ms']:.1f}")
                        print(f"  Min: {stats['latency']['min_ms']:.1f}")
                        print(f"  Max: {stats['latency']['max_ms']:.1f}")
                    print("="*60)
                    
                elif cmd == "lost":
                    lost = tracker.check_for_lost()
                    if not lost:
                        print("No lost messages detected")
                        
                elif cmd == "track":
                    print("\nRecent Message Status:")
                    count = 0
                    for msg_id in list(tracker.sent_messages.keys())[-10:]:
                        info = tracker.sent_messages[msg_id]
                        status = info['status']
                        latency = info.get('latency_ms', 0)
                        print(f"  {msg_id[:30]}: {status}" + 
                              (f" ({latency:.1f}ms)" if status == "received" else ""))
                        
                elif cmd == "latency":
                    stats = tracker.get_statistics()
                    if stats['latency']['samples'] > 0:
                        print(f"\nLatency Statistics:")
                        print(f"  Average: {stats['latency']['avg_ms']:.1f} ms")
                        print(f"  Minimum: {stats['latency']['min_ms']:.1f} ms")
                        print(f"  Maximum: {stats['latency']['max_ms']:.1f} ms")
                    else:
                        print("No latency data available")
                        
                elif cmd == "clear":
                    tracker = MessageTracker()
                    print("Statistics cleared")
                    
                elif cmd in ["quit", "exit", "q"]:
                    print("Shutting down...")
                    client.loop_stop()
                    client.disconnect()
                    break
                    
            except KeyboardInterrupt:
                print("\nShutting down...")
                client.loop_stop()
                client.disconnect()
                break
                
    except Exception as e:
        print(f"Connection error: {e}")

if __name__ == "__main__":
    main()