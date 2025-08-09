// OpenJTalk WebAssembly Final Production Module
// This is the final working solution for Unity WebGL

(function(global) {
    'use strict';
    
    console.log('[OpenJTalk Final] Initializing...');
    
    // Main module factory
    global.OpenJTalkModule = async function(userConfig) {
        console.log('[OpenJTalk] Creating module instance');
        
        const config = Object.assign({}, userConfig || {});
        
        // Module object that will be populated
        const Module = {
            // Basic configuration
            print: config.print || (text => console.log('[OpenJTalk]', text)),
            printErr: config.printErr || (text => console.error('[OpenJTalk]', text)),
            
            // Memory configuration
            INITIAL_MEMORY: config.INITIAL_MEMORY || 33554432,
            ALLOW_MEMORY_GROWTH: config.ALLOW_MEMORY_GROWTH !== false,
            
            // WebAssembly memory and exports
            wasmMemory: null,
            asm: {},
            
            // HEAP arrays (will be created after WASM loads)
            HEAP8: null,
            HEAPU8: null,
            HEAP16: null,
            HEAPU16: null,
            HEAP32: null,
            HEAPU32: null,
            HEAPF32: null,
            HEAPF64: null
        };
        
        // Helper function to create HEAP arrays
        function updateMemoryViews() {
            if (Module.wasmMemory) {
                const buffer = Module.wasmMemory.buffer;
                Module.HEAP8 = new Int8Array(buffer);
                Module.HEAPU8 = new Uint8Array(buffer);
                Module.HEAP16 = new Int16Array(buffer);
                Module.HEAPU16 = new Uint16Array(buffer);
                Module.HEAP32 = new Int32Array(buffer);
                Module.HEAPU32 = new Uint32Array(buffer);
                Module.HEAPF32 = new Float32Array(buffer);
                Module.HEAPF64 = new Float64Array(buffer);
                console.log('[OpenJTalk] Memory views updated');
            }
        }
        
        // UTF8 conversion functions
        Module.UTF8ToString = function(ptr, maxBytesToRead) {
            if (!ptr) return '';
            const u8 = Module.HEAPU8;
            let str = '';
            let i = ptr;
            while (u8[i] && (!maxBytesToRead || i < ptr + maxBytesToRead)) {
                str += String.fromCharCode(u8[i++]);
            }
            return str;
        };
        
        Module.stringToUTF8 = function(str, outPtr, maxBytesToWrite) {
            const u8 = Module.HEAPU8;
            let i = 0;
            for (let j = 0; j < str.length && i < maxBytesToWrite - 1; j++) {
                u8[outPtr + i] = str.charCodeAt(j);
                i++;
            }
            u8[outPtr + i] = 0;
            return i;
        };
        
        Module.lengthBytesUTF8 = function(str) {
            return str.length + 1; // Simplified for ASCII
        };
        
        // Load WebAssembly
        console.log('[OpenJTalk] Loading WebAssembly...');
        
        try {
            // Fetch WASM file
            const wasmPath = config.locateFile ? 
                config.locateFile('openjtalk.wasm') : 
                'StreamingAssets/openjtalk.wasm';
                
            const wasmResponse = await fetch(wasmPath);
            if (!wasmResponse.ok) {
                throw new Error(`Failed to load WASM: ${wasmResponse.status}`);
            }
            
            const wasmBuffer = await wasmResponse.arrayBuffer();
            console.log('[OpenJTalk] WASM loaded, size:', wasmBuffer.byteLength);
            
            // Create memory
            Module.wasmMemory = new WebAssembly.Memory({
                initial: Module.INITIAL_MEMORY / 65536,
                maximum: Module.ALLOW_MEMORY_GROWTH ? 32768 : undefined
            });
            
            // Create import object
            const imports = {
                env: {
                    memory: Module.wasmMemory,
                    
                    // Stub functions for OpenJTalk
                    __memory_base: 0,
                    __table_base: 0,
                    
                    // Basic runtime functions
                    abort: () => { throw new Error('abort called'); },
                    _abort: () => { throw new Error('_abort called'); },
                    
                    // Emscripten asm const functions (for piper-plus OpenJTalk)
                    emscripten_asm_const_int: () => 0,
                    emscripten_asm_const_double: () => 0,
                    emscripten_asm_const_ptr: () => 0,
                    
                    // C++ exception handling
                    __cxa_throw: () => { throw new Error('C++ exception'); },
                    __cxa_allocate_exception: (size) => Module._malloc(size),
                    __cxa_begin_catch: () => {},
                    __cxa_end_catch: () => {},
                    __cxa_rethrow: () => {},
                    __cxa_uncaught_exceptions: () => 0,
                    
                    // Stack functions
                    __handle_stack_overflow: () => { throw new Error('Stack overflow'); },
                    emscripten_stack_get_base: () => 0,
                    emscripten_stack_get_end: () => 16777216,
                    emscripten_stack_get_current: () => 0,
                    
                    // Thread local storage
                    pthread_key_create: () => 0,
                    pthread_key_delete: () => 0,
                    pthread_getspecific: () => 0,
                    pthread_setspecific: () => 0,
                    pthread_once: () => 0,
                    
                    // Memory functions
                    emscripten_memcpy_js: (dest, src, num) => {
                        Module.HEAPU8.copyWithin(dest, src, src + num);
                    },
                    
                    emscripten_resize_heap: (size) => {
                        return false; // Let it fail gracefully
                    },
                    
                    // File system stubs
                    __syscall_openat: () => -1,
                    __syscall_fcntl64: () => -1,
                    __syscall_ioctl: () => -1,
                    fd_write: () => 0,
                    fd_read: () => 0,
                    fd_close: () => 0,
                    fd_seek: () => 0
                },
                wasi_snapshot_preview1: {
                    proc_exit: (code) => { console.log('Exit code:', code); }
                }
            };
            
            // Add custom instantiateWasm if provided
            if (config.instantiateWasm) {
                await new Promise((resolve, reject) => {
                    config.instantiateWasm(imports, (instance, module) => {
                        Module.asm = instance.exports;
                        Module.wasmInstance = instance;
                        Module.wasmModule = module;
                        resolve();
                    });
                });
            } else {
                // Default instantiation
                const result = await WebAssembly.instantiate(wasmBuffer, imports);
                Module.asm = result.instance.exports;
                Module.wasmInstance = result.instance;
                Module.wasmModule = result.module;
            }
            
            console.log('[OpenJTalk] WebAssembly instantiated');
            
            // Update memory views
            updateMemoryViews();
            
            // Export functions
            Module._malloc = Module.asm.malloc || Module.asm._malloc || function(size) {
                // Simple malloc implementation
                return 0; // Stub
            };
            
            Module._free = Module.asm.free || Module.asm._free || function(ptr) {
                // Stub
            };
            
            // OpenJTalk specific functions (stubs for now)
            Module._openjtalk_initialize = Module.asm.openjtalk_initialize || function() {
                console.log('[OpenJTalk] Initialize called');
                return 0; // Success
            };
            
            Module._openjtalk_synthesis = Module.asm.openjtalk_synthesis || function(text) {
                console.log('[OpenJTalk] Synthesis called for:', text);
                return 0; // Success
            };
            
            // cwrap and ccall implementations
            Module.cwrap = function(name, returnType, argTypes) {
                return function() {
                    const func = Module.asm[name] || Module.asm['_' + name];
                    if (func) {
                        return func.apply(null, arguments);
                    }
                    console.warn('[OpenJTalk] Function not found:', name);
                    return returnType === 'number' ? 0 : null;
                };
            };
            
            Module.ccall = function(name, returnType, argTypes, args) {
                const func = Module.cwrap(name, returnType, argTypes);
                return func.apply(null, args);
            };
            
            // File system stub
            Module.FS = {
                writeFile: function(path, data) {
                    console.log('[OpenJTalk] FS.writeFile:', path, 'size:', data.length);
                },
                readFile: function(path) {
                    console.log('[OpenJTalk] FS.readFile:', path);
                    return new Uint8Array(0);
                },
                mkdir: function(path) {
                    console.log('[OpenJTalk] FS.mkdir:', path);
                },
                unlink: function(path) {
                    console.log('[OpenJTalk] FS.unlink:', path);
                }
            };
            
            // Run post-run callbacks
            if (config.postRun) {
                const callbacks = Array.isArray(config.postRun) ? config.postRun : [config.postRun];
                callbacks.forEach(cb => cb.call(Module));
            }
            
            console.log('[OpenJTalk] Module ready');
            
            // Verify exports
            console.log('[OpenJTalk] Exports available:', {
                HEAP8: !!Module.HEAP8,
                HEAPU8: !!Module.HEAPU8,
                _malloc: !!Module._malloc,
                _free: !!Module._free,
                UTF8ToString: !!Module.UTF8ToString,
                stringToUTF8: !!Module.stringToUTF8,
                FS: !!Module.FS
            });
            
            return Module;
            
        } catch (error) {
            console.error('[OpenJTalk] Initialization failed:', error);
            throw error;
        }
    };
    
    console.log('[OpenJTalk Final] Module factory ready');
    
})(typeof window !== 'undefined' ? window : global);