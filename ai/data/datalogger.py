import serial
import csv
import time
import os
import keyboard
import pandas as pd
import numpy as np
import glob

# --- Hardware Configuration ---
SERIAL_PORT = 'COM4'
BAUD_RATE = 115200
TRIGGER_KEY = 'space'
EXIT_KEY = 'esc'

# --- Extraction Configuration ---
WINDOW_SIZE = 20
JITTER_OFFSETS = [-4, -2, 0, 2, 4]
SENSOR_COLS = ['ax', 'ay', 'az', 'gx', 'gy', 'gz', 'f1', 'f2', 'f3', 'mag']
OUTPUT_CSV = 'processed_dataset.csv'

def record_data():
    print("\n--- Data Logger Mode ---")
    LABEL_MAP = {
        0: 'null',
        1: 'thumb',
        2: 'index',
        3: 'middle',
        4: 'dragon_claw',
        5: 'snake_strike',
        6: 'one_inch_punch',
        7: 'god_chop',
        8: 'crane_wing',
    }
    print("Labels: " + "  ".join(f"{k}-{v}" for k, v in LABEL_MAP.items()))
    try:
        label = int(input("Enter label index (0-8): ").strip())
        if label not in LABEL_MAP:
            raise ValueError
    except ValueError:
        print("Invalid label. Aborting.")
        return
    file_name = LABEL_MAP[label]

    output_file = f'{file_name}_raw.csv'
    write_header = not os.path.exists(output_file) or os.path.getsize(output_file) == 0

    try:
        ser = serial.Serial(SERIAL_PORT, BAUD_RATE, timeout=1)
        # Reset Arduino via DTR pulse (same as pressing the reset button)
        ser.setDTR(False)
        time.sleep(0.1)
        ser.setDTR(True)
        print("Arduino reset. Waiting for reboot...", end='', flush=True)
        time.sleep(2)           # Wait for board to boot and sketch to start
        ser.flushInput()        # Discard any startup noise/garbage
        print(" Ready.")
    except Exception as e:
        print(f"Serial connection failed: {e}")
        return

    with open(output_file, mode='a', newline='') as file:
        writer = csv.writer(file)
        if write_header:
            writer.writerow(['timestamp', 'ax', 'ay', 'az', 'gx', 'gy', 'gz', 'f1', 'f2', 'f3', 'mag', 'label', 'marker'])
            print(f"File initialized: {output_file}")
        
        try:
            print(f"\nRecording Label {label} to '{output_file}'...")
            print(f"Hold '{TRIGGER_KEY}' during kinetic apex. Press '{EXIT_KEY}' to exit.")
            
            while True:
                if keyboard.is_pressed(EXIT_KEY):
                    print("Exited.")
                    break
                
                marker = 1 if keyboard.is_pressed(TRIGGER_KEY) else 0
                
                if ser.in_waiting > 0:
                    line = ser.readline().decode('utf-8', errors='ignore').strip()
                    if line:
                        sensor_vector = [val for val in line.split(',') if val != '']
                        if len(sensor_vector) == 10:
                            timestamp = time.time()
                            row = [timestamp] + sensor_vector + [label, marker]
                            writer.writerow(row)
                            file.flush()
                            # Print confirmation without flooding terminal
                            print(f"\r[OK] Logged vector | Marker: {marker} | Queue: {ser.in_waiting}   ", end="")
                        else:
                            print(f"\n[SKIP] Expected 10 fields, got {len(sensor_vector)}")
                            
        except KeyboardInterrupt:
            print("Exited.")
        finally:
            ser.close()

def extract_data():
    print("\n--- Data Extractor Mode ---")
    data_dir = "."
    all_files = glob.glob(os.path.join(data_dir, "*_raw.csv"))
    if not all_files:
        print("Error: No raw CSV files found.")
        return

    extracted_frames = []
    gesture_id = 0
    half_window = WINDOW_SIZE // 2

    # 1. Process Active Files
    active_files = [f for f in all_files if "null" not in os.path.basename(f).lower()]
    for f in active_files:
        gesture_name = os.path.basename(f).replace('_raw.csv', '')
        df = pd.read_csv(f)
        numeric_cols = SENSOR_COLS + ['marker', 'label']
        df[numeric_cols] = df[numeric_cols].apply(pd.to_numeric, errors='coerce')
        df.dropna(subset=SENSOR_COLS, inplace=True)
        df['acc_mag'] = np.sqrt(df['ax']**2 + df['ay']**2 + df['az']**2)
        df['block'] = (df['marker'].diff() != 0).cumsum()
        
        active_blocks = df[df['marker'] == 1]
        
        for _, block_data in active_blocks.groupby('block'):
            if len(block_data) < 3:
                continue
                
            apex_idx = block_data['acc_mag'].idxmax()
            label = int(df.loc[apex_idx, 'label'])
            
            for offset in JITTER_OFFSETS:
                start_idx = apex_idx - half_window + offset
                end_idx = apex_idx + half_window + offset
                
                if start_idx >= 0 and end_idx <= len(df):
                    window_df = df.iloc[start_idx:end_idx][SENSOR_COLS].copy()
                    window_df['gesture_id'] = gesture_id
                    window_df['timestep'] = np.arange(WINDOW_SIZE)
                    window_df['label'] = label
                    window_df['name'] = gesture_name
                    
                    extracted_frames.append(window_df)
                    gesture_id += 1

    active_count = gesture_id

    # 2. Process Null Files
    null_files = [f for f in all_files if "null" in os.path.basename(f).lower()]
    null_frames = []
    
    for f in null_files:
        df = pd.read_csv(f)
        df[SENSOR_COLS] = df[SENSOR_COLS].apply(pd.to_numeric, errors='coerce')
        df.dropna(subset=SENSOR_COLS, inplace=True)
        for i in range(0, len(df) - WINDOW_SIZE, WINDOW_SIZE):
            window_df = df.iloc[i : i + WINDOW_SIZE][SENSOR_COLS].copy()
            if len(window_df) == WINDOW_SIZE:
                window_df['timestep'] = np.arange(WINDOW_SIZE)
                window_df['label'] = 0
                window_df['name'] = 'null'
                null_frames.append(window_df)

    if not extracted_frames and not null_frames:
        print("Extraction failed. Zero valid windows generated.")
        return

    # 3. Class Balancing (50/50 Split)
    np.random.shuffle(null_frames)
    balanced_nulls = null_frames[:active_count] if active_count > 0 else null_frames
    
    for window_df in balanced_nulls:
        window_df['gesture_id'] = gesture_id
        extracted_frames.append(window_df)
        gesture_id += 1

    # 4. Construct Final CSV
    final_df = pd.concat(extracted_frames, ignore_index=True)
    cols = ['gesture_id', 'timestep', 'label', 'name'] + SENSOR_COLS
    final_df = final_df[cols]
    
    try:
        final_df.to_csv(OUTPUT_CSV, index=False)
        print(f"Extraction Complete. Total Gestures: {gesture_id} (Active: {active_count}, Null: {len(balanced_nulls)})")
        print(f"Dataset compiled to '{OUTPUT_CSV}'.")
    except PermissionError:
        print(f"\n[ERROR] Cannot write '{OUTPUT_CSV}' — the file is open in another program.")
        print("Please close it (e.g. Excel) and run extraction again.")

if __name__ == "__main__":
    while True:
        print("\n=== Main Menu ===")
        print("1. Record Serial Data")
        print("2. Extract & Build Dataset CSV")
        print("3. Exit")
        choice = input("Select operation (1/2/3): ").strip()
        
        if choice == '1':
            record_data()
        elif choice == '2':
            extract_data()
        elif choice == '3':
            break
        else:
            print("Invalid selection.")