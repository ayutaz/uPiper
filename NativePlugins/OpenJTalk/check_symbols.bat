@echo off
echo === Checking symbols in Android libraries ===
docker-compose run --rm android-build /bin/bash -c "cd /workspace && for abi in arm64-v8a armeabi-v7a x86_64 x86; do echo '=== Symbols for '$abi' ==='; nm -D output/android/$abi/libopenjtalk_wrapper.so 2>/dev/null | grep -E '(openjtalk_|Java_)' | head -20; echo ''; done"
pause