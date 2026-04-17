#include <hls_stream.h>
#include "gesture_weights_hls.h"
#include <ap_int.h>
#include <ap_axi_sdata.h>

#define SEQ_LENGTH 20
#define TARGET_CLASSES 9
 
#define FEATURE_DIM 10
#define LATENT_DIM 32

#define QUANT_SHIFT 16
#define QUANT_SCALE 65536

static int32_t quantized_feature_cache[SEQ_LENGTH][FEATURE_DIM];  
static int32_t latent_state_0[LATENT_DIM]; 
static int32_t latent_state_1[LATENT_DIM];
static int32_t latent_next_0[LATENT_DIM]; 
static int32_t latent_next_1[LATENT_DIM];
static int32_t raw_predictions[TARGET_CLASSES];

typedef ap_axis<32,2,5,6> AXIS_wLAST;

void compute_rnn() {
    #pragma HLS INLINE OFF
    
    for (int i = 0; i < LATENT_DIM; i++) {
        #pragma HLS UNROLL
        latent_state_0[i] = 0;
        latent_state_1[i] = 0;
    }
    
    for (int t = 0; t < SEQ_LENGTH; t++) {
        
        for (int i = 0; i < LATENT_DIM; i++) {
            #pragma HLS PIPELINE II=1
            
            int64_t v_accum = (int64_t)rnn_bias_ih_l0_q[i] + (int64_t)rnn_bias_hh_l0_q[i]; 
            
            for (int j = 0; j < FEATURE_DIM; j++) {
                int idx = i * FEATURE_DIM + j;
                v_accum += ((int64_t)rnn_weight_ih_l0_q[idx] * (int64_t)quantized_feature_cache[t][j]) >> QUANT_SHIFT; 
            }
            
            for (int j = 0; j < LATENT_DIM; j++) {
                int idx = i * LATENT_DIM + j;
                v_accum += ((int64_t)rnn_weight_hh_l0_q[idx] * (int64_t)latent_state_0[j]) >> QUANT_SHIFT; 
            }
            
            if (v_accum < 0) v_accum = 0;
            latent_next_0[i] = (int32_t)v_accum; 
        }
        
        for (int i = 0; i < LATENT_DIM; i++) {
            #pragma HLS PIPELINE II=1
            
            int64_t v_accum = (int64_t)rnn_bias_ih_l1_q[i] + (int64_t)rnn_bias_hh_l1_q[i]; 
            
            for (int j = 0; j < LATENT_DIM; j++) {
                int idx = i * LATENT_DIM + j;
                v_accum += ((int64_t)rnn_weight_ih_l1_q[idx] * (int64_t)latent_next_0[j]) >> QUANT_SHIFT; 
            }
            
            for (int j = 0; j < LATENT_DIM; j++) {
                int idx = i * LATENT_DIM + j;
                v_accum += ((int64_t)rnn_weight_hh_l1_q[idx] * (int64_t)latent_state_1[j]) >> QUANT_SHIFT; 
            }
            
            if (v_accum < 0) v_accum = 0;
            latent_next_1[i] = (int32_t)v_accum; 
        }
        
        for (int i = 0; i < LATENT_DIM; i++) {
            #pragma HLS UNROLL
            latent_state_0[i] = latent_next_0[i];
            latent_state_1[i] = latent_next_1[i];
        }
    }
    
    for (int c = 0; c < TARGET_CLASSES; c++) {
        #pragma HLS PIPELINE II=1
        
        int64_t v_accum = (int64_t)fc_bias_q[c];
        
        for (int j = 0; j < LATENT_DIM; j++) {
            int idx = c * LATENT_DIM + j;
            v_accum += ((int64_t)fc_weight_q[idx] * (int64_t)latent_state_1[j]) >> QUANT_SHIFT;
        }
        
        raw_predictions[c] = (int32_t)v_accum;
    }
}

void execute_stream_inference(
    hls::stream<AXIS_wLAST>& in_data_stream,
    hls::stream<AXIS_wLAST>& out_data_stream
) {

    #pragma HLS INTERFACE ap_ctrl_none port=return
    #pragma HLS INTERFACE axis port=in_data_stream
    #pragma HLS INTERFACE axis port=out_data_stream
    
    #pragma HLS BIND_STORAGE variable=quantized_feature_cache type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=latent_state_0 type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=latent_state_1 type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=latent_next_0 type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=latent_next_1 type=ram_2p impl=bram
    #pragma HLS BIND_STORAGE variable=raw_predictions type=ram_2p impl=bram
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

    AXIS_wLAST stream_payload;
    
    for (int t = 0; t < SEQ_LENGTH; t++) {
        for (int f = 0; f < FEATURE_DIM; f++) {
            #pragma HLS PIPELINE II=1
    
            in_data_stream.read(stream_payload);
    
            union { int32_t i; float f; } converter;
            converter.i = stream_payload.data;
    
            int32_t x_q = (int32_t)(converter.f * QUANT_SCALE);
    
            int32_t centered = x_q - NORM_MEAN_Q[f];
            int32_t norm = (int32_t)(((int64_t)centered * NORM_INV_STD_Q[f]) >> QUANT_SHIFT);
    
            quantized_feature_cache[t][f] = norm;
        }
    }

    compute_rnn();
    
    for (int i = 0; i < TARGET_CLASSES; i++) {
        #pragma HLS PIPELINE II=1
        stream_payload.data = raw_predictions[i];
        
        if (i == TARGET_CLASSES - 1) {
            stream_payload.last = 1;
        } else {
            stream_payload.last = 0;
        }
        
        out_data_stream.write(stream_payload);
    }
}