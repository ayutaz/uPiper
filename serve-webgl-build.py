#!/usr/bin/env python3
"""
WebGL build server with proper gzip header support
"""

import http.server
import socketserver
import os
import sys
from pathlib import Path

class GzipRequestHandler(http.server.SimpleHTTPRequestHandler):
    """HTTP request handler with gzip content-encoding support"""
    
    def end_headers(self):
        # Set proper content type FIRST, then add encoding
        if '.wasm' in self.path:
            self.send_header('Content-Type', 'application/wasm')
        elif '.js' in self.path:
            self.send_header('Content-Type', 'application/javascript')
        elif '.data' in self.path:
            self.send_header('Content-Type', 'application/octet-stream')
        elif '.json' in self.path:
            self.send_header('Content-Type', 'application/json')
        elif '.css' in self.path:
            self.send_header('Content-Type', 'text/css')
            
        # Add Content-Encoding header for gzipped files
        if self.path.endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        
        # Add CORS headers for local development
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        
        # Add cache control
        if not self.path.endswith('.html'):
            self.send_header('Cache-Control', 'public, max-age=3600')
        
        super().end_headers()
    
    def do_OPTIONS(self):
        """Handle CORS preflight requests"""
        self.send_response(200)
        self.end_headers()

def serve(port=8080, directory="Build/Web"):
    """Start the WebGL development server"""
    
    # Change to the build directory
    if os.path.exists(directory):
        os.chdir(directory)
        print(f"Serving from: {os.getcwd()}")
    else:
        print(f"Error: Directory '{directory}' not found!")
        print("Make sure you've built the WebGL project first.")
        sys.exit(1)
    
    # Create server
    with socketserver.TCPServer(("", port), GzipRequestHandler) as httpd:
        print(f"\n🚀 WebGL Build Server")
        print(f"=" * 40)
        print(f"📁 Serving: {directory}")
        print(f"🌐 URL: http://localhost:{port}")
        print(f"=" * 40)
        print(f"\nPress Ctrl+C to stop the server\n")
        
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n\n✋ Server stopped")
            return

if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(description="WebGL build development server")
    parser.add_argument("-p", "--port", type=int, default=8080, help="Port to serve on (default: 8080)")
    parser.add_argument("-d", "--directory", default="Build/Web", help="Directory to serve (default: Build/Web)")
    
    args = parser.parse_args()
    serve(args.port, args.directory)