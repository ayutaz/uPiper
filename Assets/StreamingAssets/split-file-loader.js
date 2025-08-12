// Split file loader for GitHub Pages deployment
(function() {
    console.log('[SplitFileLoader] Initializing for OpenJTalk data...');
    
    // Override OpenJTalk module locateFile
    if (window.OpenJTalkUnityModule) {
        const originalLocateFile = window.OpenJTalkUnityModule.locateFile;
        
        window.OpenJTalkUnityModule.locateFile = async function(path) {
            if (path === 'openjtalk-unity.data') {
                console.log('[SplitFileLoader] Loading split OpenJTalk data files...');
                
                try {
                    // Load split files
                    const basePath = window.UnityPathResolver ? 
                        window.UnityPathResolver.resolve('') : 
                        'StreamingAssets';
                    
                    // Try to load partaa and partab
                    const responses = await Promise.all([
                        fetch(`${basePath}/openjtalk-unity.data.partaa`),
                        fetch(`${basePath}/openjtalk-unity.data.partab`)
                    ]);
                    
                    if (!responses[0].ok || !responses[1].ok) {
                        console.log('[SplitFileLoader] Split files not found, trying original file');
                        return originalLocateFile ? originalLocateFile.call(this, path) : path;
                    }
                    
                    // Get data from both parts
                    const buffers = await Promise.all(
                        responses.map(r => r.arrayBuffer())
                    );
                    
                    // Combine buffers
                    const totalLength = buffers.reduce((sum, buf) => sum + buf.byteLength, 0);
                    const combined = new Uint8Array(totalLength);
                    let offset = 0;
                    
                    for (const buffer of buffers) {
                        combined.set(new Uint8Array(buffer), offset);
                        offset += buffer.byteLength;
                    }
                    
                    console.log(`[SplitFileLoader] Combined ${totalLength} bytes of OpenJTalk data`);
                    
                    // Create blob URL
                    const blob = new Blob([combined]);
                    const url = URL.createObjectURL(blob);
                    
                    // Clean up after module loads
                    setTimeout(() => URL.revokeObjectURL(url), 60000);
                    
                    return url;
                } catch (e) {
                    console.error('[SplitFileLoader] Error loading split files:', e);
                    return originalLocateFile ? originalLocateFile.call(this, path) : path;
                }
            }
            
            return originalLocateFile ? originalLocateFile.call(this, path) : path;
        };
    }
    
    // Also patch fetch for direct requests
    const originalFetch = window.fetch;
    window.fetch = async function(input, init) {
        if (typeof input === 'string' && input.includes('openjtalk-unity.data') && !input.includes('.part')) {
            console.log('[SplitFileLoader] Intercepting direct fetch for OpenJTalk data');
            
            try {
                const basePath = input.substring(0, input.lastIndexOf('/'));
                
                // Load split files
                const responses = await Promise.all([
                    originalFetch(`${basePath}/openjtalk-unity.data.partaa`, init),
                    originalFetch(`${basePath}/openjtalk-unity.data.partab`, init)
                ]);
                
                if (!responses[0].ok || !responses[1].ok) {
                    // Fallback to original request
                    return originalFetch(input, init);
                }
                
                // Combine responses
                const buffers = await Promise.all(
                    responses.map(r => r.arrayBuffer())
                );
                
                const totalLength = buffers.reduce((sum, buf) => sum + buf.byteLength, 0);
                const combined = new Uint8Array(totalLength);
                let offset = 0;
                
                for (const buffer of buffers) {
                    combined.set(new Uint8Array(buffer), offset);
                    offset += buffer.byteLength;
                }
                
                console.log(`[SplitFileLoader] Combined ${totalLength} bytes via fetch`);
                
                // Return as Response
                return new Response(combined, {
                    status: 200,
                    statusText: 'OK',
                    headers: {
                        'Content-Type': 'application/octet-stream',
                        'Content-Length': totalLength.toString()
                    }
                });
            } catch (e) {
                console.error('[SplitFileLoader] Fetch intercept failed:', e);
                return originalFetch(input, init);
            }
        }
        
        return originalFetch(input, init);
    };
    
    console.log('[SplitFileLoader] Ready for split file loading');
})();