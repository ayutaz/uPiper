#!/bin/bash

# WebGL build output directory
BUILD_DIR="build/WebGL/uPiperWebGL"

# Check if build directory exists
if [ ! -d "$BUILD_DIR" ]; then
    echo "Error: Build directory not found: $BUILD_DIR"
    exit 1
fi

# Copy demo page HTML
echo "Copying demo page..."
cp .github/workflows/webgl-demo-page.html "$BUILD_DIR/demo.html"

# Create a simple redirect index.html if needed
if [ ! -f "$BUILD_DIR/index.html" ]; then
    echo "Creating redirect index.html..."
    cat > "$BUILD_DIR/index.html" << 'EOF'
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <meta http-equiv="refresh" content="0; url=demo.html">
    <title>Redirecting...</title>
</head>
<body>
    <p>Redirecting to demo page...</p>
</body>
</html>
EOF
fi

# Create a simple 404.html for GitHub Pages
cat > "$BUILD_DIR/404.html" << 'EOF'
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>404 - Page Not Found</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            text-align: center;
            padding: 50px;
            background: #1a1a1a;
            color: #fff;
        }
        a { color: #4CAF50; }
    </style>
</head>
<body>
    <h1>404 - Page Not Found</h1>
    <p><a href="/">Return to Demo</a></p>
</body>
</html>
EOF

# Create .nojekyll file to prevent GitHub Pages Jekyll processing
touch "$BUILD_DIR/.nojekyll"

echo "WebGL deployment preparation complete!"