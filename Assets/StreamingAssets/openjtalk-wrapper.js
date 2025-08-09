// OpenJTalk WebAssembly Wrapper for Unity WebGL
// Fixes missing HEAP arrays export issue in piper-plus build

(function() {
    console.log('[OpenJTalkWrapper] Installing wrapper with HEAP arrays fix...');
    
    // Store original Module properties that might be overwritten
    const originalModule = window.Module || {};
    
    window.OpenJTalkModule = async function(userConfig) {
        console.log('[OpenJTalkWrapper] Loading OpenJTalk with HEAP arrays workaround');
        
        // Load and modify the JS
        const response = await fetch('StreamingAssets/openjtalk.js');
        let jsText = await response.text();
        
        // Fix import.meta.url and path issues
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
        
        // CRITICAL FIX: Modify the code to export HEAP arrays
        // Find the updateMemoryViews function and add exports after it
        jsText = jsText.replace(
            'function updateMemoryViews(){var b=wasmMemory.buffer;HEAP8=new Int8Array(b);HEAP16=new Int16Array(b);HEAPU8=new Uint8Array(b);HEAPU16=new Uint16Array(b);HEAP32=new Int32Array(b);HEAPU32=new Uint32Array(b);HEAPF32=new Float32Array(b);HEAPF64=new Float64Array(b);HEAP64=new BigInt64Array(b);HEAPU64=new BigUint64Array(b)}',
            `function updateMemoryViews(){
                var b=wasmMemory.buffer;
                HEAP8=new Int8Array(b);
                HEAP16=new Int16Array(b);
                HEAPU8=new Uint8Array(b);
                HEAPU16=new Uint16Array(b);
                HEAP32=new Int32Array(b);
                HEAPU32=new Uint32Array(b);
                HEAPF32=new Float32Array(b);
                HEAPF64=new Float64Array(b);
                HEAP64=new BigInt64Array(b);
                HEAPU64=new BigUint64Array(b);
                // Export HEAP arrays to Module
                Module.HEAP8=HEAP8;
                Module.HEAPU8=HEAPU8;
                Module.HEAP16=HEAP16;
                Module.HEAPU16=HEAPU16;
                Module.HEAP32=HEAP32;
                Module.HEAPU32=HEAPU32;
                Module.HEAPF32=HEAPF32;
                Module.HEAPF64=HEAPF64;
            }`
        );
        
        // Also add HEAP exports at the end of the module initialization
        jsText = jsText.replace(
            'return wasmExports}',
            `// Export HEAP arrays
            Module.HEAP8=HEAP8;
            Module.HEAPU8=HEAPU8;
            Module.HEAP16=HEAP16;
            Module.HEAPU16=HEAPU16;
            Module.HEAP32=HEAP32;
            Module.HEAPU32=HEAPU32;
            Module.HEAPF32=HEAPF32;
            Module.HEAPF64=HEAPF64;
            return wasmExports}`
        );
        
        // Make function available globally
        jsText += '\n; window.__OpenJTalkModuleFunc = OpenJTalkModule;';
        
        console.log('[OpenJTalkWrapper] Code modifications applied');
        
        // Execute the modified code
        const script = document.createElement('script');
        script.textContent = jsText;
        document.head.appendChild(script);
        
        await new Promise(resolve => setTimeout(resolve, 100));
        
        if (typeof window.__OpenJTalkModuleFunc !== 'function') {
            throw new Error('[OpenJTalkWrapper] Failed to load module function');
        }
        
        // Create config with instantiateWasm override
        const config = Object.assign({}, userConfig || {}, {
            instantiateWasm: async function(imports, receiveInstance) {
                console.log('[OpenJTalkWrapper] Custom instantiateWasm called');
                
                try {
                    const wasmResponse = await fetch('StreamingAssets/openjtalk.wasm');
                    if (!wasmResponse.ok) {
                        throw new Error(`Failed to fetch WASM: ${wasmResponse.status}`);
                    }
                    
                    const wasmArrayBuffer = await wasmResponse.arrayBuffer();
                    console.log('[OpenJTalkWrapper] WASM loaded, size:', wasmArrayBuffer.byteLength);
                    
                    const wasmInstance = await WebAssembly.instantiate(wasmArrayBuffer, imports);
                    console.log('[OpenJTalkWrapper] WASM instantiated');
                    
                    receiveInstance(wasmInstance.instance, wasmInstance.module);
                    return {};
                } catch (error) {
                    console.error('[OpenJTalkWrapper] WASM error:', error);
                    throw error;
                }
            },
            
            locateFile: function(path) {
                if (path.endsWith('.wasm')) {
                    return 'StreamingAssets/openjtalk.wasm';
                }
                return 'StreamingAssets/' + path;
            },
            
            print: userConfig?.print || console.log.bind(console),
            printErr: userConfig?.printErr || console.error.bind(console)
        });
        
        console.log('[OpenJTalkWrapper] Initializing module...');
        const module = await window.__OpenJTalkModuleFunc(config);
        
        // Double-check HEAP arrays are available
        if (!module.HEAP8) {
            console.error('[OpenJTalkWrapper] HEAP8 still not available! Creating manually...');
            // As a last resort, get wasmMemory and create HEAP views
            if (module.wasmMemory || module.asm?.memory) {
                const memory = module.wasmMemory || module.asm.memory;
                const buffer = memory.buffer;
                module.HEAP8 = new Int8Array(buffer);
                module.HEAPU8 = new Uint8Array(buffer);
                module.HEAP16 = new Int16Array(buffer);
                module.HEAPU16 = new Uint16Array(buffer);
                module.HEAP32 = new Int32Array(buffer);
                module.HEAPU32 = new Uint32Array(buffer);
                module.HEAPF32 = new Float32Array(buffer);
                module.HEAPF64 = new Float64Array(buffer);
                console.log('[OpenJTalkWrapper] HEAP arrays created manually');
            }
        }
        
        // Log what's available
        console.log('[OpenJTalkWrapper] Module ready. Available exports:', {
            HEAP8: !!module.HEAP8,
            _malloc: !!module._malloc,
            _free: !!module._free,
            UTF8ToString: !!module.UTF8ToString,
            stringToUTF8: !!module.stringToUTF8,
            FS: !!module.FS
        });
        
        // Clean up
        delete window.__OpenJTalkModuleFunc;
        
        return module;
    };
    
    console.log('[OpenJTalkWrapper] Wrapper installed');
})();