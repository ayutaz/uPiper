// Unity WebGL Path Resolver
// Centralizes all path resolution for StreamingAssets in Unity WebGL builds

(function() {
    'use strict';
    
    console.log('[Unity Path Resolver] Initializing centralized path resolution system...');
    
    // Detect the correct base path for StreamingAssets
    function detectStreamingAssetsPath() {
        const possiblePaths = [
            'Build/StreamingAssets',
            'StreamingAssets',
            './Build/StreamingAssets',
            './StreamingAssets'
        ];
        
        // Check current URL structure
        const currentPath = window.location.pathname;
        const isGitHubPages = window.location.hostname.includes('github.io');
        
        // For GitHub Pages, prefer Build/StreamingAssets
        if (isGitHubPages) {
            return 'Build/StreamingAssets';
        }
        
        // For local development
        if (window.location.protocol === 'file:') {
            return 'StreamingAssets';
        }
        
        // Default
        return 'Build/StreamingAssets';
    }
    
    const STREAMING_ASSETS_PATH = detectStreamingAssetsPath();
    console.log(`[Unity Path Resolver] Detected StreamingAssets path: ${STREAMING_ASSETS_PATH}`);
    
    // Global path resolver function
    window.UnityPathResolver = {
        streamingAssetsPath: STREAMING_ASSETS_PATH,
        
        // Resolve any StreamingAssets path
        resolve: function(relativePath) {
            // Remove leading slash if present
            if (relativePath.startsWith('/')) {
                relativePath = relativePath.substring(1);
            }
            
            // Remove StreamingAssets prefix if already present
            if (relativePath.startsWith('StreamingAssets/')) {
                relativePath = relativePath.substring('StreamingAssets/'.length);
            }
            
            // Build full path
            const fullPath = `${STREAMING_ASSETS_PATH}/${relativePath}`;
            console.log(`[Unity Path Resolver] Resolved: ${relativePath} -> ${fullPath}`);
            return fullPath;
        },
        
        // Get absolute URL for a StreamingAssets file
        getAbsoluteURL: function(relativePath) {
            const resolved = this.resolve(relativePath);
            const base = window.location.origin + window.location.pathname.replace(/\/[^\/]*$/, '');
            return `${base}/${resolved}`;
        }
    };
    
    // Patch OpenJTalk paths
    if (window.OpenJTalkUnityModule) {
        console.log('[Unity Path Resolver] Patching OpenJTalk module paths...');
        const originalLocateFile = window.OpenJTalkUnityModule.locateFile;
        window.OpenJTalkUnityModule.locateFile = function(path) {
            if (path === 'openjtalk-unity.data') {
                const resolvedPath = window.UnityPathResolver.resolve('openjtalk-unity.data');
                console.log(`[Unity Path Resolver] OpenJTalk data: ${path} -> ${resolvedPath}`);
                return resolvedPath;
            }
            return originalLocateFile ? originalLocateFile.call(this, path) : path;
        };
    }
    
    // Patch ONNX Runtime paths
    const originalFetch = window.fetch;
    window.fetch = function(input, init) {
        if (typeof input === 'string') {
            // Check if this is a StreamingAssets request
            if (input.includes('StreamingAssets') && !input.includes('Build/StreamingAssets')) {
                // Extract the file path after StreamingAssets
                const match = input.match(/StreamingAssets\/(.+)/);
                if (match) {
                    const originalPath = input;
                    input = window.UnityPathResolver.getAbsoluteURL(match[1]);
                    console.log(`[Unity Path Resolver] Redirected fetch: ${originalPath} -> ${input}`);
                }
            }
        }
        return originalFetch.call(this, input, init);
    };
    
    console.log('[Unity Path Resolver] Path resolution system ready');
})();