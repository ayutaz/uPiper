cmake_minimum_required(VERSION 3.10)
project(openjtalk_wrapper VERSION 3.0.0)

# C standard
set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)

# Output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Find OpenJTalk installation from cross-compile build
set(OPENJTALK_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../external/openjtalk_build")
set(OPENJTALK_INCLUDE_DIR "${OPENJTALK_ROOT}/install/include")
set(OPENJTALK_LIB_DIR "${OPENJTALK_ROOT}/install/lib")
set(OPENJTALK_SRC_DIR "${OPENJTALK_ROOT}/open_jtalk-1.11")

# Source files
set(SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/../src/openjtalk_full_wrapper.c
)

# Include directories
include_directories(
    ${CMAKE_CURRENT_SOURCE_DIR}/../include
    ${OPENJTALK_INCLUDE_DIR}
)

# Additional includes for OpenJTalk internals
if(EXISTS "${OPENJTALK_SRC_DIR}")
    include_directories(
        ${OPENJTALK_SRC_DIR}/jpcommon
        ${OPENJTALK_SRC_DIR}/njd
        ${OPENJTALK_SRC_DIR}/mecab/src
        ${OPENJTALK_SRC_DIR}/text2mecab
        ${OPENJTALK_SRC_DIR}/mecab2njd
        ${OPENJTALK_SRC_DIR}/njd_set_pronunciation
        ${OPENJTALK_SRC_DIR}/njd_set_digit
        ${OPENJTALK_SRC_DIR}/njd_set_accent_phrase
        ${OPENJTALK_SRC_DIR}/njd_set_accent_type
        ${OPENJTALK_SRC_DIR}/njd_set_unvoiced_vowel
        ${OPENJTALK_SRC_DIR}/njd_set_long_vowel
        ${OPENJTALK_SRC_DIR}/njd2jpcommon
    )
endif()

# Create shared library
add_library(openjtalk_wrapper SHARED ${SOURCES})

# Windows specific definitions
target_compile_definitions(openjtalk_wrapper PRIVATE
    OPENJTALK_EXPORTS
    _CRT_SECURE_NO_WARNINGS
)

# Link libraries - use the static libraries built by cross-compilation
set(OPENJTALK_LIBS
    ${OPENJTALK_SRC_DIR}/jpcommon/libjpcommon.a
    ${OPENJTALK_SRC_DIR}/njd2jpcommon/libnjd2jpcommon.a
    ${OPENJTALK_SRC_DIR}/njd_set_unvoiced_vowel/libnjd_set_unvoiced_vowel.a
    ${OPENJTALK_SRC_DIR}/njd_set_long_vowel/libnjd_set_long_vowel.a
    ${OPENJTALK_SRC_DIR}/njd_set_pronunciation/libnjd_set_pronunciation.a
    ${OPENJTALK_SRC_DIR}/njd_set_digit/libnjd_set_digit.a
    ${OPENJTALK_SRC_DIR}/njd_set_accent_type/libnjd_set_accent_type.a
    ${OPENJTALK_SRC_DIR}/njd_set_accent_phrase/libnjd_set_accent_phrase.a
    ${OPENJTALK_SRC_DIR}/njd/libnjd.a
    ${OPENJTALK_SRC_DIR}/mecab2njd/libmecab2njd.a
    ${OPENJTALK_SRC_DIR}/text2mecab/libtext2mecab.a
    ${OPENJTALK_SRC_DIR}/mecab/src/libmecab.a
    ${OPENJTALK_LIB_DIR}/libHTSEngine.a
)

# Link with static libraries and Windows system libraries
target_link_libraries(openjtalk_wrapper
    ${OPENJTALK_LIBS}
    -static
    -lstdc++
    -lwinmm
    -lws2_32
    -lm
)

# Windows DLL properties
set_target_properties(openjtalk_wrapper PROPERTIES
    PREFIX ""
    OUTPUT_NAME "openjtalk_wrapper"
    SUFFIX ".dll"
)

# Print build configuration
message(STATUS "Cross-compiling OpenJTalk Wrapper for Windows")
message(STATUS "  Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "  OpenJTalk root: ${OPENJTALK_ROOT}")
message(STATUS "  Source file: ${SOURCES}")
message(STATUS "  CMAKE_CURRENT_SOURCE_DIR: ${CMAKE_CURRENT_SOURCE_DIR}")
message(STATUS "  Static linking: YES")