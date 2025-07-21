#!/bin/bash
set -e

echo "Building test_openjtalk_full..."

gcc -o test_openjtalk_full test_openjtalk_full.c \
    -I../include \
    -L../build/lib \
    -lopenjtalk_wrapper \
    -Wl,-rpath,../build/lib

echo "Build complete. Running test..."
export OPENJTALK_DICT="../naist_jdic/open_jtalk_dic_utf_8-1.11"
./test_openjtalk_full