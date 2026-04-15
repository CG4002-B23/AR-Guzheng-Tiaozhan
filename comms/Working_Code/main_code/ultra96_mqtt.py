import paho.mqtt.client as mqtt
import json
import time
import sys
import numpy as np
import ssl
import os
import signal
from datetime import datetime
from collections import deque

# ============================================================================
# CONFIGURATION
# ============================================================================
MQTT_BROKER  = "localhost"
MQTT_PORT    = 8883
T_STEPS      = 20
FEATURE_COLS = 10   # ax, ay, az, gx, gy, gz, f1, f2, f3, mag
WINDOW_SIZE  = 20

CA_CERT     = "/home/xilinx/b23/certs/ca.crt"
CLIENT_CERT = "/home/xilinx/b23/certs/ultra96.crt"
CLIENT_KEY  = "/home/xilinx/b23/certs/ultra96.key"

TOPIC_SUBSCRIBE  = "laptop/to_ultra96"
TOPIC_PUBLISH    = "laptop/to_esp32/FB_002"
TOPIC_PREDICTIONS = "ultra96/prediction"
TOPIC_STATUS     = "ultra96/status"

LOG_FILE = "received_packets.log"

INFERENCE_STRIDE = 7

# ============================================================================
# GLOBALS
# ============================================================================
client      = None
connected   = False
running     = True

GLOVE_STATE = {}

# ============================================================================
# FPGA INFERENCE
# ============================================================================
try:
    from inference import run_fpga_inference as fpga_infer
    FPGA_AVAILABLE = True
    print("FPGA inference module loaded successfully")
except ImportError as e:
    print(f"Warning: Could not import inference module: {e}")
    print("Will use mock inference for testing")
    FPGA_AVAILABLE = False
    def fpga_infer(input_array):
        time.sleep(0.03)
        gesture_id = np.random.randint(0, 6)
        scores = np.random.rand(6)
        scores[gesture_id] = 0.8
        return gesture_id, scores

# ============================================================================
# LOGGING
# ============================================================================
def log_packet(data, topic):
    try:
        ts       = datetime.now().strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]
        batch_id = data.get("batch_id", data.get("window_id", "unknown"))
        dev_id   = data.get("device_id", "unknown")
        with open(LOG_FILE, 'a') as f:
            f.write(f"\n{'='*60}\n")
            f.write(f"Time: {ts}\nTopic: {topic}\nDevice: {dev_id}\nID: {batch_id}\n")
            if "samples" in data:
                samples = data.get("samples", [])
                f.write(f"Samples: {len(samples)}\n")
            f.write(f"{'='*60}\n")
    except Exception as e:
        print(f"Logging error: {e}")

# ============================================================================
# FEATURE EXTRACTION
# ============================================================================
def extract_features(sample):
    flex = sample.get("flex", [0, 0, 0])
    f1 = flex[0] if len(flex) > 0 else 0.0
    f2 = flex[1] if len(flex) > 1 else 0.0
    f3 = flex[2] if len(flex) > 2 else 0.0

    acc  = sample.get("acc",  [0, 0, 0])
    gyro = sample.get("gyro", [0, 0, 0])
    ax, ay, az = acc[0],  acc[1],  acc[2]
    gx, gy, gz = gyro[0], gyro[1], gyro[2]
    mag = float(np.sqrt(ax*ax + ay*ay + az*az))

    return [ax, ay, az, gx, gy, gz, f1, f2, f3, mag]

# ============================================================================
# GLOVE HELPERS
# ============================================================================
def _ensure_glove(glove_id):
    if glove_id not in GLOVE_STATE:
        GLOVE_STATE[glove_id] = {
            'window':           deque(maxlen=WINDOW_SIZE),
            'samples_since_last_inference': 0,
            'inference_count':  0,
            'total_samples_received': 0,
            'last_stats_time':  time.time(),
        }

def publish_prediction(glove_id, gesture_id, scores, window_id):
    payload = {
        "device":       "ultra96",
        "type":         "prediction",
        "glove_id":     glove_id,
        "gesture_id":   int(gesture_id),
        "confidence":   float(np.max(scores)) if scores is not None else 0.0,
        "scores":       scores.tolist() if scores is not None else [],
        "timestamp":    time.time(),
        "window_id":    window_id,
        "inference_stride": INFERENCE_STRIDE,
    }
    
    payload_str = json.dumps(payload)
    result = client.publish(TOPIC_PREDICTIONS, payload_str, qos=1)
    
    if result.rc == mqtt.MQTT_ERR_SUCCESS:
        print(f"[PUBLISHED] {TOPIC_PREDICTIONS} - Gesture {gesture_id} (conf={payload['confidence']:.2f})")
    else:
        print(f"[PUBLISH FAILED] Error code: {result.rc}")

# ============================================================================
# SLIDING WINDOW WITH STRIDE
# ============================================================================
def process_sliding_window(glove_id, samples, window_id):
    _ensure_glove(glove_id)
    glove = GLOVE_STATE[glove_id]
    
    for sample in samples:
        features = extract_features(sample)
        glove['window'].append(features)
        glove['total_samples_received'] += 1
        glove['samples_since_last_inference'] += 1
    
    if len(glove['window']) < WINDOW_SIZE:
        return
    
    if glove['samples_since_last_inference'] >= INFERENCE_STRIDE:
        glove['samples_since_last_inference'] = 0
        
        input_array = np.array(glove['window'], dtype=np.float32)
        
        try:
            inference_start = time.time()
            gesture_id, scores = fpga_infer(input_array)
            inference_time_ms = (time.time() - inference_start) * 1000
            
            glove['inference_count'] += 1
            
            elapsed_time = time.time() - glove.get('last_stats_time', time.time())
            if elapsed_time >= 5.0:
                rate = glove['inference_count'] / elapsed_time
                print(f"[{glove_id}] Stats: {rate:.1f} inferences/sec, samples: {glove['total_samples_received']}, stride: {INFERENCE_STRIDE}")
                glove['inference_count'] = 0
                glove['total_samples_received'] = 0
                glove['last_stats_time'] = time.time()
            
            print(f"[{glove_id}] Inference took {inference_time_ms:.1f}ms, gesture={gesture_id}")
            
            publish_prediction(glove_id, gesture_id, scores, window_id)
            
        except Exception as e:
            print(f"[{glove_id}] Inference error: {e}")
            return

# ============================================================================
# MQTT CALLBACKS
# ============================================================================
def on_connect(cli, userdata, flags, rc):
    global connected
    if rc == 0:
        connected = True
        cli.subscribe(TOPIC_SUBSCRIBE)
        
        input_hz = 100
        effective_hz = input_hz / INFERENCE_STRIDE
        
        cli.publish(TOPIC_STATUS, json.dumps({
            "device":           "ultra96",
            "status":           "STARTED",
            "fpga_ready":       FPGA_AVAILABLE,
            "feature_count":    FEATURE_COLS,
            "window_size":      WINDOW_SIZE,
            "window_type":      "sliding",
            "input_frequency_hz": input_hz,
            "inference_stride": INFERENCE_STRIDE,
            "effective_inference_hz": effective_hz,
            "timestamp":        time.time()
        }))
        
        print("\n" + "="*60)
        print("Ultra96 Ready - Sliding Window with Stride")
        print(f"  Window size: {WINDOW_SIZE} samples")
        print(f"  Inference stride: Every {INFERENCE_STRIDE} samples")
        print(f"  Effective rate: {effective_hz:.1f} inferences/second")
        print("="*60 + "\n")
        
        with open(LOG_FILE, 'w') as f:
            f.write(f"MQTT Packet Log - Started at {datetime.now()}\n")
    else:
        print(f"MQTT connect failed rc={rc}")

def on_message(cli, userdata, msg):
    try:
        data = json.loads(msg.payload.decode())
        log_packet(data, msg.topic)
        glove_id = data.get("device_id", "unknown")
        window_id = data.get("window_id", data.get("batch_id", "unknown"))

        if "samples" in data:
            process_sliding_window(glove_id, data["samples"], window_id)

        elif data.get("packet_type") == "CONNECT":
            print(f"Device {glove_id} connected")
            _ensure_glove(glove_id)
            cli.publish(TOPIC_PUBLISH, json.dumps({
                "device":      "ultra96",
                "response_id": data.get("batch_id", "unknown"),
                "status":      "ACK",
                "glove_id":    glove_id,
                "timestamp":   time.time()
            }))
    except Exception as e:
        print(f"Error in on_message: {e}")

def on_disconnect(cli, userdata, rc):
    global connected
    connected = False
    if rc != 0:
        print(f"Unexpected MQTT disconnect rc={rc}")

# ============================================================================
# TLS SETUP
# ============================================================================
def verify_certs():
    missing = [p for p in [CA_CERT, CLIENT_CERT, CLIENT_KEY]
               if not os.path.exists(p)]
    if missing:
        print(f"Missing certs: {missing}")
        B
        return False
    return True

def setup_tls(c):
    try:
        c.tls_set(CA_CERT, certfile=CLIENT_CERT, keyfile=CLIENT_KEY,
                  cert_reqs=ssl.CERT_REQUIRED)
        return True
    except Exception as e:
        print(f"TLS setup failed: {e}")
        return False

# ============================================================================
# SIGNAL HANDLER
# ============================================================================
def signal_handler(signum, frame):
    global running
    print("\nForce quitting")

    try:
        import inference
        if hasattr(inference, 'teardown'):
            inference.teardown()
    except Exception:
        pass
    os._exit(0)

# ============================================================================
# MAIN
# ============================================================================
def main():
    global client, connected, running

    signal.signal(signal.SIGINT,  signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    if not verify_certs():
        sys.exit(1)

    client = mqtt.Client()
    if not setup_tls(client):
        sys.exit(1)

    client.on_connect    = on_connect
    client.on_message    = on_message
    client.on_disconnect = on_disconnect

    try:
        client.connect(MQTT_BROKER, MQTT_PORT, 60)
    except Exception as e:
        print(f"MQTT connect failed: {e}")
        sys.exit(1)

    client.loop_start()

    for _ in range(50):
        if connected:
            break
        time.sleep(0.1)

    if not connected:
        print("Timed out waiting for MQTT broker")
        sys.exit(1)

    print("Waiting for data from gloves\n")

    while running:
        time.sleep(1)
        if not client.is_connected():
            try:
                client.reconnect()
            except Exception:
                pass

    print("\nStopping MQTT")
    client.loop_stop()
    client.disconnect()

    print("\n=== Final Statistics ===")
    for glove_id, glove in GLOVE_STATE.items():
        print(f"Glove {glove_id}:")
        print(f"  Total samples: {glove['total_samples_received']}")
        print(f"  Inference stride: {INFERENCE_STRIDE}")

    if FPGA_AVAILABLE:
        try:
            import inference
            if hasattr(inference, 'teardown'):
                inference.teardown()
        except Exception:
            pass

    print("Done.\n")

if __name__ == "__main__":
    main()
