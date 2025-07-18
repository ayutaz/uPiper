#!/bin/bash
# Test script for full mecab-naist-jdic dictionary

set -e

echo "=== Testing OpenJTalk with full dictionary ==="

# Check if dictionary exists
DICT_PATH="${1:-/usr/local/lib/mecab/dic/naist-jdic}"
if [ ! -d "$DICT_PATH" ]; then
    echo "Dictionary not found at: $DICT_PATH"
    echo "Please install mecab-naist-jdic or specify dictionary path as argument"
    echo "Usage: $0 [dictionary_path]"
    exit 1
fi

echo "Using dictionary: $DICT_PATH"

# Build directory
BUILD_DIR="../build"
if [ ! -d "$BUILD_DIR" ]; then
    echo "Build directory not found. Running build..."
    mkdir -p "$BUILD_DIR"
    cd "$BUILD_DIR"
    cmake ..
    make
    cd -
fi

# Check dictionary files
echo ""
echo "Checking dictionary files:"
for file in sys.dic unk.dic matrix.bin char.bin; do
    if [ -f "$DICT_PATH/$file" ]; then
        size=$(ls -lh "$DICT_PATH/$file" | awk '{print $5}')
        echo "  ✓ $file ($size)"
    else
        echo "  ✗ $file not found"
    fi
done

# Run tests
echo ""
echo "Running tests with full dictionary:"

# Basic test
echo ""
echo "1. Basic phonemization test:"
"$BUILD_DIR/bin/test_openjtalk" "$DICT_PATH" || echo "Basic test failed"

# Benchmark test
echo ""
echo "2. Performance benchmark:"
"$BUILD_DIR/bin/benchmark_openjtalk" "$DICT_PATH" || echo "Benchmark failed"

# Complex sentences test
echo ""
echo "3. Complex sentences test:"
cat > /tmp/test_sentences.txt << EOF
今日は良い天気ですね。
花が咲く季節になりました。
東京都千代田区永田町１－７－１
人工知能技術の発展により、様々な分野で革新的な変化が起きています。
すもももももももものうち
EOF

if [ -f "$BUILD_DIR/bin/test_phonemize_full" ]; then
    while IFS= read -r line; do
        echo "Testing: $line"
        echo "$line" | "$BUILD_DIR/bin/test_phonemize_full" "$DICT_PATH" 2>&1 | grep -E "(Phonemes:|Error)" || true
    done < /tmp/test_sentences.txt
fi

# Memory usage test
echo ""
echo "4. Memory usage check:"
if command -v /usr/bin/time &> /dev/null; then
    /usr/bin/time -l "$BUILD_DIR/bin/benchmark_openjtalk" "$DICT_PATH" 2>&1 | grep -E "(maximum resident|elapsed)" || true
elif command -v time &> /dev/null; then
    time "$BUILD_DIR/bin/benchmark_openjtalk" "$DICT_PATH" > /dev/null
fi

echo ""
echo "=== Test completed ==="