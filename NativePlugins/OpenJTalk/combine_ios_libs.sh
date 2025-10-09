#!/bin/bash

# Combine all static libraries into a single fat library for iOS

set -e

CURRENT_DIR=$(pwd)
OUTPUT_LIB="${CURRENT_DIR}/../../Assets/uPiper/Plugins/iOS/libopenjtalk_wrapper.a"
TEMP_DIR="${CURRENT_DIR}/build_ios/combined_libs"

# Create temp directory
rm -rf "${TEMP_DIR}"
mkdir -p "${TEMP_DIR}"
cd "${TEMP_DIR}"

echo "Extracting object files from all libraries..."

# Extract wrapper library
ar -x "${CURRENT_DIR}/build_ios/OS64/lib/Release/libopenjtalk_wrapper.dylib"

# Extract OpenJTalk libraries
for lib in \
    "${CURRENT_DIR}/external/open_jtalk-1.11/jpcommon/libjpcommon.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/njd2jpcommon/libnjd2jpcommon.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/njd_set_unvoiced_vowel/libnjd_set_unvoiced_vowel.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/njd_set_pronunciation/libnjd_set_pronunciation.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/njd_set_long_vowel/libnjd_set_long_vowel.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/njd_set_digit/libnjd_set_digit.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/njd_set_accent_type/libnjd_set_accent_type.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/njd_set_accent_phrase/libnjd_set_accent_phrase.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/njd/libnjd.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/mecab2njd/libmecab2njd.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/text2mecab/libtext2mecab.a" \
    "${CURRENT_DIR}/external/open_jtalk-1.11/mecab/src/libmecab.a" \
    "${CURRENT_DIR}/external/openjtalk_build/install/lib/libHTSEngine.a"
do
    if [ -f "$lib" ]; then
        echo "Extracting: $(basename $lib)"
        ar -x "$lib"
    else
        echo "Warning: Library not found: $lib"
    fi
done

echo "Creating combined library..."
# Create the combined library
ar -crs "${OUTPUT_LIB}" *.o

echo "Library size: $(du -h "${OUTPUT_LIB}" | cut -f1)"
echo "Number of object files: $(ls -1 *.o | wc -l)"

# Cleanup
cd "${CURRENT_DIR}"
rm -rf "${TEMP_DIR}"

echo "Combined library created at: ${OUTPUT_LIB}"