// OpenJTalk WebAssembly Wrapper for Unity WebGL
// Complete rewrite using instantiateWasm approach

(function() {
    console.log('[OpenJTalkWrapper] Installing complete override...');
    
    window.OpenJTalkModule = async function(userConfig) {
        console.log('[OpenJTalkWrapper] Creating OpenJTalk module with override');
        
        // First, load and modify the JS
        const response = await fetch('StreamingAssets/openjtalk.js');
        let jsText = await response.text();
        
        // Critical fix: Replace the problematic patterns in the minified code
        // These patterns are exact matches from the minified version
        jsText = jsText.replace(
            'var _scriptName=import.meta.url;',
            'var _scriptName="StreamingAssets/openjtalk.js";'
        );
        
        jsText = jsText.replace(
            'scriptDirectory=new URL(".",_scriptName).href',
            'scriptDirectory="StreamingAssets/"'
        );
        
        jsText = jsText.replace(
            'return new URL("openjtalk.wasm",import.meta.url).href',
            'return "StreamingAssets/openjtalk.wasm"'
        );
        
        // Remove export statement
        jsText = jsText.replace('export default OpenJTalkModule;', '');
        
        // Make function available globally
        jsText += '\n; window.__OpenJTalkModuleFunc = OpenJTalkModule;';
        
        console.log('[OpenJTalkWrapper] Code modifications applied');
        
        // Execute the modified code
        const script = document.createElement('script');
        script.textContent = jsText;
        document.head.appendChild(script);
        
        // Wait for execution
        await new Promise(resolve => setTimeout(resolve, 100));
        
        if (typeof window.__OpenJTalkModuleFunc !== 'function') {
            throw new Error('[OpenJTalkWrapper] Failed to load module function');
        }
        
        // Create config with instantiateWasm override
        const config = Object.assign({}, userConfig || {}, {
            // Override the WebAssembly instantiation completely
            instantiateWasm: async function(imports, receiveInstance) {
                console.log('[OpenJTalkWrapper] Custom instantiateWasm called');
                
                try {
                    // Fetch the WASM file directly with the correct path
                    const wasmResponse = await fetch('StreamingAssets/openjtalk.wasm');
                    if (!wasmResponse.ok) {
                        throw new Error(`Failed to fetch WASM: ${wasmResponse.status}`);
                    }
                    
                    const wasmArrayBuffer = await wasmResponse.arrayBuffer();
                    console.log('[OpenJTalkWrapper] WASM loaded, size:', wasmArrayBuffer.byteLength);
                    
                    // Instantiate the WebAssembly module
                    const wasmInstance = await WebAssembly.instantiate(wasmArrayBuffer, imports);
                    
                    console.log('[OpenJTalkWrapper] WASM instantiated successfully');
                    
                    // Call the callback with the instance
                    receiveInstance(wasmInstance.instance, wasmInstance.module);
                    
                    return {}; // Return empty object as expected by Emscripten
                } catch (error) {
                    console.error('[OpenJTalkWrapper] WASM instantiation failed:', error);
                    throw error;
                }
            },
            
            // Also override locateFile just in case
            locateFile: function(path) {
                console.log('[OpenJTalkWrapper] locateFile:', path);
                if (path.endsWith('.wasm')) {
                    return 'StreamingAssets/openjtalk.wasm';
                }
                return 'StreamingAssets/' + path;
            },
            
            print: userConfig?.print || function(text) {
                console.log('[OpenJTalk]', text);
            },
            
            printErr: userConfig?.printErr || function(text) {
                console.error('[OpenJTalk]', text);
            }
        });
        
        // Initialize the module
        console.log('[OpenJTalkWrapper] Initializing module with custom config');
        const module = await window.__OpenJTalkModuleFunc(config);
        
        // Clean up
        delete window.__OpenJTalkModuleFunc;
        
        console.log('[OpenJTalkWrapper] Module created successfully');
        return module;
    };
    
    console.log('[OpenJTalkWrapper] Override installed');
})();