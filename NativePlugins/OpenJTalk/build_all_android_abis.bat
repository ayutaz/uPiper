@echo off
REM Build all Android ABIs using Docker

echo === Building OpenJTalk for All Android ABIs ===

REM First build dependencies if needed
echo.
echo Building dependencies first...
docker-compose run --rm android-build bash -c "apt-get update -qq && apt-get install -y -qq dos2unix && dos2unix build_dependencies_android.sh && chmod +x build_dependencies_android.sh && ./build_dependencies_android.sh"

REM Then build wrapper for all ABIs
echo.
echo Building wrapper libraries...
docker-compose run --rm android-build bash -c "apt-get update -qq && apt-get install -y -qq dos2unix && dos2unix build_all_android_abis.sh && chmod +x build_all_android_abis.sh && ./build_all_android_abis.sh"

pause