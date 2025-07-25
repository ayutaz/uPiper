cmake_minimum_required(VERSION 3.10)
project(espeak_wrapper)

# Set C standard
set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Platform detection
if(WIN32)
    add_definitions(-D_WIN32)
    add_definitions(-DESPEAK_WRAPPER_EXPORTS)
elseif(APPLE)
    add_definitions(-D_APPLE)
elseif(ANDROID)
    add_definitions(-D_ANDROID)
else()
    add_definitions(-D_LINUX)
endif()

# Source files
set(SOURCES
    src/espeak_wrapper.c
)

# Include directories
include_directories(
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_SOURCE_DIR}/external/espeak-ng/src/include
    ${CMAKE_CURRENT_SOURCE_DIR}/external/espeak-ng/src/include/espeak-ng
)

# Find or build eSpeak-NG
if(NOT ESPEAK_NG_DIR)
    set(ESPEAK_NG_DIR ${CMAKE_CURRENT_SOURCE_DIR}/external/espeak-ng)
endif()

# Add eSpeak-NG source files
file(GLOB ESPEAK_NG_SOURCES
    ${ESPEAK_NG_DIR}/src/libespeak-ng/*.c
    ${ESPEAK_NG_DIR}/src/libespeak-ng/*.cpp
)

# Remove platform-specific files
if(NOT WIN32)
    list(REMOVE_ITEM ESPEAK_NG_SOURCES ${ESPEAK_NG_DIR}/src/libespeak-ng/windows.c)
endif()

# Create shared library
add_library(espeak_wrapper SHARED
    ${SOURCES}
    ${ESPEAK_NG_SOURCES}
)

# Platform-specific settings
if(WIN32)
    target_compile_definitions(espeak_wrapper PRIVATE
        WINDOWS
        _CRT_SECURE_NO_WARNINGS
    )
    set_target_properties(espeak_wrapper PROPERTIES
        PREFIX ""
        OUTPUT_NAME "espeak_wrapper"
    )
elseif(APPLE)
    set_target_properties(espeak_wrapper PROPERTIES
        PREFIX "lib"
        OUTPUT_NAME "espeak_wrapper"
        SUFFIX ".dylib"
        MACOSX_RPATH ON
        INSTALL_RPATH "@loader_path"
    )
    # Universal binary for Apple Silicon and Intel
    set(CMAKE_OSX_ARCHITECTURES "arm64;x86_64")
elseif(ANDROID)
    set_target_properties(espeak_wrapper PROPERTIES
        PREFIX "lib"
        OUTPUT_NAME "espeak_wrapper"
    )
else()
    set_target_properties(espeak_wrapper PROPERTIES
        PREFIX "lib"
        OUTPUT_NAME "espeak_wrapper"
        SUFFIX ".so"
    )
endif()

# Link libraries
if(WIN32)
    target_link_libraries(espeak_wrapper PRIVATE ws2_32)
elseif(NOT APPLE AND NOT ANDROID)
    target_link_libraries(espeak_wrapper PRIVATE pthread m)
endif()

# Installation
install(TARGETS espeak_wrapper
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

install(FILES include/espeak_wrapper.h
    DESTINATION include
)

# Copy data files
install(DIRECTORY ${ESPEAK_NG_DIR}/espeak-ng-data
    DESTINATION share
)