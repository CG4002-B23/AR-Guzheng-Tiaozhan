from pynq import Overlay, allocate
from pynq.pl_server.global_state import clear_global_state
import numpy as np
import time
import pandas as pd

# --- 1. Hardware Initialization ---
try:
    clear_global_state() 
    ol = Overlay("rnn.bit")
    ol.download()
    dma = ol.axi_dma_0 
    print("Overlay loaded successfully.")
except Exception as e:
    print(f"Error loading overlay: {e}")

# --- 2. Memory Allocation ---
in_buf = allocate(shape=(20, 10), dtype=np.float32)
out_buf = allocate(shape=(9,), dtype=np.int32)

# --- 3. Configuration ---
T_STEPS = 20
INPUT_SIZE = 10
QUANTIZATION_SCALE = 65536.0 

# --- 4. File Data Reader ---
# Load the CSV once at the start
df = pd.read_csv('fpga_test_data.csv')
# Select only the 8 feature columns used by the model
feature_cols = ['ax', 'ay', 'az', 'gx', 'gy', 'gz', 'f1', 'f2', 'f3', 'mag', 'label']
csv_data = df[feature_cols].values.astype(np.float32)

# --- 5. Inference Logic (Unchanged) ---
def run_fpga_inference(window_data):
    np.copyto(in_buf, window_data)
    dma.sendchannel.transfer(in_buf)
    dma.recvchannel.transfer(out_buf)
    dma.sendchannel.wait()
    dma.recvchannel.wait()
    logits = out_buf.astype(np.float32) / QUANTIZATION_SCALE
    prediction = np.argmax(logits)
    return prediction, logits

# --- 6. Main Tumbling Window Loop with File Data ---
def main():
    print(f"Starting loop using fpga_test_data.csv ({len(csv_data)} samples)...")
    
    local_window = []
    row_idx = 0
    
    try:
        while row_idx < len(csv_data):
            # A. Read one "sample" from the CSV instead of dummy generator
            sample = csv_data[row_idx][:10]
            local_window.append(sample)
            row_idx = (row_idx + 1) % len(csv_data)
            
            dots = "." * ((row_idx % 20) + 1)
            print(f"\rReceiving{dots}", end="")
            
            # B. Check if Tumbling Window is full
            if len(local_window) == T_STEPS:
                print(f"\n--- [Processing Row {row_idx-20} to {row_idx}: Triggering Inference] ---")
                
                start_t = time.time()
                gest_id, scores = run_fpga_inference(np.array(local_window))
                end_t = time.time()
                
                print(f"Detected Gesture ID: {gest_id}")
                print(f"Expected Gesture ID: {label}")
                print(f"Confidence Scores: {np.round(scores, 2)}")
                print(f"Hardware Latency: {(end_t - start_t)*1000:.2f}ms")
                
                # C. Clear window
                local_window = []
            
            else:
                # Simulation delay (30Hz)
                time.sleep(0.033)

            label = csv_data[row_idx][10]

        print("\nReached end of data file.")

    except KeyboardInterrupt:
        print("\nStopping application...")
    finally:
        in_buf.freebuffer()
        out_buf.freebuffer()

if __name__ == "__main__":
    main()