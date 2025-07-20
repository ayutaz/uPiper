cmake_minimum_required(VERSION 3.10)
project(openjtalk_wrapper VERSION 3.0.0)

# C standard
set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)

# Platform detection
if(WIN32)
    set(PLATFORM_NAME "windows")
    add_definitions(-D_CRT_SECURE_NO_WARNINGS)
    add_definitions(-DOPENJTALK_EXPORTS)
elseif(UNIX AND NOT APPLE)
    set(PLATFORM_NAME "linux")
    set(CMAKE_POSITION_INDEPENDENT_CODE ON)
elseif(APPLE)
    set(PLATFORM_NAME "macos")
    set(CMAKE_POSITION_INDEPENDENT_CODE ON)
endif()

# Output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Build options
option(ENABLE_DEBUG_LOG "Enable debug logging" OFF)
option(BUILD_TESTS "Build test executables" ON)
option(BUILD_BENCHMARK "Build benchmark executable" ON)

# Find OpenJTalk installation
set(OPENJTALK_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/install")
set(OPENJTALK_INCLUDE_DIR "${OPENJTALK_ROOT}/include")
set(OPENJTALK_LIB_DIR "${OPENJTALK_ROOT}/lib")

# Source files - Full OpenJTalk wrapper only
set(SOURCES
    src/openjtalk_full_wrapper.c
)

# Include directories
include_directories(
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${OPENJTALK_INCLUDE_DIR}
)

# Add include directory for CI builds
if(NOT EXISTS "${OPENJTALK_INCLUDE_DIR}/jpcommon.h")
    message(STATUS "OpenJTalk headers not found in install dir, checking alternate locations")
    # Check for HTSEngine headers
    if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/install/include")
        include_directories(${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/install/include)
    endif()
    # Check for OpenJTalk headers in build directory
    if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11")
        include_directories(
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/jpcommon
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/njd
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/mecab/src
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/text2mecab
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/mecab2njd
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/njd_set_pronunciation
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/njd_set_digit
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/njd_set_accent_phrase
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/njd_set_accent_type
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/njd_set_unvoiced_vowel
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/njd_set_long_vowel
            ${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11/njd2jpcommon
        )
    endif()
endif()

# Create shared library
add_library(openjtalk_wrapper SHARED ${SOURCES})

# Link libraries
set(OPENJTALK_SRC_DIR "${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/open_jtalk-1.11")

# Check if OpenJTalk libraries exist
if(EXISTS "${OPENJTALK_SRC_DIR}/jpcommon/libjpcommon.a")
    # Check for HTSEngine library location
    if(EXISTS "${OPENJTALK_LIB_DIR}/libHTSEngine.a")
        set(HTSENGINE_LIB "${OPENJTALK_LIB_DIR}/libHTSEngine.a")
    elseif(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/install/lib/libHTSEngine.a")
        set(HTSENGINE_LIB "${CMAKE_CURRENT_SOURCE_DIR}/external/openjtalk_build/install/lib/libHTSEngine.a")
    else()
        message(WARNING "HTSEngine library not found - building without it")
        set(HTSENGINE_LIB "")
    endif()
    
    # Link full OpenJTalk libraries
    set(OPENJTALK_LIBS
        ${OPENJTALK_SRC_DIR}/jpcommon/libjpcommon.a
        ${OPENJTALK_SRC_DIR}/njd2jpcommon/libnjd2jpcommon.a
        ${OPENJTALK_SRC_DIR}/njd_set_unvoiced_vowel/libnjd_set_unvoiced_vowel.a
        ${OPENJTALK_SRC_DIR}/njd_set_pronunciation/libnjd_set_pronunciation.a
        ${OPENJTALK_SRC_DIR}/njd_set_long_vowel/libnjd_set_long_vowel.a
        ${OPENJTALK_SRC_DIR}/njd_set_digit/libnjd_set_digit.a
        ${OPENJTALK_SRC_DIR}/njd_set_accent_type/libnjd_set_accent_type.a
        ${OPENJTALK_SRC_DIR}/njd_set_accent_phrase/libnjd_set_accent_phrase.a
        ${OPENJTALK_SRC_DIR}/njd/libnjd.a
        ${OPENJTALK_SRC_DIR}/mecab2njd/libmecab2njd.a
        ${OPENJTALK_SRC_DIR}/text2mecab/libtext2mecab.a
        ${OPENJTALK_SRC_DIR}/mecab/src/libmecab.a
        ${HTSENGINE_LIB}
    )
    
    if(WIN32)
        target_link_libraries(openjtalk_wrapper ${OPENJTALK_LIBS})
    else()
        target_link_libraries(openjtalk_wrapper ${OPENJTALK_LIBS} m)
    endif()
else()
    message(FATAL_ERROR "OpenJTalk libraries not found. Please run fetch_dependencies.sh and build_dependencies.sh first.")
endif()

# Platform-specific settings
if(WIN32)
    set_target_properties(openjtalk_wrapper PROPERTIES
        PREFIX ""
        OUTPUT_NAME "openjtalk_wrapper"
        SUFFIX ".dll"
    )
    
    # Export all symbols on Windows
    set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)
    
elseif(APPLE)
    set_target_properties(openjtalk_wrapper PROPERTIES
        PREFIX "lib"
        OUTPUT_NAME "openjtalk_wrapper"
        SUFFIX ".dylib"
    )
    
    # macOS specific linker flags
    target_link_libraries(openjtalk_wrapper
        "-framework CoreFoundation"
        "-liconv"
        "-lc++"
    )
elseif(UNIX)
    set_target_properties(openjtalk_wrapper PROPERTIES
        PREFIX "lib"
        OUTPUT_NAME "openjtalk_wrapper"
        SUFFIX ".so"
    )
    
    # Link pthread on Unix
    find_package(Threads REQUIRED)
    target_link_libraries(openjtalk_wrapper Threads::Threads)
endif()

# Installation rules
install(TARGETS openjtalk_wrapper
    RUNTIME DESTINATION ${CMAKE_INSTALL_PREFIX}/bin
    LIBRARY DESTINATION ${CMAKE_INSTALL_PREFIX}/lib
    ARCHIVE DESTINATION ${CMAKE_INSTALL_PREFIX}/lib
)

install(FILES include/openjtalk_wrapper.h
    DESTINATION ${CMAKE_INSTALL_PREFIX}/include
)

# Build type configuration
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release)
endif()

# Compiler flags
if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    if(MSVC)
        target_compile_options(openjtalk_wrapper PRIVATE /W4 /Od /Zi)
    else()
        target_compile_options(openjtalk_wrapper PRIVATE -Wall -Wextra -g -O0)
    endif()
    target_compile_definitions(openjtalk_wrapper PRIVATE ENABLE_DEBUG_LOG)
else()
    if(MSVC)
        target_compile_options(openjtalk_wrapper PRIVATE /W3 /O2)
    else()
        target_compile_options(openjtalk_wrapper PRIVATE -Wall -O3)
    endif()
endif()

# Print build configuration
message(STATUS "Building OpenJTalk Wrapper (Full OpenJTalk Version)")
message(STATUS "  Platform: ${PLATFORM_NAME}")
message(STATUS "  Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "  OpenJTalk root: ${OPENJTALK_ROOT}")
message(STATUS "  Install prefix: ${CMAKE_INSTALL_PREFIX}")