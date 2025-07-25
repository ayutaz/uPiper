@echo off
echo === Detailed library size analysis ===
docker-compose run --rm android-build /bin/bash -c "cd /workspace && echo 'File sizes:' && ls -la output/android/*/libopenjtalk_wrapper.so && echo '' && echo 'Section sizes (arm64-v8a):' && size output/android/arm64-v8a/libopenjtalk_wrapper.so && echo '' && echo 'Dependencies:' && ldd output/android/arm64-v8a/libopenjtalk_wrapper.so 2>/dev/null || echo 'ldd not available for Android binaries' && echo '' && echo 'File type:' && file output/android/*/libopenjtalk_wrapper.so"
pause