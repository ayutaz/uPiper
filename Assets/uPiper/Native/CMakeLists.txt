cmake_minimum_required(VERSION 3.10)
project(uPiperNative)

# C++ standard
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Platform detection
if(WIN32)
    set(PLATFORM_NAME "Windows")
    set(LIBRARY_PREFIX "")
    set(LIBRARY_SUFFIX ".dll")
    add_definitions(-DOPENJTALK_EXPORTS)
elseif(UNIX AND NOT APPLE)
    set(PLATFORM_NAME "Linux")
    set(LIBRARY_PREFIX "lib")
    set(LIBRARY_SUFFIX ".so")
    set(CMAKE_POSITION_INDEPENDENT_CODE ON)
elseif(APPLE)
    set(PLATFORM_NAME "macOS")
    set(LIBRARY_PREFIX "lib")
    set(LIBRARY_SUFFIX ".dylib")
    set(CMAKE_MACOSX_RPATH ON)
else()
    message(FATAL_ERROR "Unsupported platform")
endif()

# Output directory
set(OUTPUT_DIR "${CMAKE_SOURCE_DIR}/../Plugins/${PLATFORM_NAME}")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_DIR})
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_DIR})

# Create output directory
file(MAKE_DIRECTORY ${OUTPUT_DIR})

# OpenJTalk wrapper library
add_library(openjtalk_wrapper SHARED
    OpenJTalk/openjtalk_wrapper.cpp
    OpenJTalk/openjtalk_wrapper.h
)

# Set library properties
set_target_properties(openjtalk_wrapper PROPERTIES
    PREFIX "${LIBRARY_PREFIX}"
    SUFFIX "${LIBRARY_SUFFIX}"
    OUTPUT_NAME "openjtalk_wrapper"
)

# Include directories
target_include_directories(openjtalk_wrapper PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/OpenJTalk
)

# Platform-specific settings
if(WIN32)
    # Windows specific flags
    target_compile_definitions(openjtalk_wrapper PRIVATE
        _CRT_SECURE_NO_WARNINGS
        NOMINMAX
    )
    
    # Link against Windows libraries if needed
    target_link_libraries(openjtalk_wrapper
        # Add Windows-specific libraries here if needed
    )
elseif(UNIX)
    # Unix specific flags
    target_compile_options(openjtalk_wrapper PRIVATE
        -fPIC
        -fvisibility=hidden
    )
    
    # Link against pthread on Unix
    find_package(Threads REQUIRED)
    target_link_libraries(openjtalk_wrapper
        Threads::Threads
    )
endif()

# Install rules
install(TARGETS openjtalk_wrapper
    LIBRARY DESTINATION ${OUTPUT_DIR}
    RUNTIME DESTINATION ${OUTPUT_DIR}
)

# Copy header file for reference
install(FILES OpenJTalk/openjtalk_wrapper.h
    DESTINATION ${OUTPUT_DIR}
)

# Print configuration
message(STATUS "Platform: ${PLATFORM_NAME}")
message(STATUS "Output directory: ${OUTPUT_DIR}")
message(STATUS "Library name: ${LIBRARY_PREFIX}openjtalk_wrapper${LIBRARY_SUFFIX}")