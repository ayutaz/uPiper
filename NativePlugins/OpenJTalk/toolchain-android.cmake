# Android NDK toolchain file for OpenJTalk
# Usage: cmake -DCMAKE_TOOLCHAIN_FILE=toolchain-android.cmake ..

# Android NDK path
if(NOT DEFINED ENV{ANDROID_NDK_HOME} AND NOT DEFINED ANDROID_NDK_HOME)
    message(FATAL_ERROR "ANDROID_NDK_HOME environment variable must be set")
endif()

if(NOT DEFINED ANDROID_NDK_HOME)
    set(ANDROID_NDK_HOME $ENV{ANDROID_NDK_HOME})
endif()

# Android settings
set(CMAKE_SYSTEM_NAME Android)
set(CMAKE_SYSTEM_VERSION 21) # API level 21 = Android 5.0
set(CMAKE_ANDROID_ARCH_ABI ${ANDROID_ABI})
set(CMAKE_ANDROID_NDK ${ANDROID_NDK_HOME})

# Default to arm64-v8a if not specified
if(NOT DEFINED ANDROID_ABI)
    set(ANDROID_ABI "arm64-v8a")
endif()

# Compiler settings
set(CMAKE_ANDROID_STL_TYPE c++_static)

# Platform definitions
add_definitions(-DANDROID)
add_definitions(-D__ANDROID__)

# Disable some features not available on Android
add_definitions(-DNO_SYSLOG)

# Position independent code
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Export all symbols for JNI
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fvisibility=default")
set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -fvisibility=default")