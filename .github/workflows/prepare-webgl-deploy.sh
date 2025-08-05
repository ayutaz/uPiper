#!/bin/bash

# WebGL build output directory
BUILD_DIR="build/WebGL/uPiperWebGL"

# Check if build directory exists
if [ ! -d "$BUILD_DIR" ]; then
    echo "Error: Build directory not found: $BUILD_DIR"
    echo "Listing build directory contents:"
    ls -la build/WebGL/ || echo "WebGL directory not found"
    exit 1
fi

echo "Build directory contents:"
ls -la "$BUILD_DIR"

# Create a simple demo launcher page
echo "Creating demo launcher page..."
cat > "$BUILD_DIR/demo.html" << 'EOF'
<!DOCTYPE html>
<html lang="ja">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>uPiper WebGL Demo - Unity Text-to-Speech</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            margin: 0;
            padding: 0;
            background: #1a1a1a;
            color: #fff;
            overflow: hidden;
            height: 100vh;
        }
        .header {
            background: #2a2a2a;
            padding: 15px;
            text-align: center;
            border-bottom: 2px solid #4CAF50;
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            z-index: 1000;
        }
        h1 {
            margin: 0;
            font-size: 1.5em;
            color: #4CAF50;
        }
        .unity-container {
            position: absolute;
            top: 70px;
            left: 0;
            right: 0;
            bottom: 0;
        }
        #unity-frame {
            width: 100%;
            height: 100%;
            border: none;
        }
        .info {
            position: fixed;
            bottom: 10px;
            right: 10px;
            background: rgba(0,0,0,0.8);
            padding: 10px;
            border-radius: 5px;
            font-size: 12px;
        }
        .info a {
            color: #4CAF50;
            text-decoration: none;
        }
    </style>
</head>
<body>
    <div class="header">
        <h1>🎤 uPiper WebGL Demo</h1>
    </div>
    
    <div class="unity-container">
        <iframe id="unity-frame" src="./index.html"></iframe>
    </div>
    
    <div class="info">
        <a href="https://github.com/ayutaz/uPiper" target="_blank">GitHub</a> | 
        <a href="https://github.com/ayutaz/uPiper/wiki" target="_blank">Docs</a>
    </div>
</body>
</html>
EOF

# Create a simple redirect for root if index.html doesn't exist
if [ ! -f "$BUILD_DIR/index.html" ]; then
    echo "Warning: index.html not found in build output"
    echo "Unity WebGL build might have failed or output structure changed"
    
    # Check for alternative locations
    if [ -f "$BUILD_DIR/Build/index.html" ]; then
        echo "Found index.html in Build subdirectory, moving files..."
        mv "$BUILD_DIR/Build/"* "$BUILD_DIR/"
        rmdir "$BUILD_DIR/Build"
    fi
fi

# Create .nojekyll file to prevent GitHub Pages Jekyll processing
touch "$BUILD_DIR/.nojekyll"

# Create a simple README for the deployment
cat > "$BUILD_DIR/README.md" << 'EOF'
# uPiper WebGL Demo

This is the WebGL build of uPiper - Unity Text-to-Speech Plugin.

## Links
- [Launch Demo](./demo.html)
- [Direct Unity Build](./index.html)
- [GitHub Repository](https://github.com/ayutaz/uPiper)

## Features
- Japanese TTS (OpenJTalk)
- English TTS (eSpeak-ng)
- Multi-language support
- Real-time synthesis
- WebAssembly performance
EOF

echo "WebGL deployment preparation complete!"
echo "Final directory structure:"
ls -la "$BUILD_DIR"