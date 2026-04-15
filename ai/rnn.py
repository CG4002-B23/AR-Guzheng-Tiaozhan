import torch
import torch.nn as nn
import pandas as pd
import numpy as np
import sys
from torch.utils.data import DataLoader, TensorDataset

# This python script contains pipeline for training model then saving model and weights

# --- 1. Configuration ---
PROCESSED_DATA_FILE = "./data/processed_dataset.csv"

CLASS_NAMES = [
    "null",
    "thumb",
    "index",
    "middle",
    "dragon_claw",
    "snake_strike",
    "one_inch_punch",
    "god_chop",
    "crane_wing",
]

T_STEPS = 20
INPUT_SIZE = 10
HIDDEN_SIZE = 32
NUM_CLASSES = 9
BATCH_SIZE = 16
LEARNING_RATE = 0.001
EPOCHS = 200
MAX_SEQUENCES_PER_CLASS = 1000

# --- 2. Model ---
class GestureRNN(nn.Module):
    def __init__(self):
        super().__init__()
        self.rnn = nn.RNN(INPUT_SIZE, HIDDEN_SIZE, num_layers=2,
                          nonlinearity='relu', batch_first=True)
        self.fc = nn.Linear(HIDDEN_SIZE, NUM_CLASSES)
    
    def forward(self, x):
        out, _ = self.rnn(x)
        return self.fc(out[:, -1, :])

# --- 3. Data Processing ---
def load_data(data_file=PROCESSED_DATA_FILE):
    from sklearn.model_selection import train_test_split
    from sklearn.utils import shuffle

    df = pd.read_csv(data_file)
    print(f"Loaded {len(df)} rows from '{data_file}'")

    feature_cols = ['ax', 'ay', 'az', 'gx', 'gy', 'gz', 'f1', 'f2', 'f3', 'mag']
    X_all, y_all = [], []

    for gesture_id, group in df.groupby('gesture_id'):
        group = group.sort_values('timestep')
        if len(group) != T_STEPS:
            continue  # skip incomplete windows

        label_id = int(group['label'].iloc[0])
        feats = group[feature_cols].values.astype(np.float32)  # (T_STEPS, INPUT_SIZE)
        X_all.append(feats)
        y_all.append(label_id)

    # Cap per-class sequences to MAX_SEQUENCES_PER_CLASS
    from collections import defaultdict
    class_indices = defaultdict(list)
    for idx, label_id in enumerate(y_all):
        class_indices[label_id].append(idx)

    kept_indices = []
    for label_id, idxs in sorted(class_indices.items()):
        chosen = idxs[:MAX_SEQUENCES_PER_CLASS]
        kept_indices.extend(chosen)
        print(f"  class {label_id} ({CLASS_NAMES[label_id]}): using {len(chosen)} sequences")

    X = np.array(X_all)[kept_indices]  # (N, T_STEPS, INPUT_SIZE)
    y = np.array(y_all)[kept_indices]  # (N,)

    X, y = shuffle(X, y, random_state=42)

    # split
    X_train, X_temp, y_train, y_temp = train_test_split(X, y, test_size=0.3, random_state=42)
    X_val, X_test, y_val, y_test = train_test_split(X_temp, y_temp, test_size=0.5, random_state=42)

    # normalize
    mean = X_train.reshape(-1, INPUT_SIZE).mean(axis=0)
    std = X_train.reshape(-1, INPUT_SIZE).std(axis=0) + 1e-6

    X_train = (X_train - mean) / std
    X_val = (X_val - mean) / std
    X_test = (X_test - mean) / std

    return (X_train, y_train), (X_val, y_val), (X_test, y_test), mean, std

# --- 4. Training ---
def main():
    from sklearn.model_selection import train_test_split
    from sklearn.metrics import confusion_matrix, classification_report
    from sklearn.utils import shuffle
    train_data, val_data, test_data, mean, std = load_data()

    def make_loader(data, shuffle_flag):
        X = torch.tensor(data[0])
        y = torch.tensor(data[1])
        return DataLoader(TensorDataset(X, y), batch_size=BATCH_SIZE, shuffle=shuffle_flag)

    train_loader = make_loader(train_data, True)
    val_loader = make_loader(val_data, False)
    test_loader = make_loader(test_data, False)

    model = GestureRNN()
    optimizer = torch.optim.Adam(model.parameters(), lr=LEARNING_RATE)

    # Class weights (fix imbalance)
    unique, counts = np.unique(train_data[1], return_counts=True)
    weights = 1.0 / counts
    weights = torch.tensor(weights, dtype=torch.float32)

    criterion = nn.CrossEntropyLoss(weight=weights)

    train_losses = []
    val_accuracies = []

    for epoch in range(EPOCHS):
        model.train()
        total_loss = 0

        for Xb, yb in train_loader:
            optimizer.zero_grad()
            loss = criterion(model(Xb), yb)
            loss.backward()
            optimizer.step()
            total_loss += loss.item()

        avg_loss = total_loss / len(train_loader)
        train_losses.append(avg_loss)

        model.eval()
        correct = 0
        with torch.no_grad():
            for Xb, yb in val_loader:
                preds = model(Xb).argmax(dim=1)
                correct += (preds == yb).sum().item()

        val_acc = correct / len(val_data[0])
        val_accuracies.append(val_acc)

        if (epoch+1) % 10 == 0:
            print(f"Epoch {epoch+1}: Loss={avg_loss:.4f}, ValAcc={val_acc*100:.2f}%")

    # --- 5. Evaluation ---
    model.eval()
    all_preds, all_labels = [], []

    with torch.no_grad():
        for Xb, yb in test_loader:
            preds = model(Xb).argmax(dim=1)
            all_preds.extend(preds.numpy())
            all_labels.extend(yb.numpy())

    all_preds = np.array(all_preds)
    all_labels = np.array(all_labels)

    print("\nFinal Test Accuracy:", (all_preds == all_labels).mean())

    print("\nClassification Report:")
    print(classification_report(all_labels, all_preds, target_names=CLASS_NAMES))

    # Confusion matrix
    cm = confusion_matrix(all_labels, all_preds)

    import matplotlib.pyplot as plt
    import seaborn as sns
    plt.figure(figsize=(8,6))
    sns.heatmap(cm, annot=True, fmt="d",
                xticklabels=CLASS_NAMES,
                yticklabels=CLASS_NAMES)
    plt.xlabel("Predicted")
    plt.ylabel("True")
    plt.title("Confusion Matrix")
    plt.show()

    # Curves
    plt.figure()
    plt.plot(train_losses)
    plt.title("Training Loss")
    plt.show()

    plt.figure()
    plt.plot(val_accuracies)
    plt.title("Validation Accuracy")
    plt.show()

    # Save model and generate weights
    print("Exporting HLS weights...")
    generate_hls_header(model, mean, std)
    print("Done.")

    print("Saving python model...")
    torch.save({
    "model_state": model.state_dict(),
    "mean": mean,
    "std": std
    }, "gesture_rnn_full.pth")
    print("Model saved.")

# HLS Header Generation
def generate_hls_header(model, mean, std, filename="gesture_weights_hls.h"):
    scale = 65536  # 2^16
    with open(filename, "w") as f:
        f.write("#ifndef GESTURE_WEIGHTS_HLS_H\n#define GESTURE_WEIGHTS_HLS_H\n\n")
        f.write("#include <stdint.h>\n\n")
        
        def write_array(name, data):
            q_data = (data.detach().cpu().numpy() * scale).astype(np.int32).flatten()
            f.write(f"const int32_t {name}[] = {{\n    ")
            f.write(", ".join(map(str, q_data)))
            f.write("\n};\n\n")

        # RNN Weights
        write_array("rnn_weight_ih_l0_q", model.rnn.weight_ih_l0)
        write_array("rnn_weight_hh_l0_q", model.rnn.weight_hh_l0)
        write_array("rnn_bias_ih_l0_q", model.rnn.bias_ih_l0)
        write_array("rnn_bias_hh_l0_q", model.rnn.bias_hh_l0)
        write_array("rnn_weight_ih_l1_q", model.rnn.weight_ih_l1)
        write_array("rnn_weight_hh_l1_q", model.rnn.weight_hh_l1)
        write_array("rnn_bias_ih_l1_q", model.rnn.bias_ih_l1)
        write_array("rnn_bias_hh_l1_q", model.rnn.bias_hh_l1)

        # FC Layer
        write_array("fc_weight_q", model.fc.weight)
        write_array("fc_bias_q", model.fc.bias)

        # Norm Params
        inv_std = 1.0 / std

        write_array("NORM_MEAN_Q", torch.tensor(mean))
        write_array("NORM_INV_STD_Q", torch.tensor(inv_std))
        
        f.write("#endif\n")

if __name__ == "__main__":
    main()
