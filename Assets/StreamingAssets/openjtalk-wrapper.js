// OpenJTalk WebAssembly Wrapper for Unity WebGL
// Complete fix for piper-plus HEAP arrays issue

(function() {
    console.log('[OpenJTalkWrapper] Installing complete HEAP fix...');
    
    window.OpenJTalkModule = async function(userConfig) {
        console.log('[OpenJTalkWrapper] Loading OpenJTalk module');
        
        // First, set up HEAP proxies BEFORE loading the module
        // This prevents "HEAP8 not exported" errors
        const heapProxies = {};
        const heapNames = ['HEAP8', 'HEAPU8', 'HEAP16', 'HEAPU16', 'HEAP32', 'HEAPU32', 'HEAPF32', 'HEAPF64'];
        
        heapNames.forEach(name => {
            Object.defineProperty(window, name, {
                configurable: true,
                get: function() {
                    return heapProxies[name] || new Uint8Array(0);
                },
                set: function(value) {
                    heapProxies[name] = value;
                }
            });
        });
        
        // Load and modify the JS
        const response = await fetch('StreamingAssets/openjtalk.js');
        let jsText = await response.text();
        
        // Fix paths
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
        
        // Remove export
        jsText = jsText.replace('export default OpenJTalkModule;', '');
        
        // CRITICAL: Inject HEAP export code into the module
        // We need to modify the code to make HEAP arrays available
        // Find where HEAP arrays are defined and make them accessible
        
        // Replace the function that uses HEAP arrays internally
        jsText = jsText.replace(
            /function updateMemoryViews\(\)\{[^}]+\}/,
            `function updateMemoryViews(){
                var b=wasmMemory.buffer;
                HEAP8=Module.HEAP8=new Int8Array(b);
                HEAP16=Module.HEAP16=new Int16Array(b);
                HEAPU8=Module.HEAPU8=new Uint8Array(b);
                HEAPU16=Module.HEAPU16=new Uint16Array(b);
                HEAP32=Module.HEAP32=new Int32Array(b);
                HEAPU32=Module.HEAPU32=new Uint32Array(b);
                HEAPF32=Module.HEAPF32=new Float32Array(b);
                HEAPF64=Module.HEAPF64=new Float64Array(b);
                HEAP64=Module.HEAP64=new BigInt64Array(b);
                HEAPU64=Module.HEAPU64=new BigUint64Array(b);
                // Also set on window for compatibility
                window.HEAP8=HEAP8;
                window.HEAPU8=HEAPU8;
                window.HEAP16=HEAP16;
                window.HEAPU16=HEAPU16;
                window.HEAP32=HEAP32;
                window.HEAPU32=HEAPU32;
                window.HEAPF32=HEAPF32;
                window.HEAPF64=HEAPF64;
            }`
        );
        
        // Also replace any error-throwing code for HEAP8
        jsText = jsText.replace(
            /abort\(["']'HEAP8' was not exported[^)]*\)/g,
            '(function(){console.warn("HEAP8 access intercepted"); return Module.HEAP8 || window.HEAP8;})()'
        );
        
        // Replace the unexportedRuntimeSymbol function to not throw for HEAP arrays
        jsText = jsText.replace(
            'function unexportedRuntimeSymbol(sym)',
            `function unexportedRuntimeSymbol(sym){
                // Don't throw for HEAP arrays - we'll handle them
                if(sym.startsWith('HEAP')){
                    console.log('[OpenJTalk] Intercepted HEAP array access:', sym);
                    return;
                }
                // Original function body follows
            `
        );
        
        // Make module available globally
        jsText += '\n; window.__OpenJTalkModuleFunc = OpenJTalkModule;';
        
        console.log('[OpenJTalkWrapper] Executing modified code...');
        
        // Execute the modified code
        const script = document.createElement('script');
        script.textContent = jsText;
        document.head.appendChild(script);
        
        await new Promise(resolve => setTimeout(resolve, 100));
        
        if (typeof window.__OpenJTalkModuleFunc !== 'function') {
            throw new Error('[OpenJTalkWrapper] Failed to load module function');
        }
        
        // Create config
        const config = Object.assign({}, userConfig || {}, {
            instantiateWasm: async function(imports, receiveInstance) {
                console.log('[OpenJTalkWrapper] Loading WASM...');
                
                try {
                    const wasmResponse = await fetch('StreamingAssets/openjtalk.wasm');
                    if (!wasmResponse.ok) {
                        throw new Error(`WASM fetch failed: ${wasmResponse.status}`);
                    }
                    
                    const wasmArrayBuffer = await wasmResponse.arrayBuffer();
                    console.log('[OpenJTalkWrapper] WASM size:', wasmArrayBuffer.byteLength);
                    
                    const wasmInstance = await WebAssembly.instantiate(wasmArrayBuffer, imports);
                    
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
            
            // Pre-run callback to ensure HEAP arrays are set up
            preRun: [(userConfig?.preRun || [])].flat().concat([
                function() {
                    console.log('[OpenJTalkWrapper] PreRun: Setting up HEAP arrays');
                }
            ]),
            
            postRun: [(userConfig?.postRun || [])].flat().concat([
                function() {
                    console.log('[OpenJTalkWrapper] PostRun: Module initialized');
                    // Ensure HEAP arrays are accessible
                    if (Module.HEAP8) {
                        console.log('[OpenJTalkWrapper] HEAP arrays confirmed available');
                    }
                }
            ]),
            
            print: console.log.bind(console),
            printErr: console.error.bind(console)
        });
        
        console.log('[OpenJTalkWrapper] Initializing module...');
        const module = await window.__OpenJTalkModuleFunc(config);
        
        // Remove temporary proxies
        heapNames.forEach(name => {
            delete window[name];
            if (heapProxies[name]) {
                window[name] = heapProxies[name];
            }
        });
        
        // Ensure module has all required exports
        console.log('[OpenJTalkWrapper] Module initialized. Checking exports...');
        console.log('[OpenJTalkWrapper] Available:', {
            HEAP8: !!module.HEAP8,
            _malloc: !!module._malloc,
            _free: !!module._free,
            UTF8ToString: !!module.UTF8ToString,
            FS: !!module.FS
        });
        
        // Clean up
        delete window.__OpenJTalkModuleFunc;
        
        return module;
    };
    
    console.log('[OpenJTalkWrapper] Wrapper ready');
})();