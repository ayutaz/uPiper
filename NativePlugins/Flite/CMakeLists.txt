cmake_minimum_required(VERSION 3.10)
project(flite_unity)

# C standard
set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)

# Options
option(BUILD_SHARED_LIBS "Build shared library" ON)
option(FLITE_LEAN_AND_MEAN "Build minimal Flite for Unity" ON)

# Platform-specific settings
if(WIN32)
    add_definitions(-D_CRT_SECURE_NO_WARNINGS)
    add_definitions(-DWIN32)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} /MT")
elseif(APPLE)
    set(CMAKE_OSX_ARCHITECTURES "x86_64;arm64")
    set(CMAKE_MACOSX_RPATH ON)
elseif(UNIX)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -fPIC")
endif()

# Flite source directory
set(FLITE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/external/flite)

# Include directories
include_directories(
    ${FLITE_DIR}/include
    ${FLITE_DIR}/src/audio
    ${FLITE_DIR}/src/cg
    ${FLITE_DIR}/src/hrg
    ${FLITE_DIR}/src/lexicon
    ${FLITE_DIR}/src/regex
    ${FLITE_DIR}/src/speech
    ${FLITE_DIR}/src/stats
    ${FLITE_DIR}/src/synth
    ${FLITE_DIR}/src/utils
    ${FLITE_DIR}/src/wavesynth
)

# Define minimal Flite
add_definitions(
    -DFLITE_LEAN_AND_MEAN
    -DCST_NO_SOCKETS
    -DCST_AUDIO_NONE  # Unity handles audio
)

# Flite core source files (minimal set for LTS)
set(FLITE_CORE_SRCS
    # Core utilities
    ${FLITE_DIR}/src/utils/cst_alloc.c
    ${FLITE_DIR}/src/utils/cst_error.c
    ${FLITE_DIR}/src/utils/cst_features.c
    ${FLITE_DIR}/src/utils/cst_file_stdio.c
    ${FLITE_DIR}/src/utils/cst_string.c
    ${FLITE_DIR}/src/utils/cst_tokenstream.c
    ${FLITE_DIR}/src/utils/cst_val.c
    ${FLITE_DIR}/src/utils/cst_val_const.c
    ${FLITE_DIR}/src/utils/cst_val_user.c
    
    # Regex
    ${FLITE_DIR}/src/regex/cst_regex.c
    ${FLITE_DIR}/src/regex/regexp.c
    
    # HRG (Heterogeneous Relation Graphs)
    ${FLITE_DIR}/src/hrg/cst_utterance.c
    ${FLITE_DIR}/src/hrg/cst_item.c
    ${FLITE_DIR}/src/hrg/cst_relation.c
    ${FLITE_DIR}/src/hrg/cst_ffeature.c
    
    # Lexicon and LTS
    ${FLITE_DIR}/src/lexicon/cst_lexicon.c
    ${FLITE_DIR}/src/lexicon/cst_lts.c
    ${FLITE_DIR}/src/lexicon/cst_lts_rewrites.c
    
    # Synth
    ${FLITE_DIR}/src/synth/cst_synth.c
    ${FLITE_DIR}/src/synth/cst_utt_utils.c
    ${FLITE_DIR}/src/synth/cst_voice.c
    ${FLITE_DIR}/src/synth/cst_phoneset.c
    ${FLITE_DIR}/src/synth/flite.c
    
    # US English voice (for LTS rules)
    ${FLITE_DIR}/lang/usenglish/us_text.c
    ${FLITE_DIR}/lang/usenglish/us_expand.c
    ${FLITE_DIR}/lang/usenglish/us_nums_cart.c
    ${FLITE_DIR}/lang/usenglish/us_phoneset.c
    ${FLITE_DIR}/lang/usenglish/us_ffeatures.c
    ${FLITE_DIR}/lang/usenglish/us_phrasing_cart.c
    ${FLITE_DIR}/lang/usenglish/us_pos_cart.c
    ${FLITE_DIR}/lang/usenglish/us_text.c
    ${FLITE_DIR}/lang/usenglish/usenglish.c
    
    # CMU lexicon and LTS
    ${FLITE_DIR}/lang/cmulex/cmu_lex.c
    ${FLITE_DIR}/lang/cmulex/cmu_lex_data.c
    ${FLITE_DIR}/lang/cmulex/cmu_lex_entries.c
    ${FLITE_DIR}/lang/cmulex/cmu_lts_model.c
    ${FLITE_DIR}/lang/cmulex/cmu_lts_rules.c
    ${FLITE_DIR}/lang/cmulex/cmu_postlex.c
)

# Unity wrapper
set(UNITY_WRAPPER_SRCS
    src/flite_unity.c
)

# Create library
add_library(flite_unity ${FLITE_CORE_SRCS} ${UNITY_WRAPPER_SRCS})

# Export settings
if(WIN32)
    target_compile_definitions(flite_unity PRIVATE FLITE_UNITY_EXPORTS)
endif()

# Install
install(TARGETS flite_unity
    RUNTIME DESTINATION ${CMAKE_CURRENT_SOURCE_DIR}/../../Assets/uPiper/Plugins/Windows/x86_64
    LIBRARY DESTINATION ${CMAKE_CURRENT_SOURCE_DIR}/../../Assets/uPiper/Plugins/${CMAKE_SYSTEM_NAME}
)

# Display configuration
message(STATUS "=== Flite Unity Configuration ===")
message(STATUS "Platform: ${CMAKE_SYSTEM_NAME}")
message(STATUS "Architecture: ${CMAKE_SYSTEM_PROCESSOR}")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "Lean build: ${FLITE_LEAN_AND_MEAN}")
message(STATUS "================================")