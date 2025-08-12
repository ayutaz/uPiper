// Split file loader for GitHub Pages deployment
(function() {
    console.log('[SplitFileLoader] Initializing for OpenJTalk data...');
    
    // Store original fetch immediately
    const originalFetch = window.fetch;
    
    // Override fetch to intercept data file requests
    window.fetch = async function(input, init) {
        const url = typeof input === 'string' ? input : input.url;
        
        // Check if this is a request for OpenJTalk data file
        if (url && (url.includes('openjtalk-unity.data') || url.includes('openjtalk-unity-full.data')) && !url.includes('.part')) {
            console.log('[SplitFileLoader] Intercepting fetch for:', url);
            
            try {
                // Extract base path from URL
                const basePath = url.substring(0, url.lastIndexOf('/'));
                
                // Try to load split files
                console.log('[SplitFileLoader] Loading split files from:', basePath);
                const responses = await Promise.all([
                    originalFetch(`${basePath}/openjtalk-unity.data.partaa`, init),
                    originalFetch(`${basePath}/openjtalk-unity.data.partab`, init)
                ]);
                
                // Check if both files loaded successfully
                if (responses[0].ok && responses[1].ok) {
                    console.log('[SplitFileLoader] Split files loaded successfully');
                    
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
                    
                    // Return as Response
                    return new Response(combined, {
                        status: 200,
                        statusText: 'OK',
                        headers: {
                            'Content-Type': 'application/octet-stream',
                            'Content-Length': totalLength.toString()
                        }
                    });
                } else {
                    console.log('[SplitFileLoader] Split files not found, falling back to original request');
                    return originalFetch(input, init);
                }
            } catch (e) {
                console.error('[SplitFileLoader] Error loading split files:', e);
                return originalFetch(input, init);
            }
        }
        
        // Pass through all other requests
        return originalFetch(input, init);
    };
    
    // Also handle XMLHttpRequest for older code
    const originalXHROpen = XMLHttpRequest.prototype.open;
    const originalXHRSend = XMLHttpRequest.prototype.send;
    
    XMLHttpRequest.prototype.open = function(method, url, ...args) {
        this._url = url;
        this._method = method;
        return originalXHROpen.apply(this, [method, url, ...args]);
    };
    
    XMLHttpRequest.prototype.send = function(...args) {
        const url = this._url;
        
        // Check if this is a request for OpenJTalk data file
        if (url && (url.includes('openjtalk-unity.data') || url.includes('openjtalk-unity-full.data')) && !url.includes('.part')) {
            console.log('[SplitFileLoader] Intercepting XHR for:', url);
            
            // Use fetch to load split files
            const xhr = this;
            const basePath = url.substring(0, url.lastIndexOf('/'));
            
            Promise.all([
                fetch(`${basePath}/openjtalk-unity.data.partaa`),
                fetch(`${basePath}/openjtalk-unity.data.partab`)
            ]).then(async responses => {
                if (responses[0].ok && responses[1].ok) {
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
                    
                    console.log(`[SplitFileLoader] XHR: Combined ${totalLength} bytes`);
                    
                    // Simulate XHR response
                    Object.defineProperty(xhr, 'status', { value: 200 });
                    Object.defineProperty(xhr, 'statusText', { value: 'OK' });
                    Object.defineProperty(xhr, 'response', { value: combined.buffer });
                    Object.defineProperty(xhr, 'responseType', { value: 'arraybuffer' });
                    
                    if (xhr.onload) {
                        xhr.onload({ target: xhr });
                    }
                } else {
                    // Fallback to original request
                    originalXHRSend.apply(xhr, args);
                }
            }).catch(error => {
                console.error('[SplitFileLoader] XHR error:', error);
                originalXHRSend.apply(xhr, args);
            });
        } else {
            return originalXHRSend.apply(this, args);
        }
    };
    
    // Define OpenJTalkModule for compatibility
    window.OpenJTalkModule = window.OpenJTalkModule || {};
    
    console.log('[SplitFileLoader] Ready - fetch and XHR interceptors installed');
})();