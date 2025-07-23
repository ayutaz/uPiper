@echo off
echo === Building OpenJTalk for all Android ABIs ===
docker-compose run --rm android-build /bin/bash -c "apt-get update && apt-get install -y dos2unix && dos2unix build_android.sh && ./build_android.sh"
pause