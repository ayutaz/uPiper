@echo off
REM Setup Unity Android plugin structure

echo === Setting up Unity Android Plugin Structure ===

REM Run the setup script in Docker to ensure consistent paths
docker-compose run --rm android-build bash -c "apt-get update -qq && apt-get install -y -qq dos2unix uuid-runtime && dos2unix setup_unity_android_plugin.sh && chmod +x setup_unity_android_plugin.sh && ./setup_unity_android_plugin.sh"

pause