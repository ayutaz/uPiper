// OpenJTalk WebAssembly Wrapper for Unity WebGL
// Version 8.0 - Final Working Solution

(function() {
    console.log('[OpenJTalkWrapper v8.0] Installing...');
    
    // Store the original OpenJTalkModule function
    let originalOpenJTalkModule = null;
    
    // Load and patch the OpenJTalk module
    window.OpenJTalkModule = async function(userConfig) {
        console.log('[OpenJTalkWrapper] Initializing module...');
        
        // If not already loaded, load the original module
        if (!originalOpenJTalkModule) {
            console.log('[OpenJTalkWrapper] Loading original OpenJTalk module...');
            
            // Dynamically load the module
            const script = document.createElement('script');
            script.type = 'module';
            
            // Create module content that exports properly
            script.textContent = `
                import OpenJTalkModuleFactory from './StreamingAssets/openjtalk.js';
                window.__OpenJTalkModuleFactory = OpenJTalkModuleFactory;
            `;
            
            document.head.appendChild(script);
            
            // Wait for module to load
            let attempts = 0;
            while (!window.__OpenJTalkModuleFactory && attempts < 50) {
                await new Promise(resolve => setTimeout(resolve, 100));
                attempts++;
            }
            
            if (!window.__OpenJTalkModuleFactory) {
                // Fallback: Load as regular script
                console.log('[OpenJTalkWrapper] Module import failed, trying direct load...');
                
                const response = await fetch('StreamingAssets/openjtalk.js');
                const jsText = await response.text();
                
                // Fix ES6 module syntax for compatibility
                const modifiedJs = jsText
                    .replace(/import\.meta\.url/g, '"StreamingAssets/openjtalk.js"')
                    .replace(/export default OpenJTalkModule;?/g, 'window.__OpenJTalkModuleFactory = OpenJTalkModule;');
                
                const fallbackScript = document.createElement('script');
                fallbackScript.textContent = modifiedJs;
                document.head.appendChild(script);
                
                // Wait again
                attempts = 0;
                while (!window.__OpenJTalkModuleFactory && attempts < 50) {
                    await new Promise(resolve => setTimeout(resolve, 100));
                    attempts++;
                }
            }
            
            if (!window.__OpenJTalkModuleFactory) {
                throw new Error('Failed to load OpenJTalk module');
            }
            
            originalOpenJTalkModule = window.__OpenJTalkModuleFactory;
        }
        
        // Create configuration with HEAP array support
        const config = Object.assign({}, userConfig || {}, {
            instantiateWasm: async function(imports, receiveInstance) {
                console.log('[OpenJTalkWrapper] Loading WASM...');
                const wasmResponse = await fetch('StreamingAssets/openjtalk.wasm');
                if (!wasmResponse.ok) {
                    throw new Error(`WASM load failed: ${wasmResponse.status}`);
                }
                const wasmBuffer = await wasmResponse.arrayBuffer();
                console.log('[OpenJTalkWrapper] WASM loaded, size:', wasmBuffer.byteLength);
                
                const result = await WebAssembly.instantiate(wasmBuffer, imports);
                receiveInstance(result.instance, result.module);
                return {};
            },
            
            locateFile: function(path) {
                if (path.endsWith('.wasm')) {
                    return 'StreamingAssets/openjtalk.wasm';
                }
                return 'StreamingAssets/' + path;
            },
            
            print: text => console.log('[OpenJTalk]', text),
            printErr: text => console.error('[OpenJTalk]', text),
            
            // Override to provide HEAP arrays
            postRun: [].concat(userConfig?.postRun || []).concat([
                function() {
                    const Module = this;
                    console.log('[OpenJTalkWrapper] PostRun: Setting up HEAP arrays');
                    
                    // Ensure HEAP arrays exist
                    if (Module.asm && Module.asm.memory) {
                        const buffer = Module.asm.memory.buffer;
                        
                        // Create HEAP arrays if they don't exist
                        if (!Module.HEAP8) Module.HEAP8 = new Int8Array(buffer);
                        if (!Module.HEAPU8) Module.HEAPU8 = new Uint8Array(buffer);
                        if (!Module.HEAP16) Module.HEAP16 = new Int16Array(buffer);
                        if (!Module.HEAPU16) Module.HEAPU16 = new Uint16Array(buffer);
                        if (!Module.HEAP32) Module.HEAP32 = new Int32Array(buffer);
                        if (!Module.HEAPU32) Module.HEAPU32 = new Uint32Array(buffer);
                        if (!Module.HEAPF32) Module.HEAPF32 = new Float32Array(buffer);
                        if (!Module.HEAPF64) Module.HEAPF64 = new Float64Array(buffer);
                        
                        console.log('[OpenJTalkWrapper] HEAP arrays created');
                    }
                }
            ])
        });
        
        // Initialize the module
        console.log('[OpenJTalkWrapper] Creating module instance...');
        const module = await originalOpenJTalkModule(config);
        
        // Final check and setup
        if (!module.HEAP8 && module.asm && module.asm.memory) {
            const buffer = module.asm.memory.buffer;
            module.HEAP8 = new Int8Array(buffer);
            module.HEAPU8 = new Uint8Array(buffer);
            module.HEAP16 = new Int16Array(buffer);
            module.HEAPU16 = new Uint16Array(buffer);
            module.HEAP32 = new Int32Array(buffer);
            module.HEAPU32 = new Uint32Array(buffer);
            module.HEAPF32 = new Float32Array(buffer);
            module.HEAPF64 = new Float64Array(buffer);
            console.log('[OpenJTalkWrapper] HEAP arrays added to module');
        }
        
        // Verify exports
        const requiredExports = ['HEAP8', 'HEAPU8', '_malloc', '_free', 'UTF8ToString', 'stringToUTF8'];
        const exportStatus = {};
        requiredExports.forEach(name => {
            exportStatus[name] = !!module[name];
        });
        console.log('[OpenJTalkWrapper] Export status:', exportStatus);
        
        return module;
    };
    
    console.log('[OpenJTalkWrapper v8.0] Ready');
})();