// OpenJTalk WebAssembly Wrapper for Unity WebGL - Fixed Version
// Resolves HEAP8 export issue and uses wasmExports instead of asm

(function() {
    console.log('[OpenJTalkWrapper Fixed] Installing...');
    
    window.OpenJTalkModule = async function(userConfig) {
        console.log('[OpenJTalkWrapper] Loading module');
        
        try {
            // Load the original OpenJTalk module
            const response = await fetch('StreamingAssets/openjtalk.js');
            let jsText = await response.text();
            
            console.log('[OpenJTalkWrapper] JS loaded, size:', jsText.length);
            
            // Fix import.meta.url
            jsText = jsText.replace(/import\.meta\.url/g, '"StreamingAssets/openjtalk.js"');
            jsText = jsText.replace(/export default OpenJTalkModule;/g, '');
            
            // Fix the HEAP8 export issue by modifying the unexportedRuntimeSymbol function
            // This function throws the error when HEAP8 is not in EXPORTED_RUNTIME_METHODS
            jsText = jsText.replace(
                /unexportedRuntimeSymbol\(sym\)/g,
                '(function(sym) { ' +
                    'if (sym.startsWith("HEAP")) { ' +
                        'if (!Module[sym] && Module.wasmExports && Module.wasmExports.memory) { ' +
                            'const buffer = Module.wasmExports.memory.buffer; ' +
                            'Module.HEAP8 = new Int8Array(buffer); ' +
                            'Module.HEAPU8 = new Uint8Array(buffer); ' +
                            'Module.HEAP16 = new Int16Array(buffer); ' +
                            'Module.HEAPU16 = new Uint16Array(buffer); ' +
                            'Module.HEAP32 = new Int32Array(buffer); ' +
                            'Module.HEAPU32 = new Uint32Array(buffer); ' +
                            'Module.HEAPF32 = new Float32Array(buffer); ' +
                            'Module.HEAPF64 = new Float64Array(buffer); ' +
                            'console.log("[OpenJTalkWrapper] Created HEAP arrays from wasmExports.memory"); ' +
                        '} ' +
                        'return Module[sym]; ' +
                    '} ' +
                    'return unexportedRuntimeSymbol(sym); ' +
                '})(sym)'
            );
            
            // Also fix references to Module.asm to use Module.wasmExports
            jsText = jsText.replace(/Module\["asm"\]/g, 'Module["wasmExports"]');
            jsText = jsText.replace(/Module\.asm/g, 'Module.wasmExports');
            
            // Wrap the module code
            const wrappedJs = `
                (function() {
                    console.log('[OpenJTalkWrapper] Executing patched module');
                    
                    // Save original unexportedRuntimeSymbol if it exists
                    var originalUnexportedRuntimeSymbol = typeof unexportedRuntimeSymbol !== 'undefined' ? unexportedRuntimeSymbol : null;
                    
                    // Create our override
                    window.unexportedRuntimeSymbol = function(sym) {
                        console.log('[OpenJTalkWrapper] unexportedRuntimeSymbol called for:', sym);
                        
                        // Handle HEAP arrays specially
                        if (sym.startsWith('HEAP')) {
                            // Try to get from Module first
                            if (typeof Module !== 'undefined' && Module[sym]) {
                                return Module[sym];
                            }
                            
                            // Try to create from wasmExports.memory
                            if (typeof Module !== 'undefined' && Module.wasmExports && Module.wasmExports.memory) {
                                const buffer = Module.wasmExports.memory.buffer;
                                if (!Module.HEAP8) {
                                    Module.HEAP8 = new Int8Array(buffer);
                                    Module.HEAPU8 = new Uint8Array(buffer);
                                    Module.HEAP16 = new Int16Array(buffer);
                                    Module.HEAPU16 = new Uint16Array(buffer);
                                    Module.HEAP32 = new Int32Array(buffer);
                                    Module.HEAPU32 = new Uint32Array(buffer);
                                    Module.HEAPF32 = new Float32Array(buffer);
                                    Module.HEAPF64 = new Float64Array(buffer);
                                    console.log('[OpenJTalkWrapper] Created HEAP arrays from wasmExports.memory');
                                }
                                return Module[sym];
                            }
                            
                            // Try legacy asm
                            if (typeof Module !== 'undefined' && Module.asm && Module.asm.memory) {
                                const buffer = Module.asm.memory.buffer;
                                if (!Module.HEAP8) {
                                    Module.HEAP8 = new Int8Array(buffer);
                                    Module.HEAPU8 = new Uint8Array(buffer);
                                    Module.HEAP16 = new Int16Array(buffer);
                                    Module.HEAPU16 = new Uint16Array(buffer);
                                    Module.HEAP32 = new Int32Array(buffer);
                                    Module.HEAPU32 = new Uint32Array(buffer);
                                    Module.HEAPF32 = new Float32Array(buffer);
                                    Module.HEAPF64 = new Float64Array(buffer);
                                    console.log('[OpenJTalkWrapper] Created HEAP arrays from asm.memory');
                                }
                                return Module[sym];
                            }
                            
                            console.warn('[OpenJTalkWrapper] Could not create HEAP array:', sym);
                            // Return empty array to prevent crash
                            return new Uint8Array(0);
                        }
                        
                        // For non-HEAP symbols, call original if it exists
                        if (originalUnexportedRuntimeSymbol) {
                            return originalUnexportedRuntimeSymbol(sym);
                        }
                        
                        console.warn('[OpenJTalkWrapper] Symbol not found:', sym);
                        return undefined;
                    };
                    
                    ${jsText}
                    
                    // Store the module function
                    if (typeof OpenJTalkModule !== 'undefined') {
                        window.__OpenJTalkModuleFunc = OpenJTalkModule;
                    }
                })();
            `;
            
            // Execute the wrapped module
            const script = document.createElement('script');
            script.textContent = wrappedJs;
            document.head.appendChild(script);
            
            // Wait for module to be available
            await new Promise(resolve => setTimeout(resolve, 100));
            
            if (typeof window.__OpenJTalkModuleFunc !== 'function') {
                throw new Error('Module function not found after script execution');
            }
            
            console.log('[OpenJTalkWrapper] Module function ready');
            
            // Create config with HEAP array support
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
                
                // Override to ensure HEAP arrays are created after WASM loads
                postRun: [].concat(userConfig?.postRun || []).concat([
                    function() {
                        console.log('[OpenJTalkWrapper] PostRun: Ensuring HEAP arrays');
                        const Module = this;
                        
                        // Check for wasmExports first (newer Emscripten)
                        let memory = null;
                        if (Module.wasmExports && Module.wasmExports.memory) {
                            memory = Module.wasmExports.memory;
                            console.log('[OpenJTalkWrapper] Using wasmExports.memory');
                        } else if (Module.asm && Module.asm.memory) {
                            memory = Module.asm.memory;
                            console.log('[OpenJTalkWrapper] Using asm.memory (legacy)');
                        } else if (Module.wasmMemory) {
                            memory = Module.wasmMemory;
                            console.log('[OpenJTalkWrapper] Using wasmMemory');
                        }
                        
                        if (memory && !Module.HEAP8) {
                            const buffer = memory.buffer;
                            Module.HEAP8 = new Int8Array(buffer);
                            Module.HEAPU8 = new Uint8Array(buffer);
                            Module.HEAP16 = new Int16Array(buffer);
                            Module.HEAPU16 = new Uint16Array(buffer);
                            Module.HEAP32 = new Int32Array(buffer);
                            Module.HEAPU32 = new Uint32Array(buffer);
                            Module.HEAPF32 = new Float32Array(buffer);
                            Module.HEAPF64 = new Float64Array(buffer);
                            console.log('[OpenJTalkWrapper] HEAP arrays created in postRun');
                        }
                    }
                ]),
                
                // Also try preRun to set up HEAP arrays early
                preRun: [].concat(userConfig?.preRun || []).concat([
                    function() {
                        console.log('[OpenJTalkWrapper] PreRun: Setting up environment');
                        const Module = this;
                        
                        // Override updateMemoryViews if it exists
                        const originalUpdateMemoryViews = Module.updateMemoryViews;
                        Module.updateMemoryViews = function() {
                            console.log('[OpenJTalkWrapper] updateMemoryViews called');
                            
                            // Call original if it exists
                            if (originalUpdateMemoryViews) {
                                originalUpdateMemoryViews.call(this);
                            }
                            
                            // Ensure HEAP arrays exist
                            let memory = Module.wasmExports?.memory || Module.asm?.memory || Module.wasmMemory;
                            if (memory && !Module.HEAP8) {
                                const buffer = memory.buffer;
                                Module.HEAP8 = new Int8Array(buffer);
                                Module.HEAPU8 = new Uint8Array(buffer);
                                Module.HEAP16 = new Int16Array(buffer);
                                Module.HEAPU16 = new Uint16Array(buffer);
                                Module.HEAP32 = new Int32Array(buffer);
                                Module.HEAPU32 = new Uint32Array(buffer);
                                Module.HEAPF32 = new Float32Array(buffer);
                                Module.HEAPF64 = new Float64Array(buffer);
                                console.log('[OpenJTalkWrapper] HEAP arrays created in updateMemoryViews');
                            }
                        };
                    }
                ])
            });
            
            // Initialize the module
            console.log('[OpenJTalkWrapper] Initializing module...');
            const module = await window.__OpenJTalkModuleFunc(config);
            
            // Final check for HEAP arrays
            if (!module.HEAP8) {
                let memory = module.wasmExports?.memory || module.asm?.memory || module.wasmMemory;
                if (memory) {
                    const buffer = memory.buffer;
                    module.HEAP8 = new Int8Array(buffer);
                    module.HEAPU8 = new Uint8Array(buffer);
                    module.HEAP16 = new Int16Array(buffer);
                    module.HEAPU16 = new Uint16Array(buffer);
                    module.HEAP32 = new Int32Array(buffer);
                    module.HEAPU32 = new Uint32Array(buffer);
                    module.HEAPF32 = new Float32Array(buffer);
                    module.HEAPF64 = new Float64Array(buffer);
                    console.log('[OpenJTalkWrapper] HEAP arrays created after init');
                }
            }
            
            // Verify exports
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
    
    console.log('[OpenJTalkWrapper Fixed] Ready');
})();