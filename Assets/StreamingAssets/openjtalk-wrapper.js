// OpenJTalk WebAssembly Wrapper for Unity WebGL
// Complete path resolution fix

(function() {
    console.log('[OpenJTalkWrapper] Installing custom loader...');
    
    // Store the original fetch
    const originalFetch = window.fetch;
    
    // Override fetch to intercept openjtalk.wasm requests
    window.fetch = function(url, ...args) {
        // Check if this is a request for openjtalk.wasm
        if (typeof url === 'string' && url.includes('openjtalk.wasm')) {
            console.log('[OpenJTalkWrapper] Intercepting WASM fetch:', url);
            // Fix the path - remove any duplicate StreamingAssets
            const fixedUrl = url.replace(/StreamingAssets\/StreamingAssets\//g, 'StreamingAssets/')
                               .replace(/\/StreamingAssets\/openjtalk\.wasm$/, '/StreamingAssets/openjtalk.wasm')
                               .replace(/^.*StreamingAssets\/openjtalk\.wasm/, 'StreamingAssets/openjtalk.wasm');
            console.log('[OpenJTalkWrapper] Fixed WASM URL:', fixedUrl);
            return originalFetch.call(this, fixedUrl, ...args);
        }
        return originalFetch.call(this, url, ...args);
    };
    
    // Create custom module loader
    window.OpenJTalkModule = async function(config) {
        console.log('[OpenJTalkWrapper] Loading OpenJTalk module with custom config');
        
        // Load the original module code
        const response = await fetch('StreamingAssets/openjtalk.js');
        let jsText = await response.text();
        
        console.log('[OpenJTalkWrapper] Original JS size:', jsText.length);
        
        // Apply multiple fixes to ensure all path issues are resolved
        // Fix 1: Replace import.meta.url (minified version might have different patterns)
        jsText = jsText.replace(/import\.meta\.url/gi, '"StreamingAssets/openjtalk.js"');
        
        // Fix 2: Replace various forms of scriptDirectory assignment
        // Pattern 1: scriptDirectory=new URL(".",something).href
        jsText = jsText.replace(/scriptDirectory\s*=\s*new\s+URL\s*\([^)]+\)\.href/gi, 'scriptDirectory="StreamingAssets/"');
        
        // Pattern 2: scriptDirectory=something.href
        jsText = jsText.replace(/scriptDirectory\s*=\s*[^;,\s]+\.href/gi, 'scriptDirectory="StreamingAssets/"');
        
        // Pattern 3: Any remaining scriptDirectory assignments that might cause issues
        jsText = jsText.replace(/scriptDirectory\s*=\s*"[^"]*StreamingAssets\/StreamingAssets[^"]*"/gi, 'scriptDirectory="StreamingAssets/"');
        
        // Fix 3: Fix WASM file loading
        // Pattern 1: new URL("openjtalk.wasm",something).href
        jsText = jsText.replace(/new\s+URL\s*\(\s*["']openjtalk\.wasm["'][^)]*\)\.href/gi, '"StreamingAssets/openjtalk.wasm"');
        
        // Pattern 2: scriptDirectory+"openjtalk.wasm" 
        jsText = jsText.replace(/scriptDirectory\s*\+\s*["']openjtalk\.wasm["']/gi, '"StreamingAssets/openjtalk.wasm"');
        
        // Fix 4: Remove export statement
        jsText = jsText.replace(/export\s+default\s+OpenJTalkModule[;\s]*/gi, '');
        jsText = jsText.replace(/export\s*{\s*OpenJTalkModule\s*as\s*default\s*}[;\s]*/gi, '');
        
        // Fix 5: Add global assignment
        jsText = jsText + '\n; window.__OpenJTalkModuleFunc = OpenJTalkModule;';
        
        console.log('[OpenJTalkWrapper] Applied text replacements');
        
        // Create and execute the script
        const script = document.createElement('script');
        script.textContent = jsText;
        document.head.appendChild(script);
        
        // Wait for script execution
        await new Promise(resolve => setTimeout(resolve, 100));
        
        // Check if module function is available
        if (typeof window.__OpenJTalkModuleFunc !== 'function') {
            console.error('[OpenJTalkWrapper] Module function not found after script execution');
            throw new Error('Failed to load OpenJTalkModule function');
        }
        
        console.log('[OpenJTalkWrapper] Module function loaded, creating instance...');
        
        // Create final config with aggressive path overrides
        const finalConfig = Object.assign({}, config || {}, {
            locateFile: function(path) {
                console.log('[OpenJTalkWrapper] locateFile called for:', path);
                // Always return the correct path for WASM
                if (path === 'openjtalk.wasm' || path.endsWith('.wasm')) {
                    return 'StreamingAssets/openjtalk.wasm';
                }
                return 'StreamingAssets/' + path;
            },
            // Override scriptDirectory if the module tries to use it
            scriptDirectory: 'StreamingAssets/',
            // Logging functions
            print: config?.print || function(text) {
                console.log('[OpenJTalk]', text);
            },
            printErr: config?.printErr || function(text) {
                console.error('[OpenJTalk]', text);
            }
        });
        
        // Call the module function
        console.log('[OpenJTalkWrapper] Initializing module instance...');
        const moduleInstance = await window.__OpenJTalkModuleFunc(finalConfig);
        
        // Clean up
        delete window.__OpenJTalkModuleFunc;
        
        console.log('[OpenJTalkWrapper] Module instance created successfully');
        return moduleInstance;
    };
    
    // Also restore original fetch after a delay to avoid interfering with other code
    setTimeout(() => {
        console.log('[OpenJTalkWrapper] Restoring original fetch');
        window.fetch = originalFetch;
    }, 30000); // 30 seconds should be enough for module loading
    
    console.log('[OpenJTalkWrapper] Wrapper installed successfully');
})();