import paho.mqtt.client as mqtt
import hashlib
import time
import os
import struct
import sys
import threading

CA_CERT = "/home/eugene/Desktop/CG4002/mqtt_certs/ca/ca.crt"
CLIENT_CERT = "/home/eugene/Desktop/CG4002/mqtt_certs/client/laptop_broker.crt"
CLIENT_KEY = "/home/eugene/Desktop/CG4002/mqtt_certs/client/laptop_broker.key"

BROKER_ADDRESS = "localhost" 
SOURCE_FILE = "10MB.zip"
DESTINATION_FILE = "reconstructed_10MB.zip"
SEGMENT_SIZE = 16384
PUBLISH_TOPIC = "esp32/file_transfer"
RESPONSE_TOPIC = "esp32/file_chunk"
STATUS_TOPIC = "esp32/file_ready"

collected_segments = {}
total_segments_expected = 0
operation_start = 0
current_segment_number = 0
device_ready_signal = threading.Event()
segment_received_signal = threading.Event()
retransmission_needed = threading.Event()

def compute_file_hash(filename):
    hasher = hashlib.md5()
    with open(filename, "rb") as f:
        for data_block in iter(lambda: f.read(4096), b""):
            hasher.update(data_block)
    return hasher.hexdigest()

def handle_incoming_messages(client, userdata, msg):
    global collected_segments, operation_start, current_segment_number

    if msg.topic == STATUS_TOPIC:
        message_text = msg.payload.decode()
        print(f"\n[ESP32] {message_text}")
        if "ONLINE" in message_text:
            if current_segment_number > 0:
                print(f"\n[RECOVERY] ESP32 back online. Resuming from chunk {current_segment_number}")
                retransmission_needed.set()
            device_ready_signal.set()
        return

    if msg.topic == RESPONSE_TOPIC:
        try:
            sequence_id = struct.unpack('<I', msg.payload[:4])[0]
            payload_data = msg.payload[4:]

            if sequence_id not in collected_segments:
                collected_segments[sequence_id] = payload_data
                
                if sequence_id == current_segment_number:
                    segment_received_signal.set()
                
                segments_completed = len(collected_segments)
                completion_percentage = (segments_completed / total_segments_expected) * 100
                time_elapsed = time.time() - operation_start

                if segments_completed > 1:
                    total_bits = segments_completed * (len(msg.payload) * 8)
                    transfer_rate = (total_bits / time_elapsed) / 1000
                    estimated_remaining = (total_segments_expected - segments_completed) * (time_elapsed / segments_completed)
                    sys.stdout.write(f"\rProgress: {completion_percentage:.2f}% | Speed: {transfer_rate:.2f} kbps | ETA: {estimated_remaining/60:.1f} min | Received: {segments_completed}/{total_segments_expected}")
                    sys.stdout.flush()
        except Exception as e:
            print(f"\nError processing message: {e}")

def handle_connection_established(client, userdata, flags, rc, properties=None):
    if rc == 0:
        print("Connected to broker")
        client.subscribe([(RESPONSE_TOPIC, 1), (STATUS_TOPIC, 1)])
    else:
        print(f"Connection failed with code {rc}")

mqtt_client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
mqtt_client.tls_set(ca_certs=CA_CERT, certfile=CLIENT_CERT, keyfile=CLIENT_KEY)
mqtt_client.on_connect = handle_connection_established
mqtt_client.on_message = handle_incoming_messages

try:
    print("Connecting to broker")
    mqtt_client.connect(BROKER_ADDRESS, 8883)
    mqtt_client.loop_start()

    time.sleep(2)

    if not os.path.exists(SOURCE_FILE):
        print(f"File {SOURCE_FILE} not found. Creating 10MB test file")
        with open(SOURCE_FILE, "wb") as f: 
            f.write(os.urandom(10 * 1024 * 1024))

    print("Calculating original MD5")
    source_hash = compute_file_hash(SOURCE_FILE)
    file_byte_size = os.path.getsize(SOURCE_FILE)
    total_segments_expected = (file_byte_size + SEGMENT_SIZE - 1) // SEGMENT_SIZE
    print(f"MD5: {source_hash}")
    print(f"Total chunks: {total_segments_expected}")

    print(f"\nWaiting for ESP32 ready signal on {STATUS_TOPIC}...")
    if not device_ready_signal.wait(timeout=30.0):
        print("ESP32 not detected - continuing anyway")
    else:
        print("ESP32 is ready")

    print("\n" + "="*40)
    print("Ready to start transfer")
    input("Press Enter to begin")
    print("="*40 + "\n")

    operation_start = time.time()
    
    with open(SOURCE_FILE, "rb") as f:
        while current_segment_number < total_segments_expected:
            segment_received_signal.clear()
            
            f.seek(current_segment_number * SEGMENT_SIZE)
            segment_data = f.read(SEGMENT_SIZE)
            sequence_header = struct.pack('<I', current_segment_number)

            mqtt_client.publish(PUBLISH_TOPIC, sequence_header + segment_data, qos=1)

            # Increased timeout from 10 to 30 seconds
            confirmed = segment_received_signal.wait(timeout=30.0)

            if confirmed:
                if current_segment_number % 10 == 0:
                    elapsed = time.time() - operation_start
                    print(f"\nChunk {current_segment_number} verified. Time: {elapsed:.1f}s")
                current_segment_number += 1
            else:
                print(f"\nTimeout waiting for chunk {current_segment_number}. Waiting for ESP32")
                
                device_ready_signal.clear()
                retransmission_needed.clear()
                
                # Wait for ESP32 to come back online
                if device_ready_signal.wait(timeout=60.0):  # Increased to 60 seconds
                    print(f"\nESP32 reconnected. Resuming from chunk {current_segment_number}")
                    
                    # Resend the current chunk immediately after reconnection
                    print(f"Resending chunk {current_segment_number}...")
                    f.seek(current_segment_number * SEGMENT_SIZE)
                    segment_data = f.read(SEGMENT_SIZE)
                    sequence_header = struct.pack('<I', current_segment_number)
                    mqtt_client.publish(PUBLISH_TOPIC, sequence_header + segment_data, qos=1)
                    
                    # Wait for confirmation with another timeout
                    if segment_received_signal.wait(timeout=30.0):
                        print(f"Chunk {current_segment_number} successfully resent")
                        current_segment_number += 1
                    else:
                        print(f"Failed to resend chunk {current_segment_number}. Exiting.")
                        break
                else:
                    print(f"\nESP32 did not reconnect. Exiting.")
                    break

    print("\nAll chunks sent. Reconstructing file")

    # Increased timeout from 60 to 120 seconds
    timeout_limit = time.time() + 120
    while len(collected_segments) < total_segments_expected and time.time() < timeout_limit:
        time.sleep(1)
        print(f"\rWaiting for chunks: {len(collected_segments)}/{total_segments_expected}", end="")
    
    print()

    if len(collected_segments) < total_segments_expected:
        print(f"\nOnly received {len(collected_segments)}/{total_segments_expected} chunks")
        missing = [i for i in range(total_segments_expected) if i not in collected_segments]
        print(f"Missing chunks: {missing[:10]}{'...' if len(missing) > 10 else ''}")
    else:
        with open(DESTINATION_FILE, "wb") as f:
            for i in range(total_segments_expected):
                if i in collected_segments:
                    f.write(collected_segments[i])

        destination_hash = compute_file_hash(DESTINATION_FILE)
        print(f"\nOriginal MD5: {source_hash}")
        print(f"Received MD5: {destination_hash}")
        
        if destination_hash == source_hash:
            total_time = time.time() - operation_start
            print(f"\nSUCCESS! File transfer complete in {total_time/60:.2f} minutes")
            print(f"Average speed: {(file_byte_size/1024/1024)/(total_time/60):.2f} MB/min")
        else:
            print("\nHash mismatch")

except KeyboardInterrupt:
    print("\n\nStopped by user")
finally:
    mqtt_client.loop_stop()
    mqtt_client.disconnect()
    sys.exit(0)