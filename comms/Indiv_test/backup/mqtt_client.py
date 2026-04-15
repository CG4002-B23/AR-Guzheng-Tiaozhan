import paho.mqtt.client as mqtt
import json
import time
import threading
import sys
from concurrent.futures import ThreadPoolExecutor
import random
import uuid
import ssl
import os
from collections import defaultdict

# ============================================================================
# CONFIGURATION
# ============================================================================
MQTT_BROKER = "localhost"           # Address of the MQTT broker
MQTT_PORT = 8883                     # Port for MQTT connection
MAX_WORKERS = 3                      # Maximum number of concurrent processing threads

# Certificate paths for TLS authentication
CA_CERT = "/home/xilinx/b23/certs/ca.crt"
CLIENT_CERT = "/home/xilinx/b23/certs/ultra96.crt"
CLIENT_KEY = "/home/xilinx/b23/certs/ultra96.key"

# MQTT topics for communication
TOPICS = {
    "incoming": {
        "from_laptop": "laptop/to_ultra96",      # Topic for ESP32 sensor data
        "system_commands": "system/command"      # Topic for system commands
    },
    "outgoing": {
        "predictions": "ultra96/prediction",     # Topic for sending predictions
        "status": "ultra96/status",              # Topic for status updates
        "errors": "system/error"                 # Topic for error reporting
    }
}

# ============================================================================
# MESSAGE TRACKING
# ============================================================================
class MessageTracker:
    
    #Tracks messages received, processed, and sent by the Ultra96.
    #Provides statistics for monitoring system performance.
    
    
    def __init__(self, max_history=1000):
    
        # Initialize the message tracker with empty data structures
        self.received_messages = {}       # Messages received from laptop
        self.processed_messages = {}      # Messages that have been processed
        self.sent_responses = {}          # Responses sent back to laptop
        self.latencies = []                # Processing latency measurements
        self.lock = threading.Lock()       # Thread lock for concurrent access
        self.max_history = max_history     # Maximum history size
        self.sequence_check = defaultdict(lambda: -1)  # Track sequence numbers
        
    def track_received(self, msg_id, source, timestamp=None):
        
        # Record when a message is received from the laptop
        with self.lock:
            if timestamp is None:
                timestamp = time.time()
            
            self.received_messages[msg_id] = {
                "timestamp": timestamp,
                "source": source,
                "status": "received"
            }
            
            # Check for sequence gaps in ESP32 batches
            if "ESP32_BATCH" in msg_id:
                try:
                    seq = int(msg_id.split("_")[-1])
                    expected = self.sequence_check["esp32"] + 1
                    if expected > 0 and seq != expected:
                        print(f"Sequence gap: expected {expected}, got {seq}")
                    self.sequence_check["esp32"] = seq
                except:
                    pass
            
            # Maintain history size limit
            if len(self.received_messages) > self.max_history:
                oldest = min(self.received_messages.keys(), 
                           key=lambda k: self.received_messages[k]["timestamp"])
                del self.received_messages[oldest]
    
    def track_processed(self, msg_id, processing_time):
        # Record when a message has been processed
        with self.lock:
            self.processed_messages[msg_id] = {
                "timestamp": time.time(),
                "processing_time": processing_time
            }
    
    def track_sent(self, msg_id, destination):
        
        #Record when a response is sent
        with self.lock:
            self.sent_responses[msg_id] = {
                "timestamp": time.time(),
                "destination": destination
            }
    
    def get_statistics(self):
        # Get current tracking statistics
        with self.lock:
            return {
                "received": len(self.received_messages),
                "processed": len(self.processed_messages),
                "responses_sent": len(self.sent_responses),
                "queue_depth": len(active_requests) if 'active_requests' in globals() else 0
            }

# Global variables
client = None                          # MQTT client instance
connected = False                       # Connection status flag
use_tls = True                          # Whether to use TLS encryption
executor = ThreadPoolExecutor(max_workers=MAX_WORKERS)  # Thread pool for parallel processing
active_requests = {}                    # Dictionary of currently active requests
request_lock = threading.Lock()          # Thread lock for request management
processed_count = 0                      # Total number of processed messages
tracker = MessageTracker()                # Message tracking instance

# ============================================================================
# MQTT CALLBACK FUNCTIONS
# ============================================================================
def on_connect(client, userdata, flags, rc):
    
    # Called when connection to MQTT broker is established
    # Subscribes to incoming topics and sends startup status
    global connected
    
    if rc == 0:
        connected = True
        print("Connected to laptop broker")
        print(f"Thread pool ready: {MAX_WORKERS} workers")
        
        for topic in TOPICS["incoming"].values():
            client.subscribe(topic)
            print(f"Subscribed to: {topic}")
            
        send_status("STARTED")
        print("\nUltra96 Processor Running")
        print("Commands: status, stats, quit")
    else:
        connected = False
        print(f"Connection failed: {rc}")

def on_message(client, userdata, msg):
    #Main message handler routes messages based on topic
    if msg.topic == TOPICS["incoming"]["from_laptop"]:
        handle_laptop_message(msg)
    elif msg.topic == TOPICS["incoming"]["system_commands"]:
        handle_system_command(msg)

# ============================================================================
# MESSAGE HANDLERS
# ============================================================================
def handle_laptop_message(msg):
    try:
        data = json.loads(msg.payload.decode())
        
        # Extract message ID from various possible field names
        original_id = None
        if "batch_id" in data:
            original_id = data["batch_id"]
        elif "original_id" in data:
            original_id = data["original_id"]
        elif "id" in data:
            original_id = data["id"]
        elif "request_id" in data:
            original_id = data["request_id"]
        
        if not original_id:
            print("Message missing ID, cannot process")
            return
        
        print(f"\nReceived: {original_id}")
        print(f"  Device: {data.get('device', 'unknown')}")
        print(f"  Samples: {data.get('batch_size', 1)}")
        
        # Track the received message
        tracker.track_received(original_id, data.get("device", "unknown"))
        
        # Generate internal ID for tracking
        internal_id = f"internal_{uuid.uuid4().hex[:8]}"
        
        # Store request in active queue
        with request_lock:
            active_requests[internal_id] = {
                "start_time": time.time(),
                "status": "queued",
                "data": data,
                "original_id": original_id
            }
        
        # Submit to thread pool for processing
        future = executor.submit(process_with_ai, internal_id, original_id)
        future.add_done_callback(lambda f: on_processing_complete(f, internal_id))
        
        print(f"  Active: {len(active_requests)}")
        
    except json.JSONDecodeError as e:
        print(f"  JSON Error: {e}")
    except Exception as e:
        print(f"  Error: {e}")

def process_with_ai(internal_id, original_id):
    try:
        # Update status to processing
        with request_lock:
            if internal_id not in active_requests:
                return None
            active_requests[internal_id]["status"] = "processing"
        
        # Simulate processing time (0.1-0.3 seconds)
        processing_time = random.uniform(0.1, 0.3)
        time.sleep(processing_time)
        
        # Generate random prediction and confidence
        prediction = random.randint(0, 99)
        confidence = random.uniform(0.7, 0.99)
        
        result = {
            "prediction": prediction,
            "confidence": round(confidence, 2),
            "processing_time": round(processing_time, 3),
            "original_id": original_id,
            "timestamp": time.time()
        }
        
        # Track processing completion
        tracker.track_processed(original_id, processing_time)
        
        return result
        
    except Exception as e:
        print(f"  Processing error: {e}")
        return None

def on_processing_complete(future, internal_id):
    global processed_count
    
    try:
        result = future.result(timeout=5.0)
        
        if not result:
            return
        
        # Remove from active requests and calculate total time
        with request_lock:
            if internal_id in active_requests:
                original_id = active_requests[internal_id]["original_id"]
                start_time = active_requests[internal_id]["start_time"]
                total_time = time.time() - start_time
                
                del active_requests[internal_id]
                processed_count += 1
            else:
                return
        
        result["total_time"] = round(total_time, 3)
        
        # Send prediction back to laptop
        success = send_prediction(result, original_id)
        
        if success:
            print(f"  Response sent for {original_id}")
            print(f"     Prediction: {result['prediction']}, Confidence: {result['confidence']:.2f}")
        else:
            print(f"  Failed to send response for {original_id}")
            
    except Exception as e:
        print(f"  Callback error: {e}")

def send_prediction(result, original_id):
    if not client or not connected:
        return False
    
    # Prepare prediction data
    prediction_data = {
        "device": "ultra96",
        "type": "prediction",
        "prediction": result["prediction"],
        "confidence": result["confidence"],
        "processing_time": result["processing_time"],
        "total_time": result["total_time"],
        "timestamp": time.time(),
        "response_id": original_id,
        "batch_id": original_id,
        "verified": True
    }
    
    try:
        message = json.dumps(prediction_data)
        info = client.publish(TOPICS["outgoing"]["predictions"], message)
        
        if info.rc == mqtt.MQTT_ERR_SUCCESS:
            tracker.track_sent(original_id, "laptop")
            return True
        else:
            return False
    except Exception as e:
        print(f"  Publish error: {e}")
        return False

def send_status(status):
    with request_lock:
        active_count = len(active_requests)
    
    status_data = {
        "device": "ultra96",
        "status": status,
        "timestamp": time.time(),
        "active_requests": active_count,
        "processed_count": processed_count
    }
    
    if client and connected:
        try:
            client.publish(TOPICS["outgoing"]["status"], json.dumps(status_data))
        except:
            pass

def handle_system_command(msg):
    try:
        command = msg.payload.decode()
        
        if command == "STATS":
            stats = tracker.get_statistics()
            print("\nStatistics:")
            print(f"  Received: {stats['received']}")
            print(f"  Processed: {stats['processed']}")
            print(f"  Responses: {stats['responses_sent']}")
            print(f"  Queue: {stats['queue_depth']}")
    except Exception as e:
        print(f"  Command error: {e}")

def shutdown():
    print("\nShutting down...")
    executor.shutdown(wait=True)  # Wait for all threads to complete
    if client:
        client.loop_stop()
        client.disconnect()
    os._exit(0)

# ============================================================================
# TLS FUNCTIONS
# ============================================================================
def verify_certificates():
    missing = []
    for cert_path in [CA_CERT, CLIENT_CERT, CLIENT_KEY]:
        if not os.path.exists(cert_path):
            missing.append(cert_path)
    
    if missing:
        print("Missing certificates:")
        for m in missing:
            print(f"  - {m}")
        return False
    return True

def setup_tls_connection(client_instance):
    try:
        client_instance.tls_set(
            ca_certs=CA_CERT,
            certfile=CLIENT_CERT,
            keyfile=CLIENT_KEY,
            cert_reqs=ssl.CERT_REQUIRED,
            tls_version=ssl.PROTOCOL_TLSv1_2
        )
        client_instance.tls_insecure_set(True)
        return True
    except Exception as e:
        print(f"TLS setup error: {e}")
        return False

def test_tls_connection():
    print("\nTesting TLS connection...")
    test_client = mqtt.Client()
    
    try:
        if not setup_tls_connection(test_client):
            return False
        
        test_client.connect(MQTT_BROKER, MQTT_PORT, 5)
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
    global client, connected, use_tls, MQTT_PORT
    
    print("="*60)
    print("Ultra96 Processor")
    print(f"Max concurrent: {MAX_WORKERS}")
    print("="*60)
    
    # Verify certificates if using TLS
    if not verify_certificates():
        print("\nMissing certificates. TLS disabled.")
        use_tls = False
        MQTT_PORT = 1883
    
    # Test TLS connection if enabled
    if use_tls:
        if not test_tls_connection():
            print("\nTLS failed. Switching to non-TLS.")
            use_tls = False
            MQTT_PORT = 1883
    
    # Create MQTT client
    client = mqtt.Client()
    
    # Setup TLS if enabled
    if use_tls:
        if not setup_tls_connection(client):
            print("Failed to setup TLS. Switching to non-TLS.")
            use_tls = False
            MQTT_PORT = 1883
            client = mqtt.Client()
    
    print(f"\nBroker: {MQTT_BROKER}:{MQTT_PORT}")
    print(f"TLS: {'Enabled' if use_tls else 'Disabled'}")
    
    # Set callbacks
    client.on_connect = on_connect
    client.on_message = on_message
    
    try:
        # Connect to broker
        client.connect(MQTT_BROKER, MQTT_PORT, 60)
        client.loop_start()
        
        # Wait for connection to establish
        for i in range(10):
            if connected:
                break
            time.sleep(1)
        
        if not connected:
            print("Failed to connect")
            sys.exit(1)
        
        # Main command loop
        while True:
            try:
                cmd = input("\nUltra96 > ").strip().lower()
                
                if cmd == "status":
                    # Display current status
                    with request_lock:
                        print(f"\nConnected: {connected}")
                        print(f"Active: {len(active_requests)}")
                        print(f"Processed: {processed_count}")
                        
                elif cmd == "stats":
                    # Display statistics
                    stats = tracker.get_statistics()
                    print(f"\nReceived: {stats['received']}")
                    print(f"Processed: {stats['processed']}")
                    print(f"Responses: {stats['responses_sent']}")
                    print(f"Queue: {stats['queue_depth']}")
                    
                elif cmd == "quit":
                    # Shutdown application
                    shutdown()
                    break
                    
            except KeyboardInterrupt:
                shutdown()
                break
                
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()