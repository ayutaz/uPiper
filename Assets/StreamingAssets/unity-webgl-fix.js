// Unity WebGL GitHub Pages Fix
// This script fixes Unity WebGL deployment issues on GitHub Pages

(function() {
    console.log('[Unity WebGL Fix] Initializing GitHub Pages compatibility fixes...');
    
    // Override Unity's loader to handle gzip files correctly
    if (window.createUnityInstance) {
        const originalCreateUnityInstance = window.createUnityInstance;
        
        window.createUnityInstance = function(canvas, config, onProgress) {
            console.log('[Unity WebGL Fix] Patching Unity configuration for GitHub Pages...');
            
            // Modify config to use uncompressed files if available
            if (config.dataUrl && config.dataUrl.endsWith('.gz')) {
                // Try to use uncompressed version first
                const uncompressedUrl = config.dataUrl.replace('.gz', '');
                console.log(`[Unity WebGL Fix] Checking for uncompressed data at: ${uncompressedUrl}`);
                
                // Test if uncompressed version exists
                fetch(uncompressedUrl, { method: 'HEAD' })
                    .then(response => {
                        if (response.ok) {
                            console.log('[Unity WebGL Fix] Found uncompressed data file, using it instead');
                            config.dataUrl = uncompressedUrl;
                        }
                    })
                    .catch(() => {
                        console.log('[Unity WebGL Fix] No uncompressed version found, using gzip');
                    });
            }
            
            if (config.frameworkUrl && config.frameworkUrl.endsWith('.gz')) {
                const uncompressedUrl = config.frameworkUrl.replace('.gz', '');
                console.log(`[Unity WebGL Fix] Checking for uncompressed framework at: ${uncompressedUrl}`);
                
                fetch(uncompressedUrl, { method: 'HEAD' })
                    .then(response => {
                        if (response.ok) {
                            console.log('[Unity WebGL Fix] Found uncompressed framework file, using it instead');
                            config.frameworkUrl = uncompressedUrl;
                        }
                    })
                    .catch(() => {
                        console.log('[Unity WebGL Fix] No uncompressed version found, using gzip');
                    });
            }
            
            if (config.codeUrl && config.codeUrl.endsWith('.gz')) {
                const uncompressedUrl = config.codeUrl.replace('.gz', '');
                console.log(`[Unity WebGL Fix] Checking for uncompressed code at: ${uncompressedUrl}`);
                
                fetch(uncompressedUrl, { method: 'HEAD' })
                    .then(response => {
                        if (response.ok) {
                            console.log('[Unity WebGL Fix] Found uncompressed code file, using it instead');
                            config.codeUrl = uncompressedUrl;
                        }
                    })
                    .catch(() => {
                        console.log('[Unity WebGL Fix] No uncompressed version found, using gzip');
                    });
            }
            
            // Call original function with modified config
            return originalCreateUnityInstance.call(this, canvas, config, onProgress);
        };
    }
    
    // Manual gzip decompression fallback
    window.decompressGzip = async function(compressedData) {
        try {
            const ds = new DecompressionStream('gzip');
            const decompressedStream = new Response(compressedData).body.pipeThrough(ds);
            const decompressedData = await new Response(decompressedStream).arrayBuffer();
            return decompressedData;
        } catch (error) {
            console.error('[Unity WebGL Fix] Failed to decompress gzip data:', error);
            throw error;
        }
    };
    
    console.log('[Unity WebGL Fix] Initialization complete');
})();