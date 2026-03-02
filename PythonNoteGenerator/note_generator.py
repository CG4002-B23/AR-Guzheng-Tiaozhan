import librosa
import numpy as np
import json
import argparse
from scipy.signal import butter, sosfiltfilt

def create_filter(lowcut, highcut, fs, order=4):
    """Creates an audio filter based on frequency boundaries."""
    nyq = 0.5 * fs
    if lowcut is None:
        return butter(order, highcut / nyq, btype='lowpass', output='sos')
    elif highcut is None:
        return butter(order, lowcut / nyq, btype='highpass', output='sos')
    else:
        return butter(order, [lowcut / nyq, highcut / nyq], btype='bandpass', output='sos')

def apply_filter(data, sos):
    """Applies the filter to the audio data using zero-phase filtering."""
    return sosfiltfilt(sos, data)

def analyze_gesture(y_band, onset_sample, sr):
    """Analyzes the audio after a note hits to determine the Guzheng gesture."""
    analysis_window = int(0.3 * sr)
    segment = y_band[onset_sample : onset_sample + analysis_window]
    
    if len(segment) == 0:
        return "muo" # Fallback
        
    peak_amplitude = np.max(np.abs(segment))
    rms_energy = np.sqrt(np.mean(segment**2))
    
    # HEURISTICS
    if rms_energy > (peak_amplitude * 0.4): 
        return "tremolo"
    elif peak_amplitude > 0.5: 
        return "tuo"
    else:
        return "muo"

def generate_guzheng_beatmap(input_mp3, output_json):
    """Processes the audio and exports the JSON beatmap."""
    print(f"Loading '{input_mp3}' for beatmap generation...")
    try:
        y, sr = librosa.load(input_mp3, sr=None)
    except Exception as e:
        print(f"Error loading audio file: {e}")
        return
    
    bands = [
        (None, 250),        # String 1
        (250, 1000),        # String 2
        (1000, 3000),       # String 3
        (3000, 8000),       # String 4
        (8000, None)        # String 5
    ]
    
    beatmap = {"notes": []}
    
    for i, (low, high) in enumerate(bands):
        string_num = i + 1
        print(f"Detecting notes on String {string_num}...")
        
        sos = create_filter(low, high, sr)
        y_band = apply_filter(y, sos)
        
        onset_frames = librosa.onset.onset_detect(y=y_band, sr=sr, backtrack=True)
        onset_times = librosa.frames_to_time(onset_frames, sr=sr)
        onset_samples = librosa.frames_to_samples(onset_frames)
        
        for time, sample in zip(onset_times, onset_samples):
            gesture = analyze_gesture(y_band, sample, sr)
            beatmap["notes"].append({
                "time": round(float(time), 3),
                "string": string_num,
                "gesture": gesture
            })

    beatmap["notes"] = sorted(beatmap["notes"], key=lambda k: k['time'])
    
    with open(output_json, 'w') as f:
        json.dump(beatmap, f, indent=4)
        
    print(f"Success! Beatmap saved to '{output_json}' with {len(beatmap['notes'])} notes!")

# --- Command Line Interface Setup ---
if __name__ == "__main__":
    # Initialize the argument parser
    parser = argparse.ArgumentParser(description="Generate a JSON beatmap from an MP3 for an AR Guzheng game.")
    
    # Add the required arguments
    parser.add_argument("-i", "--input", required=True, help="Path to the input MP3 file (e.g., song.mp3)")
    parser.add_argument("-o", "--output", required=True, help="Path to the output JSON file (e.g., level1.json)")
    
    # Parse the arguments from the terminal
    args = parser.parse_args()
    
    # Run the main function using the provided arguments
    generate_guzheng_beatmap(args.input, args.output)
