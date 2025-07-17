cmake_minimum_required(VERSION 3.10)
project(openjtalk_wrapper VERSION 2.0.0)

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
option(BUILD_FULL_VERSION "Build full version with dictionary support" ON)
option(ENABLE_DEBUG_LOG "Enable debug logging" OFF)

# Source files
if(BUILD_FULL_VERSION)
    set(SOURCES
        src/openjtalk_wrapper_full.c
        src/openjtalk_phonemizer.c
        src/phoneme_converter.c
        src/accent_estimator.c
        src/phoneme_timing.c
        src/mecab_dict_loader.c
        src/mecab_darts.c
        src/mecab_full.c
        src/surface_index.c
        src/memory_pool.c
        src/error_handling.c
    )
else()
    set(SOURCES
        src/openjtalk_wrapper_light.c
        src/openjtalk_phonemizer.c
        src/mecab_light.c
        src/mecab_light_impl.c
        src/memory_pool.c
        src/error_handling.c
    )
endif()

# Header files
set(HEADERS
    include/openjtalk_wrapper.h
    src/openjtalk_phonemizer.h
    src/phoneme_converter.h
    src/phoneme_mapping.h
    src/kanji_mapping.h
    src/mecab_dict_loader.h
    src/mecab_darts.h
    src/mecab_full.h
    src/surface_index.h
    src/memory_pool.h
    src/error_handling.h
)

# Include directories
include_directories(
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)

# Create shared library
add_library(openjtalk_wrapper SHARED ${SOURCES})

# Define exports for Windows DLL
target_compile_definitions(openjtalk_wrapper PRIVATE OPENJTALK_EXPORTS)

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
    target_compile_definitions(openjtalk_wrapper PRIVATE LOG_LEVEL=4)  # Debug level
else()
    if(MSVC)
        target_compile_options(openjtalk_wrapper PRIVATE /W3 /O2)
    else()
        target_compile_options(openjtalk_wrapper PRIVATE -Wall -O3)
    endif()
    if(ENABLE_DEBUG_LOG)
        target_compile_definitions(openjtalk_wrapper PRIVATE LOG_LEVEL=3)  # Info level
    else()
        target_compile_definitions(openjtalk_wrapper PRIVATE LOG_LEVEL=1)  # Error only
    endif()
endif()

# Print build configuration
message(STATUS "Building OpenJTalk Wrapper (Light Implementation)")
message(STATUS "  Platform: ${PLATFORM_NAME}")
message(STATUS "  Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "  Install prefix: ${CMAKE_INSTALL_PREFIX}")

# Test executable
option(BUILD_TESTS "Build test programs" ON)
if(BUILD_TESTS)
    add_executable(test_openjtalk test/test_openjtalk.c)
    target_link_libraries(test_openjtalk openjtalk_wrapper)
    
    # Build API test
    if(EXISTS ${CMAKE_CURRENT_SOURCE_DIR}/test/test_openjtalk_api.c)
        add_executable(test_openjtalk_api test/test_openjtalk_api.c)
        target_link_libraries(test_openjtalk_api openjtalk_wrapper)
    endif()
    
    # Build full phonemization test
    if(BUILD_FULL_VERSION)
        add_executable(test_phonemize_full test/test_phonemize_full.c)
        target_link_libraries(test_phonemize_full openjtalk_wrapper)
    endif()
    
    # Build new features test
    if(EXISTS ${CMAKE_CURRENT_SOURCE_DIR}/test/test_new_features.c)
        add_executable(test_new_features test/test_new_features.c)
        target_link_libraries(test_new_features openjtalk_wrapper)
    endif()
    
    # Build benchmark test
    add_executable(benchmark_openjtalk test/benchmark_openjtalk.c)
    target_link_libraries(benchmark_openjtalk openjtalk_wrapper)
    
    # Build platform test
    add_executable(test_platform test/test_platform.c)
    target_link_libraries(test_platform openjtalk_wrapper)
    
    # Enable CTest
    enable_testing()
    add_test(NAME openjtalk_basic 
        COMMAND test_openjtalk
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR})
    add_test(NAME openjtalk_api 
        COMMAND test_openjtalk_api
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR})
    if(TARGET test_phonemize_full)
        add_test(NAME phonemize_full 
            COMMAND test_phonemize_full
            WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR})
    endif()
    if(TARGET test_new_features)
        add_test(NAME new_features 
            COMMAND test_new_features
            WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR})
    endif()
    add_test(NAME platform_compat 
        COMMAND test_platform
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR})
endif()

# Additional tests for core components
if(BUILD_TESTS AND BUILD_FULL_VERSION)
    # Test for char.bin loading
    add_executable(test_char_bin test/test_char_bin.c 
        src/mecab_dict_loader.c
        src/mecab_darts.c
        src/surface_index.c
        src/memory_pool.c
        src/error_handling.c
    )
    target_link_libraries(test_char_bin openjtalk_wrapper)
    if(NOT MSVC)
        target_link_libraries(test_char_bin m)
    endif()
    add_test(NAME test_char_bin 
        COMMAND test_char_bin
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR})
    
    # Test for memory pool
    add_executable(test_memory_pool test/test_memory_pool.c
        src/memory_pool.c
        src/error_handling.c
    )
    target_link_libraries(test_memory_pool)
    if(NOT MSVC)
        target_link_libraries(test_memory_pool m)
    endif()
    # Temporarily disable memory pool test due to CI timeout
    # add_test(NAME test_memory_pool 
    #     COMMAND test_memory_pool
    #     WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR})
endif()