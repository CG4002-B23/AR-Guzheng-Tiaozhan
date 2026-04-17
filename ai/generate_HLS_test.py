import torch
import pandas as pd
import numpy as np
from rnn import GestureRNN, NUM_CLASSES, T_STEPS, CLASS_NAMES, PROCESSED_DATA_FILE

# This python script is for generate tb vectors for HLS

def generate_hls_testdata(
    pth_file = "gesture_rnn_full.pth",
    csv_in   = PROCESSED_DATA_FILE,          # ./data/processed_dataset.csv
    csv_out  = "hls/data_with_logits.csv",
):
    """
    Loads the processed dataset (grouped by gesture_id), runs inference on
    selected windows, and writes hls/data_with_logits.csv.

    Sampling:
      - null class (label 0): up to 50 windows
      - every other class   : up to 5 windows each
    """

    # ── 1. Load model ────────────────────────────────────────────────────────
    checkpoint = torch.load(pth_file, map_location="cpu", weights_only=False)
    model = GestureRNN()
    model.load_state_dict(checkpoint["model_state"])
    model.eval()
    mean = checkpoint["mean"]   # (INPUT_SIZE,)
    std  = checkpoint["std"]    # (INPUT_SIZE,)

    print(f"Loaded model from '{pth_file}'")
    print(f"  norm mean : {np.round(mean, 4)}")
    print(f"  norm std  : {np.round(std,  4)}")

    # ── 2. Load processed dataset ────────────────────────────────────────────
    df = pd.read_csv(csv_in)
    print(f"Loaded {len(df)} rows from '{csv_in}'")

    feature_cols = ['ax', 'ay', 'az', 'gx', 'gy', 'gz', 'f1', 'f2', 'f3', 'mag']

    # ── 3. Sampling quota ────────────────────────────────────────────────────
    quota = {0: 50}                                    # null class
    for c in range(1, NUM_CLASSES):
        quota[c] = 5                                   # normal classes

    counts = {c: 0 for c in range(NUM_CLASSES)}
    output_rows = []   # list of dicts, one per kept window row

    # ── 4. Iterate over gesture_id groups ────────────────────────────────────
    for gesture_id, group in df.groupby("gesture_id"):
        group = group.sort_values("timestep")

        if len(group) != T_STEPS:
            continue  # skip incomplete windows

        label = int(group["label"].iloc[0])

        if label not in quota:
            continue
        if counts[label] >= quota[label]:
            continue

        # Normalise & run inference
        feats      = group[feature_cols].values.astype(np.float32)
        feats_norm = (feats - mean) / std
        x          = torch.tensor(feats_norm).unsqueeze(0)   # (1, T, F)

        with torch.no_grad():
            logits = model(x).squeeze(0).numpy()             # (NUM_CLASSES,)

        pred = int(np.argmax(logits))
        ok   = "OK" if pred == label else "WRONG"
        print(f"  gesture_id={gesture_id:6}  label={label} ({CLASS_NAMES[label]:15s})"
              f"  pred={pred}  {ok}"
              f"  logits=[{', '.join(f'{v:.3f}' for v in logits)}]")

        # Build output rows for this window
        logit_dict = {f"exp_logit_{c}": round(float(logits[c]), 6)
                      for c in range(NUM_CLASSES)}
        for _, row in group.iterrows():
            output_rows.append({**row.to_dict(), **logit_dict})

        counts[label] += 1

    # ── 5. Save ──────────────────────────────────────────────────────────────
    output_df = pd.DataFrame(output_rows)
    output_df.to_csv(csv_out, index=False)

    print(f"\nGeneration Complete.")
    print(f"Saved -> '{csv_out}'  ({len(output_df)} rows)")
    for label in range(NUM_CLASSES):
        print(f"  class {label:2d} ({CLASS_NAMES[label]:15s}): {counts[label]} windows")


if __name__ == "__main__":
    generate_hls_testdata()