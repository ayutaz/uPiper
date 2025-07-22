@echo off
REM Test Android build using Docker

echo === Running Android Build Test ===

REM Run the test build script in Docker
docker-compose run --rm android-build bash -c "apt-get update -qq && apt-get install -y -qq dos2unix && dos2unix test_android_build.sh && chmod +x test_android_build.sh && ./test_android_build.sh"

pause