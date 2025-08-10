@echo off
echo Starting test server on http://localhost:8080
echo.
echo Test URLs:
echo - http://localhost:8080/test-onnx-runtime-integration.html
echo - http://localhost:8080/test-openjtalk-wasm-cli.mjs
echo.
echo Press Ctrl+C to stop the server
echo.
python -m http.server 8080