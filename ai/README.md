# AI Module Overview

This folder contains the full AI pipeline for the capstone project, from dataset preparation and RNN training to hardware deployment on FPGA.

## Main Files

- `rnn.py`  
  Loads pre-processed data CSV in ./data folder, trains the gesture RNN model, evaluates performance, and exports:
  - `gesture_rnn_full.pth` (PyTorch checkpoint with model state + normalization stats)
  - `gesture_weights_hls.h` (quantized weights/header for hardware implementation)

- `generate_HLS_test.py`  
  Uses the trained checkpoint and processed dataset to generate a reduced, labeled test subset with expected logits for Vitis HLS testbench validation.

- `arduino_test.py`  
  Software-side real-time inference test script for direct glove ESP serial input. This is used to validate the model behavior before/alongside hardware acceleration.

- `gesture_rnn_full.pth`  
  Trained model artifact saved from `rnn.py`.

## Subfolders

### `data/`

Contains training data assets:

- Raw CSV recordings for each gesture class (for example `thumb_raw.csv`, `snake_strike_raw.csv`, etc.)
- Merged/processed dataset used by `rnn.py` for training

The data collection and preprocessing pipeline produces these files before model training.

### `vitis/`

Contains Vitis HLS hardware implementation and testbench assets:

- `rnn_hls.cpp`: HLS C++ implementation of the RNN inference core
- `gesture_weights_hls.h`: exported quantized weights/normalization constants for HLS
- `tb.cpp`: HLS testbench
- `data_with_logits.csv`: generated HLS testbench input + expected output reference data
- `vitis_workspace.zip`: Exported workspace file for Vitis HLS
  
### `vivado/`

Contains Vivado-packaged hardware project deliverables:

- `rnn_wrapper.zip`: packaged hardware implementation exported from Vivado
  
### `fpga/`

Contains FPGA runtime deployment files and inference wrapper:

- `rnn.bit` and `rnn.hwh`: hardware bitstream and hardware handoff metadata
- `inference.py`: overlay load/setup script and inference abstraction for communication with the hardware model via DMA


## Typical Workflow

1. Collect and preprocess gesture data
2. Train and export model artifacts using `rnn.py`
3. Generate HLS test vectors using `generate_HLS_test.py`
4. Create and validate hardware implementation in Vitis HLS using latest weights and test vectors
5. Import hardware IP into Vivado, create and validate block hardware design and package into bitstream
5. Deploy bitstream/handoff files in fpga folder and run hardware inference
