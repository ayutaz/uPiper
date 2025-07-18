#ifndef OPENJTALK_CORE_H
#define OPENJTALK_CORE_H

#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

// Forward declarations
typedef struct _Mecab Mecab;
typedef struct _NJD NJD;
typedef struct _JPCommon JPCommon;
typedef struct _HTS_Engine HTS_Engine;

// OpenJTalk core structure
typedef struct {
    Mecab* mecab;
    NJD* njd;
    JPCommon* jpcommon;
    HTS_Engine* engine;
    
    char* dic_dir;
    char* voice_dir;
    
    // Processing options
    double speed;
    double pitch;
    double volume;
    
    // Internal buffers
    char* text_buffer;
    size_t text_buffer_size;
    
    // Error state
    int last_error;
    char error_message[256];
    
} OpenJTalkCore;

// Core functions
OpenJTalkCore* openjtalk_core_create(void);
void openjtalk_core_destroy(OpenJTalkCore* core);

bool openjtalk_core_initialize(OpenJTalkCore* core, const char* dic_dir);
bool openjtalk_core_load_voice(OpenJTalkCore* core, const char* voice_file);

// Text to phoneme conversion
bool openjtalk_core_text_to_phonemes(OpenJTalkCore* core, 
                                      const char* text,
                                      char* phonemes,
                                      size_t phonemes_size,
                                      int* phoneme_ids,
                                      size_t* phoneme_count);

// Text to speech synthesis
bool openjtalk_core_synthesize(OpenJTalkCore* core,
                               const char* text,
                               float** audio_buffer,
                               size_t* sample_count,
                               int* sample_rate);

// Parameter setters
void openjtalk_core_set_speed(OpenJTalkCore* core, double speed);
void openjtalk_core_set_pitch(OpenJTalkCore* core, double pitch);
void openjtalk_core_set_volume(OpenJTalkCore* core, double volume);

// Error handling
const char* openjtalk_core_get_error(OpenJTalkCore* core);
void openjtalk_core_clear_error(OpenJTalkCore* core);

#ifdef __cplusplus
}
#endif

#endif // OPENJTALK_CORE_H