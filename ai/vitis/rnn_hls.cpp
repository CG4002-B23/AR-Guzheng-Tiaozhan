#include <hls_stream.h>
#include <ap_int.h>
#include <ap_axi_sdata.h>
#include "gesture_weights_hls.h"

#define T_STEPS 20
#define INPUT_SIZE 10
#define HIDDEN_SIZE 32
#define NUM_CLASSES 9
#define WEIGHT_SCALE_BITS 16
#define WEIGHT_SCALE 65536

// AXI stream packet type (32-bit data for float)
typedef ap_axis<32,2,5,6> AXIS_wLAST;

// static buffers for internal computation (int32_t for fixed-point arithmetic)
static int32_t input_buffer_q[T_STEPS][INPUT_SIZE];  // quantised inputs
static int32_t h0[HIDDEN_SIZE]; // hidden state for layer 0
static int32_t h1[HIDDEN_SIZE];
static int32_t h0_new[HIDDEN_SIZE]; // temporary buffer for new hidden state before update
static int32_t h1_new[HIDDEN_SIZE];
static int32_t output_logits[NUM_CLASSES];

// RNN computation core
// h0(t) = ReLU( W_ih^(0) x(t) + W_hh^(0) h0(t-1) + b^(0) )
// h1(t) = ReLU( W_ih^(1) h0(t) + W_hh^(1) h1(t-1) + b^(1) )
// output = W_out h1(T) + b_out
void compute_rnn() {
    #pragma HLS INLINE OFF
    
    // initialise hidden states to zero
    for (int i = 0; i < HIDDEN_SIZE; i++) {
        #pragma HLS UNROLL
        h0[i] = 0;
        h1[i] = 0;
    }
    
    for (int t = 0; t < T_STEPS; t++) {
        
        // layer 0: input to hidden
        for (int i = 0; i < HIDDEN_SIZE; i++) {
            #pragma HLS PIPELINE II=1
            
            // start with bias
            int64_t acc = (int64_t)rnn_bias_ih_l0_q[i] + (int64_t)rnn_bias_hh_l0_q[i]; // bih, bhh
            
            // input contribution: weight_q (scaled 2^16) * input_q (scaled 2^16) = scaled 2^32
            for (int j = 0; j < INPUT_SIZE; j++) {
                int idx = i * INPUT_SIZE + j;
                acc += ((int64_t)rnn_weight_ih_l0_q[idx] * (int64_t)input_buffer_q[t][j]) >> WEIGHT_SCALE_BITS; // wih * x
            }
            
            // hidden state contribution
            for (int j = 0; j < HIDDEN_SIZE; j++) {
                int idx = i * HIDDEN_SIZE + j;
                acc += ((int64_t)rnn_weight_hh_l0_q[idx] * (int64_t)h0[j]) >> WEIGHT_SCALE_BITS; // whh * h
            }
            
            // ReLU activation
            if (acc < 0) acc = 0;
            h0_new[i] = (int32_t)acc; // h0_new
        }
        
        // layer 1: Hidden to hidden
        for (int i = 0; i < HIDDEN_SIZE; i++) {
            #pragma HLS PIPELINE II=1
            
            // start with bias
            int64_t acc = (int64_t)rnn_bias_ih_l1_q[i] + (int64_t)rnn_bias_hh_l1_q[i]; // bih, bhh
            
            // input from layer 0
            for (int j = 0; j < HIDDEN_SIZE; j++) {
                int idx = i * HIDDEN_SIZE + j;
                acc += ((int64_t)rnn_weight_ih_l1_q[idx] * (int64_t)h0_new[j]) >> WEIGHT_SCALE_BITS; // wih * h0_new
            }
            
            // hidden state contribution
            for (int j = 0; j < HIDDEN_SIZE; j++) {
                int idx = i * HIDDEN_SIZE + j;
                acc += ((int64_t)rnn_weight_hh_l1_q[idx] * (int64_t)h1[j]) >> WEIGHT_SCALE_BITS; // whh * h
            }
            
            // ReLU activation
            if (acc < 0) acc = 0;
            h1_new[i] = (int32_t)acc; // h1_new
        }
        
        // update hidden states
        for (int i = 0; i < HIDDEN_SIZE; i++) {
            #pragma HLS UNROLL
            h0[i] = h0_new[i];
            h1[i] = h1_new[i];
        }
    }
    
    // fully connected output layer
    for (int c = 0; c < NUM_CLASSES; c++) {
        #pragma HLS PIPELINE II=1
        
        // start with bias
        int64_t acc = (int64_t)fc_bias_q[c];
        
        for (int j = 0; j < HIDDEN_SIZE; j++) {
            int idx = c * HIDDEN_SIZE + j;
            acc += ((int64_t)fc_weight_q[idx] * (int64_t)h1[j]) >> WEIGHT_SCALE_BITS;
        }
        
        // final output remains in scaled format for consistency
        output_logits[c] = (int32_t)acc;
    }
}

void gesture_stream(
    hls::stream<AXIS_wLAST>& input_stream,
    hls::stream<AXIS_wLAST>& output_stream
) {

    #pragma HLS INTERFACE ap_ctrl_none port=return
    #pragma HLS INTERFACE axis port=input_stream
    #pragma HLS INTERFACE axis port=output_stream
    
    // bind static arrays to BRAM for efficient storage
    // DMA -> float buffer → float normalisation -> fixed quantisation -> fixed RNN -> stream logits -> DMA
    #pragma HLS BIND_STORAGE variable=input_buffer_q type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=h0 type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=h1 type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=h0_new type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=h1_new type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=output_logits type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=rnn_weight_ih_l0_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=rnn_weight_hh_l0_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=rnn_bias_ih_l0_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=rnn_bias_hh_l0_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=rnn_weight_ih_l1_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=rnn_weight_hh_l1_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=rnn_bias_ih_l1_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=rnn_bias_hh_l1_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=fc_weight_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=fc_bias_q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=NORM_MEAN_Q type=rom_2p impl=bram
    #pragma HLS BIND_STORAGE variable=NORM_INV_STD_Q type=rom_2p impl=bram

    AXIS_wLAST axis_packet;
    
    // 30 timesteps × 10 features = 300 float values
    // convert raw float input to fixed-point quantisation
    // Optimisation: Removed use of FLOAT and DIV, used std instead of 1/std
    for (int t = 0; t < T_STEPS; t++) {
        for (int f = 0; f < INPUT_SIZE; f++) {
            #pragma HLS PIPELINE II=1
    
            input_stream.read(axis_packet);
    
            union { int32_t i; float f; } converter;
            converter.i = axis_packet.data;
    
            int32_t x_q = (int32_t)(converter.f * WEIGHT_SCALE);
    
            int32_t centered = x_q - NORM_MEAN_Q[f];
            int32_t norm = (int32_t)(((int64_t)centered * NORM_INV_STD_Q[f]) >> WEIGHT_SCALE_BITS);
    
            input_buffer_q[t][f] = norm;
        }
    }

    compute_rnn();
    
    // write output logits to AXI Stream
    for (int i = 0; i < NUM_CLASSES; i++) {
        #pragma HLS PIPELINE II=1
        axis_packet.data = output_logits[i];
        
        // set TLAST on final output
        if (i == NUM_CLASSES - 1) {
            axis_packet.last = 1;
        } else {
            axis_packet.last = 0;
        }
        
        output_stream.write(axis_packet);
    }
}
