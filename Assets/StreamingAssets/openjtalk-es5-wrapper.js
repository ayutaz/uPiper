// OpenJTalk ES5 Wrapper
// Converts ES6 module to globally accessible function

(async function() {
    try {
        // Fetch the ES6 module
        const response = await fetch('StreamingAssets/openjtalk.js');
        const moduleText = await response.text();
        
        // Convert ES6 to ES5 syntax
        let modifiedText = moduleText
            // Replace import.meta.url with current location
            .replace(/import\.meta\.url/g, '"' + window.location.href + '"')
            // Remove export statement
            .replace(/export\s+default\s+OpenJTalkModule\s*;?\s*$/m, '');
        
        // Create a function that returns OpenJTalkModule
        const wrapperCode = `
        (function() {
            ${modifiedText}
            
            // Make OpenJTalkModule globally available
            window.OpenJTalkModuleOriginal = OpenJTalkModule;
            
            // Create wrapper that ensures HEAP arrays are exposed
            window.OpenJTalkModule = async function(moduleArg) {
                moduleArg = moduleArg || {};
                
                // Store the module instance
                var moduleInstance = null;
                
                // Hook into runtime initialization
                var originalOnRuntimeInitialized = moduleArg.onRuntimeInitialized;
                moduleArg.onRuntimeInitialized = function() {
                    console.log('[OpenJTalk ES5 Wrapper] Runtime initialized');
                    
                    if (originalOnRuntimeInitialized) {
                        originalOnRuntimeInitialized.call(this);
                    }
                    
                    // At this point, HEAP arrays should exist in the module's scope
                    // We need to find and expose them
                    if (moduleInstance) {
                        // Check if wasmMemory exists
                        var memory = moduleInstance.wasmMemory || moduleInstance.memory;
                        if (memory && memory.buffer) {
                            var buffer = memory.buffer;
                            moduleInstance.HEAP8 = new Int8Array(buffer);
                            moduleInstance.HEAPU8 = new Uint8Array(buffer);
                            moduleInstance.HEAP16 = new Int16Array(buffer);
                            moduleInstance.HEAPU16 = new Uint16Array(buffer);
                            moduleInstance.HEAP32 = new Int32Array(buffer);
                            moduleInstance.HEAPU32 = new Uint32Array(buffer);
                            moduleInstance.HEAPF32 = new Float32Array(buffer);
                            moduleInstance.HEAPF64 = new Float64Array(buffer);
                            
                            console.log('[OpenJTalk ES5 Wrapper] HEAP arrays created from memory buffer');
                        }
                    }
                };
                
                // Call the original module
                moduleInstance = await OpenJTalkModuleOriginal(moduleArg);
                
                // Final attempt to ensure HEAP arrays
                if (moduleInstance && !moduleInstance.HEAP8) {
                    // Try to get memory from various sources
                    var memory = moduleInstance.wasmMemory || 
                               moduleInstance.memory || 
                               (moduleInstance.asm && moduleInstance.asm.memory) ||
                               (typeof wasmMemory !== 'undefined' ? wasmMemory : null);
                    
                    if (memory && memory.buffer) {
                        var buffer = memory.buffer;
                        moduleInstance.HEAP8 = new Int8Array(buffer);
                        moduleInstance.HEAPU8 = new Uint8Array(buffer);
                        moduleInstance.HEAP16 = new Int16Array(buffer);
                        moduleInstance.HEAPU16 = new Uint16Array(buffer);
                        moduleInstance.HEAP32 = new Int32Array(buffer);
                        moduleInstance.HEAPU32 = new Uint32Array(buffer);
                        moduleInstance.HEAPF32 = new Float32Array(buffer);
                        moduleInstance.HEAPF64 = new Float64Array(buffer);
                        
                        console.log('[OpenJTalk ES5 Wrapper] HEAP arrays created in final attempt');
                    }
                }
                
                // Add helper methods if they don't exist
                if (!moduleInstance.getValue && moduleInstance.HEAP8) {
                    moduleInstance.getValue = function(ptr, type) {
                        type = type || 'i8';
                        switch(type) {
                            case 'i8': return moduleInstance.HEAP8[ptr];
                            case 'i16': return moduleInstance.HEAP16[ptr >> 1];
                            case 'i32': return moduleInstance.HEAP32[ptr >> 2];
                            case 'float': return moduleInstance.HEAPF32[ptr >> 2];
                            case 'double': return moduleInstance.HEAPF64[ptr >> 3];
                            default: return moduleInstance.HEAP8[ptr];
                        }
                    };
                }
                
                if (!moduleInstance.setValue && moduleInstance.HEAP8) {
                    moduleInstance.setValue = function(ptr, value, type) {
                        type = type || 'i8';
                        switch(type) {
                            case 'i8': moduleInstance.HEAP8[ptr] = value; break;
                            case 'i16': moduleInstance.HEAP16[ptr >> 1] = value; break;
                            case 'i32': moduleInstance.HEAP32[ptr >> 2] = value; break;
                            case 'float': moduleInstance.HEAPF32[ptr >> 2] = value; break;
                            case 'double': moduleInstance.HEAPF64[ptr >> 3] = value; break;
                            default: moduleInstance.HEAP8[ptr] = value; break;
                        }
                    };
                }
                
                return moduleInstance;
            };
            
            console.log('[OpenJTalk ES5 Wrapper] Module wrapper installed');
        })();
        `;
        
        // Execute the wrapper code
        eval(wrapperCode);
        
        console.log('[OpenJTalk ES5 Wrapper] ES5 conversion complete');
        
    } catch (error) {
        console.error('[OpenJTalk ES5 Wrapper] Failed to load module:', error);
    }
})();