// OpenJTalk WebAssembly Wrapper for Unity WebGL
// Version 3.0 - Simplified approach with minimal modifications

(function() {
    console.log('[OpenJTalkWrapper v3.0] Installing...');
    
    window.OpenJTalkModule = async function(userConfig) {
        console.log('[OpenJTalkWrapper] Loading module');
        
        try {
            // Load the original JS
            const response = await fetch('StreamingAssets/openjtalk.js');
            const jsText = await response.text();
            
            console.log('[OpenJTalkWrapper] JS loaded, size:', jsText.length);
            
            // Create a modified version with minimal changes
            // We'll wrap it and override critical functions
            const modifiedJs = `
                (function() {
                    // Store original functions
                    var originalDefineProperty = Object.defineProperty;
                    
                    // Override Object.defineProperty to intercept HEAP array access
                    Object.defineProperty = function(obj, prop, descriptor) {
                        // Intercept HEAP array property definitions
                        if (prop && prop.startsWith && prop.startsWith('HEAP')) {
                            console.log('[OpenJTalkWrapper] Intercepting HEAP property:', prop);
                            // Create a getter that provides the HEAP array
                            descriptor = {
                                configurable: true,
                                enumerable: true,
                                get: function() {
                                    // Try to return the actual HEAP array
                                    if (prop === 'HEAP8' && typeof HEAP8 !== 'undefined') return HEAP8;
                                    if (prop === 'HEAPU8' && typeof HEAPU8 !== 'undefined') return HEAPU8;
                                    if (prop === 'HEAP16' && typeof HEAP16 !== 'undefined') return HEAP16;
                                    if (prop === 'HEAPU16' && typeof HEAPU16 !== 'undefined') return HEAPU16;
                                    if (prop === 'HEAP32' && typeof HEAP32 !== 'undefined') return HEAP32;
                                    if (prop === 'HEAPU32' && typeof HEAPU32 !== 'undefined') return HEAPU32;
                                    if (prop === 'HEAPF32' && typeof HEAPF32 !== 'undefined') return HEAPF32;
                                    if (prop === 'HEAPF64' && typeof HEAPF64 !== 'undefined') return HEAPF64;
                                    // Fallback
                                    console.warn('[OpenJTalkWrapper] HEAP array not ready:', prop);
                                    return new Uint8Array(0);
                                }
                            };
                        }
                        return originalDefineProperty.call(this, obj, prop, descriptor);
                    };
                    
                    // Fix import.meta.url before the module code runs
                    var import_meta_url = "StreamingAssets/openjtalk.js";
                    
                    // Original module code with simple replacements
                    ${jsText
                        .replace('import.meta.url', 'import_meta_url')
                        .replace('export default OpenJTalkModule;', '')
                    }
                    
                    // Make the module function available
                    window.__OpenJTalkModuleFunc = OpenJTalkModule;
                    
                    // Restore original Object.defineProperty
                    Object.defineProperty = originalDefineProperty;
                })();
            `;
            
            // Execute the modified code
            const script = document.createElement('script');
            script.textContent = modifiedJs;
            document.head.appendChild(script);
            
            // Wait for execution
            await new Promise(resolve => setTimeout(resolve, 100));
            
            if (typeof window.__OpenJTalkModuleFunc !== 'function') {
                throw new Error('Module function not found');
            }
            
            console.log('[OpenJTalkWrapper] Module function ready');
            
            // Create config with our overrides
            const config = Object.assign({}, userConfig || {}, {
                instantiateWasm: async function(imports, receiveInstance) {
                    console.log('[OpenJTalkWrapper] Loading WASM...');
                    const wasmResponse = await fetch('StreamingAssets/openjtalk.wasm');
                    if (!wasmResponse.ok) {
                        throw new Error(`WASM failed: ${wasmResponse.status}`);
                    }
                    const wasmBuffer = await wasmResponse.arrayBuffer();
                    console.log('[OpenJTalkWrapper] WASM size:', wasmBuffer.byteLength);
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
                
                // Add postRun to export HEAP arrays
                postRun: [].concat(userConfig?.postRun || []).concat([
                    function() {
                        console.log('[OpenJTalkWrapper] PostRun: Exporting HEAP arrays');
                        // Export HEAP arrays from internal scope to Module
                        if (typeof HEAP8 !== 'undefined' && !Module.HEAP8) Module.HEAP8 = HEAP8;
                        if (typeof HEAPU8 !== 'undefined' && !Module.HEAPU8) Module.HEAPU8 = HEAPU8;
                        if (typeof HEAP16 !== 'undefined' && !Module.HEAP16) Module.HEAP16 = HEAP16;
                        if (typeof HEAPU16 !== 'undefined' && !Module.HEAPU16) Module.HEAPU16 = HEAPU16;
                        if (typeof HEAP32 !== 'undefined' && !Module.HEAP32) Module.HEAP32 = HEAP32;
                        if (typeof HEAPU32 !== 'undefined' && !Module.HEAPU32) Module.HEAPU32 = HEAPU32;
                        if (typeof HEAPF32 !== 'undefined' && !Module.HEAPF32) Module.HEAPF32 = HEAPF32;
                        if (typeof HEAPF64 !== 'undefined' && !Module.HEAPF64) Module.HEAPF64 = HEAPF64;
                    }
                ])
            });
            
            // Initialize the module
            console.log('[OpenJTalkWrapper] Initializing...');
            const module = await window.__OpenJTalkModuleFunc(config);
            
            // Check exports
            const exports = ['HEAP8', 'HEAPU8', '_malloc', '_free', 'UTF8ToString', 'stringToUTF8'];
            const status = {};
            exports.forEach(name => {
                status[name] = !!module[name];
            });
            console.log('[OpenJTalkWrapper] Export status:', status);
            
            // Clean up
            delete window.__OpenJTalkModuleFunc;
            
            return module;
            
        } catch (error) {
            console.error('[OpenJTalkWrapper] Error:', error);
            throw error;
        }
    };
    
    console.log('[OpenJTalkWrapper v3.0] Ready');
})();