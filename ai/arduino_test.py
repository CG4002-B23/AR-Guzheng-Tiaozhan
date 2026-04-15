import serial
import torch
import torch.nn as nn
from collections import deque

# --- Configuration ---
SERIAL_PORT = 'COM4'
BAUD_RATE = 115200
INPUT_SIZE = 10
HIDDEN_SIZE = 32
NUM_CLASSES = 9
SEQUENCE_LENGTH = 20

# --- Model Definition ---
class GestureRNN(nn.Module):
    def __init__(self):
        super(GestureRNN, self).__init__()
        self.rnn = nn.RNN(INPUT_SIZE, HIDDEN_SIZE, num_layers=2,
                          nonlinearity='relu', batch_first=True)
        self.fc = nn.Linear(HIDDEN_SIZE, NUM_CLASSES)

    def forward(self, x):
        out, _ = self.rnn(x)
        return self.fc(out[:, -1, :])

# --- Initialization ---
checkpoint = torch.load("gesture_rnn_full.pth", map_location=torch.device('cpu'), weights_only=False)
model = GestureRNN()
model.load_state_dict(checkpoint["model_state"])
model.eval()

mean = torch.tensor(checkpoint["mean"], dtype=torch.float32)
std = torch.tensor(checkpoint["std"], dtype=torch.float32)

LABELS = [
    "null", "thumb", "index", "middle", 
    "dragon_claw", "snake_strike", "one_inch_punch", 
    "god_chop", "crane_wing"
]

buffer = deque(maxlen=SEQUENCE_LENGTH)
frame_count = 0

# --- Execution ---
try:
    ser = serial.Serial(SERIAL_PORT, BAUD_RATE, timeout=1.0)
    buffer.clear()
    print("Ready to predict!")
    
    while True:
        line = ser.readline().decode('utf-8', errors='ignore').strip()
        if not line:
            continue
            
        raw_values = line.split(',')
        
        # Expecting at least 10 features (3 Accel, 3 Gyro, 3 Flex, 1 Mag)
        if len(raw_values) >= 10:
            try:
                current_features = [float(x) for x in raw_values[:10]]
                buffer.append(current_features)
                
                if len(buffer) == SEQUENCE_LENGTH:
                    if frame_count % 8 == 0:
                        # Convert buffer to tensor (Batch, Seq, Features)
                        input_tensor = torch.tensor(list(buffer), dtype=torch.float32).unsqueeze(0)
                        
                        # Apply Normalization
                        input_tensor = (input_tensor - mean) / (std + 1e-8)
                        
                        with torch.no_grad():
                            output = model(input_tensor)
                            probs = torch.softmax(output, dim=1)
                            confidence = torch.max(probs).item()
                            prediction = torch.argmax(output, dim=1).item()
                            
                        print(f"Prediction: {LABELS[prediction]} (Confidence: {confidence:.2%})")
                    
                    frame_count += 1
                    
            except ValueError:
                continue

except KeyboardInterrupt:
    pass
finally:
    if 'ser' in locals():
        ser.close()