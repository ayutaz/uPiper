@echo off
REM Verify Android JNI symbols using Docker

echo === Verifying Android JNI Symbols ===

docker-compose run --rm android-build bash -c "chmod +x verify_android_symbols.sh && ./verify_android_symbols.sh"

pause