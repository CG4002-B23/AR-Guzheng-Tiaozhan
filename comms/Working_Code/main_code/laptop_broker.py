import paho.mqtt.client as mqtt
import json
import time
import threading
import socket
import ssl
import os
from collections import defaultdict
import datetime
from queue import Queue

# ============================================================================
# ANSI COLOR CODES
# ============================================================================
class Colors:
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    CYAN = '\033[96m'
    MAGENTA = '\033[95m'
    RED = '\033[91m'
    RESET = '\033[0m'

# ============================================================================
# CONFIGURATION
# ============================================================================
MQTT_BROKER = "localhost"
MQTT_PORT = 8883
UDP_DISCOVERY_PORT = 18883

NUM_WORKER_THREADS = 4
QUEUE_SIZE = 1000
BATCH_FORWARD_SIZE = 1
BATCH_TIMEOUT_MS = 10
GESTURE_COOLDOWN = 0.3

# Certificate paths
CA_CERT = "/home/eugene/Desktop/CG4002/Working_Code/mqtt_certs/ca/ca.crt"
CLIENT_CERT = "/home/eugene/Desktop/CG4002/Working_Code/mqtt_certs/client/laptop_broker.crt"
CLIENT_KEY = "/home/eugene/Desktop/CG4002/Working_Code/mqtt_certs/client/laptop_broker.key"

TOPICS = {
    "sub": {
        "esp32": "esp32/scan",
        "ultra96_pred": "ultra96/prediction",
        "ultra96_status": "ultra96/status",
        "unity": "unity/to_broker",
        "system": "system/status"
    },
    "pub": {
        "to_ultra96": "laptop/to_ultra96",
        "to_esp32": "laptop/to_esp32",
        "to_unity": "ar/visualizer",
        "system": "system/command"
    }
}

PRIORITY_HIGH = 0
PRIORITY_MEDIUM = 1
PRIORITY_LOW = 2

# ============================================================================
# CONNECTION STATUS TRACKER
# ============================================================================
class ConnectionStatus:
    def __init__(self):
        self.ultra96_connected = False
        self.ultra96_last_seen = None
        self.esp32_devices = {}
        self.unity_connected = False
        self.unity_client_id = None
        self.lock = threading.Lock()
    
    def update_ultra96(self, status_data):
        with self.lock:
            self.ultra96_connected = status_data.get("status") == "STARTED"
            self.ultra96_last_seen = time.time()
            if self.ultra96_connected:
                print(f"{Colors.CYAN}[STATUS] Ultra96 Connected - FPGA Ready: {status_data.get('fpga_ready', False)}{Colors.RESET}")
            else:
                print(f"{Colors.YELLOW}[STATUS] Ultra96 Disconnected{Colors.RESET}")
    
    def update_esp32(self, device_id):
        with self.lock:
            if device_id not in self.esp32_devices:
                self.esp32_devices[device_id] = {"last_seen": time.time(), "connected": True}
                print(f"{Colors.GREEN}[STATUS] ESP32 Connected: {device_id}{Colors.RESET}")
            else:
                self.esp32_devices[device_id]["last_seen"] = time.time()
    
    def update_unity(self, client_id, connected=True):
        with self.lock:
            if connected:
                self.unity_connected = True
                self.unity_client_id = client_id
                print(f"{Colors.MAGENTA}[STATUS] Visualizer Connected: {client_id}{Colors.RESET}")
            else:
                self.unity_connected = False
                self.unity_client_id = None
                print(f"{Colors.YELLOW}[STATUS] Visualizer Disconnected{Colors.RESET}")
    
    def get_status(self):
        with self.lock:
            return {
                "ultra96": self.ultra96_connected,
                "esp32": list(self.esp32_devices.keys()),
                "unity": self.unity_connected
            }

connection_status = ConnectionStatus()

# ============================================================================
# MESSAGE TRACKER
# ============================================================================
class MessageTracker:
    def __init__(self, max_history=1000):
        self.sent = {}
        self.received = {}
        self.lost = set()
        self.mismatches = defaultdict(int)
        self.latencies = []
        self.lock = threading.Lock()
        self.max_history = max_history
        
    def track_sent(self, msg_id, source, dest):
        with self.lock:
            self.sent[msg_id] = {
                "ts": time.time(),
                "src": source,
                "dest": dest,
                "status": "sent"
            }
            
            if len(self.sent) > self.max_history:
                to_remove = sorted(self.sent.keys(), 
                                key=lambda k: self.sent[k]["ts"])[:200]
                for k in to_remove:
                    del self.sent[k]
    
    def track_received(self, msg_id, dest):
        with self.lock:
            if msg_id in self.sent:
                sent = self.sent[msg_id]["ts"]
                latency = (time.time() - sent) * 1000
                
                if len(self.latencies) >= 1000:
                    self.latencies.pop(0)
                self.latencies.append(latency)
                
                self.sent[msg_id]["status"] = "received"
                self.sent[msg_id]["latency"] = latency
                return True
            else:
                self.mismatches["unknown"] += 1
                return False
    
    def get_stats(self):
        with self.lock:
            total = len(self.sent)
            received = len([m for m in self.sent.values() if m["status"] == "received"])
            lost = len(self.lost)
            
            lat = self.latencies[-100:] if self.latencies else []
            return {
                "sent": total,
                "received": received,
                "lost": lost,
                "pending": total - received - lost,
                "loss_rate": (lost / total * 100) if total else 0,
                "latency": {
                    "avg": sum(lat)/len(lat) if lat else 0,
                    "min": min(lat) if lat else 0,
                    "max": max(lat) if lat else 0,
                    "samples": len(lat)
                }
            }

# ============================================================================
# BATCH FORWARDER
# ============================================================================
class BatchForwarder:
    def __init__(self, mqtt_client, max_batch_size=1, timeout_ms=10):
        self.client = mqtt_client
        self.max_batch_size = max_batch_size
        self.timeout = timeout_ms / 1000.0
        self.batch = []
        self.batch_lock = threading.Lock()
        self.last_flush = time.time()
        self.running = True
        
        self.flush_thread = threading.Thread(target=self._periodic_flush, daemon=True)
        self.flush_thread.start()
    
    def add_message(self, data, msg_id):
        with self.batch_lock:
            self.batch.append((data, msg_id))
            if len(self.batch) >= self.max_batch_size:
                self._flush()
    
    def _flush(self):
        if not self.batch:
            return
        
        batch_to_send = self.batch.copy()
        self.batch.clear()
        self.last_flush = time.time()
        
        for data, msg_id in batch_to_send:
            self.client.publish(TOPICS["pub"]["to_ultra96"], 
                            json.dumps(data))
    
    def _periodic_flush(self):
        while self.running:
            time.sleep(self.timeout)
            with self.batch_lock:
                if self.batch and (time.time() - self.last_flush) >= self.timeout:
                    self._flush()
    
    def stop(self):
        self.running = False
        with self.batch_lock:
            if self.batch:
                self._flush()

# ============================================================================
# MESSAGE QUEUE SYSTEM
# ============================================================================
class MessageQueue:
    def __init__(self, num_workers=4):
        self.queues = {
            PRIORITY_HIGH: Queue(maxsize=QUEUE_SIZE),
            PRIORITY_MEDIUM: Queue(maxsize=QUEUE_SIZE),
            PRIORITY_LOW: Queue(maxsize=QUEUE_SIZE)
        }
        self.workers = []
        self.running = True
        self.num_workers = num_workers
        self.processed_count = 0
        self.stats_lock = threading.Lock()
        
    def start(self):
        for i in range(self.num_workers):
            worker = threading.Thread(target=self._worker, daemon=True)
            worker.start()
            self.workers.append(worker)
    
    def put(self, priority, handler_func, data, msg_id):
        try:
            self.queues[priority].put((handler_func, data, msg_id), block=False)
        except:
            pass
    
    def _worker(self):
        while self.running:
            for priority in [PRIORITY_HIGH, PRIORITY_MEDIUM, PRIORITY_LOW]:
                try:
                    handler_func, data, msg_id = self.queues[priority].get(timeout=0.01)
                    handler_func(data, msg_id)
                    
                    with self.stats_lock:
                        self.processed_count += 1
                    
                    break
                except:
                    continue
    
    def stop(self):
        self.running = False
        for worker in self.workers:
            worker.join(timeout=2)
    
    def get_stats(self):
        with self.stats_lock:
            return {
                "processed": self.processed_count,
                "queue_sizes": {
                    "high": self.queues[PRIORITY_HIGH].qsize(),
                    "medium": self.queues[PRIORITY_MEDIUM].qsize(),
                    "low": self.queues[PRIORITY_LOW].qsize()
                }
            }

# ============================================================================
# GLOBALS
# ============================================================================
tracker = MessageTracker()
unity_clients = {}
unity_lock = threading.Lock()
esp32_devices = {}
esp32_lock = threading.Lock()
client = None
connected = False
message_queue = None
batch_forwarder = None
last_gesture_sent = {}
last_gesture_lock = threading.Lock()

# ============================================================================
# UDP DISCOVERY
# ============================================================================
def get_local_ip():
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except:
        return "127.0.0.1"

def start_udp_announcer():
    global udp_socket, udp_running
    
    try:
        udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        udp_socket.bind(('', UDP_DISCOVERY_PORT))
        udp_socket.settimeout(0.5)
        udp_running = True
        
        def announce_loop():
            last_broadcast = 0
            while udp_running:
                try:
                    now = time.time()
                    if now - last_broadcast >= 2:
                        local_ip = get_local_ip()
                        message = f"MQTT_BROKER:{local_ip}:{MQTT_PORT}"
                        udp_socket.sendto(message.encode(), 
                                        ('255.255.255.255', UDP_DISCOVERY_PORT))
                        last_broadcast = now
                    
                    try:
                        data, addr = udp_socket.recvfrom(1024)
                        if data.decode() == "DISCOVER_MQTT_BROKER":
                            local_ip = get_local_ip()
                            message = f"MQTT_BROKER:{local_ip}:{MQTT_PORT}"
                            udp_socket.sendto(message.encode(), addr)
                    except socket.timeout:
                        pass
                    
                    time.sleep(0.1)
                except Exception as e:
                    time.sleep(1)
        
        thread = threading.Thread(target=announce_loop, daemon=True)
        thread.start()
    except Exception as e:
        pass

udp_socket = None
udp_running = False

def stop_udp_announcer():
    global udp_running, udp_socket
    udp_running = False
    if udp_socket:
        udp_socket.close()

# ============================================================================
# MESSAGE HANDLERS
# ============================================================================
def handle_esp32_fast(data, msg_id):
    dev = data.get("device_id", "unknown")
    
    with esp32_lock:
        esp32_devices[dev] = {"last": time.time(), "msg": msg_id}
    
    # Update connection status
    connection_status.update_esp32(dev)
    
    tracker.track_sent(msg_id, dev, "ultra96")
    batch_forwarder.add_message(data, msg_id)

def handle_ultra96_fast(data, msg_id):
    target = data.get("target_device")
    
    gesture_id = data.get("gesture_id")
    if gesture_id == 0 or gesture_id == "null" or gesture_id is None:
        return

    glove_id = data.get("glove_id", "unknown")
    current_time = time.time()
    
    with last_gesture_lock:
        key = f"{glove_id}_{gesture_id}"
        last_time = last_gesture_sent.get(key, 0)
        if current_time - last_time < GESTURE_COOLDOWN:
            return
        last_gesture_sent[key] = current_time
    
    gestures = ['null', 'thumb', 'index', 'middle', 'dragon_claw',
                'snake_strike', 'one_inch_punch', 'god_chop', 'crane_wing']
    prediction_idx = data.get("gesture_id", 'N/A')

    if prediction_idx == 0:
        return

    # ONLY PRINT HERE - when sending valid gesture to Unity
    gesture_name = gestures[prediction_idx] if isinstance(prediction_idx, int) else prediction_idx
    hand = "Left" if data.get("glove_id") == "FB_001" else "Right"
    confidence = data.get('confidence', 'N/A')
    
    print(f"\n{Colors.GREEN}[GESTURE] {gesture_name} ({hand}) - Confidence: {confidence:.3f}{Colors.RESET}")
    
    if msg_id != "unknown":
        tracker.track_received(msg_id, "laptop")
    
    if target:
        data["response_id"] = msg_id
        topic = f"laptop/to_esp32/{target}"
        client.publish(topic, json.dumps(data))
    
    message_queue.put(PRIORITY_MEDIUM, forward_to_unity_fast, data, msg_id)

def handle_unity_fast(data, msg_id):
    if data.get("type") == "trigger":
        target = data.get("target_device", "FB_001")
        action = data.get("action", "")
        print(f"\n{Colors.GREEN}[TRIGGER] {action} -> {target}{Colors.RESET}")
        
        trigger = {
            "type": "trigger",
            "action": action,
            "target_device": target,
            "timestamp": time.time(),
            "source": "unity"
        }
        
        client.publish(f"laptop/to_esp32/{target}", json.dumps(trigger))
        
        client.publish(TOPICS["pub"]["to_unity"], json.dumps({
            "type": "trigger_confirmation",
            "target_device": target,
            "action": action,
            "status": "sent"
        }))
    
    elif data.get("type") == "connection":
        client_id = data.get('client_id', 'N/A')
        connection_status.update_unity(client_id, True)
        with unity_lock:
            unity_clients[client_id] = {"ts": time.time(), "status": "connected"}
        
        client.publish(TOPICS["pub"]["to_unity"], json.dumps({
            "type": "welcome",
            "client_id": client_id,
            "broker_ip": get_local_ip(),
            "status": "connected"
        }))

def forward_to_unity_fast(data, msg_id):
    # Prepare the message
    message = {
        "type": "prediction",
        "device_id": data.get("glove_id"),
        "prediction": data.get("gesture_id"),
        "confidence": data.get("confidence"),
        "request_id": msg_id,
        "player": data.get("player", 1),
        "source": "ultra96"
    }
    
    # Print what we are sending to unity
    print(f"{Colors.YELLOW}[DEBUG] Sending to Unity: {json.dumps(message)}{Colors.RESET}")
    
    # Publish to Unity
    result = client.publish(TOPICS["pub"]["to_unity"], json.dumps(message))
    
    # Check if publish was successful
    if result.rc == mqtt.MQTT_ERR_SUCCESS:
        print(f"{Colors.GREEN}[DEBUG] Successfully sent to Unity topic: {TOPICS['pub']['to_unity']}{Colors.RESET}")
    else:
        print(f"{Colors.RED}[DEBUG] Failed to send to Unity. Error code: {result.rc}{Colors.RESET}")
# ============================================================================
# MQTT CALLBACKS
# ============================================================================
def on_connect(client, userdata, flags, rc, props=None):
    global connected
    
    if rc == 0:
        connected = True
        for name, topic in TOPICS["sub"].items():
            client.subscribe(topic)
        print(f"{Colors.GREEN}MQTT Broker connected - Ready{Colors.RESET}")
    else:
        connected = False
        print(f"{Colors.RED}Connection failed: {rc}{Colors.RESET}")

def on_message(client, userdata, msg):
    try:
        data = json.loads(msg.payload)
        
        if msg.topic == TOPICS["sub"]["esp32"]:
            msg_id = data.get("window_id") or data.get("batch_id") or data.get("request_id") or "unknown"
            message_queue.put(PRIORITY_LOW, handle_esp32_fast, data, msg_id)
            
        elif msg.topic == TOPICS["sub"]["ultra96_pred"]:
            msg_id = data.get("response_id") or data.get("batch_id") or "unknown"
            message_queue.put(PRIORITY_MEDIUM, handle_ultra96_fast, data, msg_id)
            
        elif msg.topic == TOPICS["sub"]["unity"]:
            msg_id = data.get("id", "unknown")
            message_queue.put(PRIORITY_HIGH, handle_unity_fast, data, msg_id)
            
        elif msg.topic == TOPICS["sub"]["ultra96_status"]:
            # Update Ultra96 connection status
            connection_status.update_ultra96(data)
            
    except Exception as e:
        pass

def on_disconnect(client, userdata, rc, props=None):
    global connected
    connected = False
    print(f"{Colors.YELLOW}MQTT Broker disconnected{Colors.RESET}")

# ============================================================================
# TLS SETUP
# ============================================================================
def setup_tls(mqtt_client):
    try:
        mqtt_client.tls_set(CA_CERT, CLIENT_CERT, CLIENT_KEY,
                        cert_reqs=ssl.CERT_REQUIRED,
                        tls_version=ssl.PROTOCOL_TLSv1_2)
        mqtt_client.tls_insecure_set(True)
        return True
    except Exception as e:
        return False

def verify_certs():
    for path in [CA_CERT, CLIENT_CERT, CLIENT_KEY]:
        if not os.path.exists(path):
            return False
    return True

# ============================================================================
# UTILITIES
# ============================================================================
def list_devices():
    with esp32_lock:
        if not esp32_devices:
            print("No ESP32 devices")
            return
        
        print(f"\nESP32 Devices:")
        print("-" * 40)
        for dev_id, info in esp32_devices.items():
            last = datetime.datetime.fromtimestamp(info["last"]).strftime("%H:%M:%S")
            print(f"  {dev_id}: Last seen {last}")

def show_connection_status():
    status = connection_status.get_status()
    print("\n" + "="*50)
    print("CONNECTION STATUS")
    print("="*50)
    print(f"Ultra96: {'✓ Connected' if status['ultra96'] else '✗ Disconnected'}")
    print(f"ESP32 Devices: {', '.join(status['esp32']) if status['esp32'] else 'None'}")
    print(f"Visualizer: {'✓ Connected' if status['unity'] else '✗ Disconnected'}")
    print("="*50)

# ============================================================================
# MAIN
# ============================================================================
def main():
    global client, connected, message_queue, batch_forwarder
    
    print("="*50)
    print("Laptop MQTT Forwarder - Gesture Mode")
    print("="*50)
    
    if not verify_certs():
        print("Certificate error")
        return
    
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    if not setup_tls(client):
        print("TLS setup failed")
        return
    
    client.on_connect = on_connect
    client.on_message = on_message
    client.on_disconnect = on_disconnect
    
    client.connect(MQTT_BROKER, MQTT_PORT, 60)
    client.loop_start()
    
    for i in range(10):
        if connected:
            break
        time.sleep(1)
    
    if not connected:
        print("Failed to connect")
        return
    
    message_queue = MessageQueue(num_workers=NUM_WORKER_THREADS)
    message_queue.start()
    
    batch_forwarder = BatchForwarder(client, 
                                    max_batch_size=BATCH_FORWARD_SIZE,
                                    timeout_ms=BATCH_TIMEOUT_MS)
    
    start_udp_announcer()
    
    print(f"\n{Colors.GREEN}Ready! Waiting for gestures{Colors.RESET}\n")
    
    while True:
        try:
            cmd = input(f"\nbroker> ").strip().lower()
            
            if cmd == "stats":
                s = tracker.get_stats()
                q = message_queue.get_stats()
                print("\n" + "="*50)
                print("Statistics")
                print("="*50)
                print(f"Sent: {s['sent']} | Received: {s['received']}")
                if s['latency']['samples']:
                    print(f"Latency: avg={s['latency']['avg']:.1f}ms")
                print(f"Queue sizes: H:{q['queue_sizes']['high']} "
                    f"M:{q['queue_sizes']['medium']} "
                    f"L:{q['queue_sizes']['low']}")
                print("="*50)
                
            elif cmd == "devices":
                list_devices()
                
            elif cmd == "status":
                show_connection_status()
                
            elif cmd in ["quit", "exit", "q"]:
                print("Shutting down")
                batch_forwarder.stop()
                message_queue.stop()
                stop_udp_announcer()
                client.loop_stop()
                client.disconnect()
                break
                
        except KeyboardInterrupt:
            print("\nShutting down")
            batch_forwarder.stop()
            message_queue.stop()
            stop_udp_announcer()
            client.loop_stop()
            client.disconnect()
            break

if __name__ == "__main__":
    main()