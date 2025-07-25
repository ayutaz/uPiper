@echo off
REM Build x86 dependencies using Docker

echo === Building x86 Dependencies for Android ===

docker-compose run --rm android-build bash -c "apt-get update -qq && apt-get install -y -qq dos2unix && dos2unix build_x86_dependencies.sh && chmod +x build_x86_dependencies.sh && ./build_x86_dependencies.sh"

pause