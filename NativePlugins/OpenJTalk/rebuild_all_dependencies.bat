@echo off
REM Rebuild all Android dependencies using Docker

echo === Rebuilding All Android Dependencies ===

docker-compose run --rm android-build bash -c "apt-get update -qq && apt-get install -y -qq dos2unix && dos2unix rebuild_all_dependencies.sh && chmod +x rebuild_all_dependencies.sh && ./rebuild_all_dependencies.sh"

pause