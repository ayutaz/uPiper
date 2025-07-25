@echo off
echo === Copying Android libraries to Unity ===
docker-compose run --rm android-build /bin/bash -c "apt-get update && apt-get install -y dos2unix && dos2unix copy_to_unity.sh && ./copy_to_unity.sh"
pause