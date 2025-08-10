# Full OpenJTalk WebAssembly Build using CMake approach (like piper-plus)
FROM emscripten/emsdk:3.1.39

# Install build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    curl \
    tar \
    git \
    python3 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /build

# Download OpenJTalk source
RUN curl -L -o open_jtalk-1.11.tar.gz \
    https://sourceforge.net/projects/open-jtalk/files/Open%20JTalk/open_jtalk-1.11/open_jtalk-1.11.tar.gz/download \
    && tar -xzf open_jtalk-1.11.tar.gz

# Download dictionary
RUN curl -L -o open_jtalk_dic_utf_8-1.11.tar.gz \
    https://sourceforge.net/projects/open-jtalk/files/Dictionary/open_jtalk_dic-1.11/open_jtalk_dic_utf_8-1.11.tar.gz/download \
    && tar -xzf open_jtalk_dic_utf_8-1.11.tar.gz

# Create CMakeLists.txt for OpenJTalk (simplified version)
RUN cat > /build/open_jtalk-1.11/CMakeLists.txt << 'EOF'
cmake_minimum_required(VERSION 3.13)
project(OpenJTalk C CXX)

set(CMAKE_C_STANDARD 99)
set(CMAKE_CXX_STANDARD 11)

# Source files for mecab
file(GLOB MECAB_SOURCES mecab/src/*.cpp)

# Source files for other components
file(GLOB NJD_SOURCES njd/*.c)
file(GLOB TEXT2MECAB_SOURCES text2mecab/*.c)
file(GLOB MECAB2NJD_SOURCES mecab2njd/*.c)
file(GLOB NJD2JPCOMMON_SOURCES njd2jpcommon/*.c)
file(GLOB JPCOMMON_SOURCES jpcommon/*.c)
file(GLOB NJD_SET_SOURCES 
    njd_set_pronunciation/*.c
    njd_set_digit/*.c
    njd_set_accent_phrase/*.c
    njd_set_accent_type/*.c
    njd_set_unvoiced_vowel/*.c
    njd_set_long_vowel/*.c
)

# Create static library
add_library(openjtalk_static STATIC
    ${MECAB_SOURCES}
    ${NJD_SOURCES}
    ${TEXT2MECAB_SOURCES}
    ${MECAB2NJD_SOURCES}
    ${NJD2JPCOMMON_SOURCES}
    ${JPCOMMON_SOURCES}
    ${NJD_SET_SOURCES}
)

# Include directories
target_include_directories(openjtalk_static PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/mecab/src
    ${CMAKE_CURRENT_SOURCE_DIR}/njd
    ${CMAKE_CURRENT_SOURCE_DIR}/jpcommon
    ${CMAKE_CURRENT_SOURCE_DIR}/text2mecab
    ${CMAKE_CURRENT_SOURCE_DIR}/mecab2njd
    ${CMAKE_CURRENT_SOURCE_DIR}/njd2jpcommon
    ${CMAKE_CURRENT_SOURCE_DIR}/njd_set_pronunciation
    ${CMAKE_CURRENT_SOURCE_DIR}/njd_set_digit
    ${CMAKE_CURRENT_SOURCE_DIR}/njd_set_accent_phrase
    ${CMAKE_CURRENT_SOURCE_DIR}/njd_set_accent_type
    ${CMAKE_CURRENT_SOURCE_DIR}/njd_set_unvoiced_vowel
    ${CMAKE_CURRENT_SOURCE_DIR}/njd_set_long_vowel
)

# Definitions
target_compile_definitions(openjtalk_static PRIVATE
    DIC_VERSION=102
    PACKAGE="open_jtalk"
    VERSION="1.11"
    CHARSET_UTF_8
)

# C++ compatibility
set_property(TARGET openjtalk_static PROPERTY CXX_STANDARD 11)
EOF

# Build OpenJTalk with CMake and Emscripten
WORKDIR /build/open_jtalk-1.11
RUN emcmake cmake . \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_CXX_FLAGS="-Wno-deprecated-declarations" \
    && emmake make -j$(nproc)

# Create wrapper
WORKDIR /build
COPY openjtalk_wasm_cmake.c /build/

# Prepare dictionary
RUN mkdir -p dict_data \
    && cp open_jtalk_dic_utf_8-1.11/*.bin dict_data/ \
    && cp open_jtalk_dic_utf_8-1.11/*.dic dict_data/ \
    && cp open_jtalk_dic_utf_8-1.11/*.def dict_data/

# Build WebAssembly module
RUN emcc openjtalk_wasm_cmake.c \
    -o openjtalk-unity-full.js \
    -I/build/open_jtalk-1.11/mecab/src \
    -I/build/open_jtalk-1.11/njd \
    -I/build/open_jtalk-1.11/jpcommon \
    -I/build/open_jtalk-1.11/text2mecab \
    -I/build/open_jtalk-1.11/mecab2njd \
    -I/build/open_jtalk-1.11/njd2jpcommon \
    -I/build/open_jtalk-1.11/njd_set_pronunciation \
    -I/build/open_jtalk-1.11/njd_set_digit \
    -I/build/open_jtalk-1.11/njd_set_accent_phrase \
    -I/build/open_jtalk-1.11/njd_set_accent_type \
    -I/build/open_jtalk-1.11/njd_set_unvoiced_vowel \
    -I/build/open_jtalk-1.11/njd_set_long_vowel \
    /build/open_jtalk-1.11/libopenjtalk_static.a \
    -s EXPORTED_FUNCTIONS='["_malloc","_free","_Open_JTalk_initialize","_Open_JTalk_load","_Open_JTalk_synthesis","_Open_JTalk_clear","_allocate_memory","_free_memory","_get_string_length"]' \
    -s EXPORTED_RUNTIME_METHODS='["ccall","cwrap","UTF8ToString","stringToUTF8","FS"]' \
    -s MODULARIZE=1 \
    -s EXPORT_NAME='OpenJTalkModule' \
    -s ALLOW_MEMORY_GROWTH=1 \
    -s INITIAL_MEMORY=134217728 \
    -s FILESYSTEM=1 \
    -s FORCE_FILESYSTEM=1 \
    --preload-file dict_data@/dict \
    -O2 \
    -lm

CMD ["sh", "-c", "cp openjtalk-unity-full.* /output/"]