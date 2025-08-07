mergeInto(LibraryManager.library, {
    // Initialize OpenJTalk WebAssembly module
    InitializeOpenJTalkWeb: function() {
        console.log('[uPiper] Initializing OpenJTalk WebAssembly...');
        
        try {
            // Check if already initialized
            if (window.uPiperOpenJTalk && window.uPiperOpenJTalk.initialized) {
                console.log('[uPiper] OpenJTalk already initialized');
                return 1;
            }
            
            // Store loading state
            window.uPiperOpenJTalkLoadingState = 'loading';
            
            // Create initialization promise
            if (!window.uPiperOpenJTalkInit) {
                window.uPiperOpenJTalkInit = (async function() {
                    try {
                        console.log('[uPiper] Loading OpenJTalk module...');
                        
                        // Load openjtalk.js directly (it's already ES5)
                        const moduleScript = document.createElement('script');
                        moduleScript.src = 'StreamingAssets/openjtalk.js';
                        
                        // Wait for script to load
                        await new Promise((resolve, reject) => {
                            moduleScript.onload = resolve;
                            moduleScript.onerror = reject;
                            document.head.appendChild(moduleScript);
                        });
                        
                        // Wait for OpenJTalkModule to be available
                        let attempts = 0;
                        while (typeof window.OpenJTalkModule === 'undefined' && attempts < 50) {
                            await new Promise(resolve => setTimeout(resolve, 100));
                            attempts++;
                        }
                        
                        if (typeof window.OpenJTalkModule === 'undefined') {
                            throw new Error('OpenJTalkModule not found');
                        }
                        
                        console.log('[uPiper] OpenJTalkModule loaded, initializing...');
                        
                        // Initialize the module
                        const module = await window.OpenJTalkModule({
                            locateFile: function(path) {
                                if (path.endsWith('.wasm')) {
                                    return 'StreamingAssets/openjtalk.wasm';
                                }
                                return path;
                            },
                            printErr: function(text) {
                                console.error('[OpenJTalk]', text);
                            },
                            print: function(text) {
                                console.log('[OpenJTalk]', text);
                            },
                            onRuntimeInitialized: function() {
                                console.log('[uPiper] OpenJTalk runtime initialized');
                            }
                        });
                        
                        // Wait for runtime to be initialized
                        if (!module.calledRun) {
                            await new Promise((resolve) => {
                                const originalCallback = module.onRuntimeInitialized;
                                module.onRuntimeInitialized = function() {
                                    if (originalCallback) originalCallback();
                                    resolve();
                                };
                            });
                        }
                        
                        // Store module reference immediately after loading
                        window.uPiperOpenJTalk = {
                            initialized: false, // Will be set to true after dictionary load
                            module: module,
                            FS: module.FS,
                            ccall: module.ccall,
                            cwrap: module.cwrap,
                            _malloc: module._malloc || module.malloc,
                            _free: module._free || module.free,
                            HEAP8: module.HEAP8,
                            HEAPU8: module.HEAPU8,
                            HEAP16: module.HEAP16,
                            HEAPU16: module.HEAPU16,
                            HEAP32: module.HEAP32,
                            HEAPU32: module.HEAPU32,
                            HEAPF32: module.HEAPF32,
                            HEAPF64: module.HEAPF64,
                            UTF8ToString: module.UTF8ToString,
                            stringToUTF8: module.stringToUTF8,
                            lengthBytesUTF8: module.lengthBytesUTF8,
                            allocateUTF8: module.allocateUTF8 || module.allocateString,
                            getValue: module.getValue,
                            setValue: module.setValue,
                            // Wrap OpenJTalk functions (may not exist in minimal build)
                            _openjtalk_initialize: module._openjtalk_initialize || null,
                            _openjtalk_synthesis_labels: module._openjtalk_synthesis_labels || null,
                            _openjtalk_free_string: module._openjtalk_free_string || null,
                            _openjtalk_clear: module._openjtalk_clear || null,
                            _openjtalk_test: module._openjtalk_test || null
                        };
                        
                        console.log('[uPiper] OpenJTalk module loaded successfully');
                        window.uPiperOpenJTalkLoadingState = 'loaded';
                        return true;
                    } catch (error) {
                        console.error('[uPiper] Failed to load OpenJTalk module:', error);
                        window.uPiperOpenJTalkLoadingState = 'error';
                        return false;
                    }
                })();
            }
            
            // Check if already loaded during async init
            if (window.uPiperOpenJTalk && window.uPiperOpenJTalk.module) {
                console.log('[uPiper] OpenJTalk module already loaded during async init');
                return 1;
            }
            
            // Return 0 to indicate async initialization in progress
            return 0;
            
        } catch (error) {
            console.error('[uPiper] Failed to initialize OpenJTalk:', error);
            return -1;
        }
    },
    
    // Check if OpenJTalk is initialized
    IsOpenJTalkInitialized: function() {
        return (window.uPiperOpenJTalk && window.uPiperOpenJTalk.initialized) ? 1 : 0;
    },
    
    // Check if OpenJTalk module is loaded (but not necessarily initialized with dictionary)
    IsOpenJTalkModuleLoaded: function() {
        var isLoaded = (window.uPiperOpenJTalk && window.uPiperOpenJTalk.module) ? 1 : 0;
        console.log('[uPiper] IsOpenJTalkModuleLoaded:', isLoaded, 'window.uPiperOpenJTalk:', !!window.uPiperOpenJTalk);
        return isLoaded;
    },
    
    // Load dictionary data for OpenJTalk
    LoadOpenJTalkDictionary: function(dictionaryDataPtr, dataLength) {
        console.log('[uPiper] Loading OpenJTalk dictionary...');
        
        if (!window.uPiperOpenJTalk || !window.uPiperOpenJTalk.module) {
            console.error('[uPiper] OpenJTalk module not loaded');
            return false;
        }
        
        try {
            var module = window.uPiperOpenJTalk.module;
            
            // For piper-plus version, just mark as initialized
            // The actual initialization happens when synthesis_labels is called
            console.log('[uPiper] OpenJTalk module ready for use (lazy initialization)');
            window.uPiperOpenJTalk.initialized = true;
            return true;
        } catch (error) {
            console.error('[uPiper] Failed to load dictionary:', error);
            return false;
        }
    },
    
    // Phonemize Japanese text using OpenJTalk
    PhonemizeJapaneseText: function(textPtr) {
        var text = UTF8ToString(textPtr);
        console.log('[uPiper] PhonemizeJapaneseText called with:', text);
        
        // Check if OpenJTalk is available
        if (!window.uPiperOpenJTalk || !window.uPiperOpenJTalk.module) {
            console.error('[uPiper] OpenJTalk module not loaded');
            var errorResult = JSON.stringify({
                success: false,
                error: 'OpenJTalk module not loaded',
                phonemes: []
            });
            var bufferSize = lengthBytesUTF8(errorResult) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
        
        
        try {
            var module = window.uPiperOpenJTalk.module;
            
            // Log available functions
            console.log('[uPiper] Using OpenJTalk for phonemization with text:', text);
            
            // Use OpenJTalk module functions for OpenJTalk operations
            var openJTalkMalloc = window.uPiperOpenJTalk._malloc;
            var openJTalkFree = window.uPiperOpenJTalk._free;
            var openJTalkAllocateUTF8 = window.uPiperOpenJTalk.allocateUTF8 || function(str) {
                var len = window.uPiperOpenJTalk.lengthBytesUTF8(str) + 1;
                var ptr = openJTalkMalloc(len);
                window.uPiperOpenJTalk.stringToUTF8(str, ptr, len);
                return ptr;
            };
            var openJTalkUTF8ToString = window.uPiperOpenJTalk.UTF8ToString;
            
            var labels = '';
            
            // Convert text to proper encoding using OpenJTalk's allocator
            var textPtr = openJTalkAllocateUTF8(text);
            
            // Use piper-plus production API
            if (window.uPiperOpenJTalk._openjtalk_synthesis_labels) {
                // Call OpenJTalk synthesis_labels function
                var resultPtr = window.uPiperOpenJTalk._openjtalk_synthesis_labels(textPtr);
                openJTalkFree(textPtr);
                
                if (!resultPtr) {
                    throw new Error('OpenJTalk synthesis_labels returned null');
                }
                
                labels = openJTalkUTF8ToString(resultPtr);
                window.uPiperOpenJTalk._openjtalk_free_string(resultPtr);
                
                console.log('[uPiper] OpenJTalk labels received, processing phonemes...');
                // Continue with label processing below...
            } else {
                openJTalkFree(textPtr);
                console.error('[uPiper] OpenJTalk synthesis_labels function not available');
                var errorResult = JSON.stringify({
                    success: false,
                    error: 'OpenJTalk synthesis_labels function not available',
                    phonemes: []
                });
                // Use Unity's global functions for the return buffer
                var bufferSize = lengthBytesUTF8(errorResult) + 1;
                var buffer = _malloc(bufferSize);
                stringToUTF8(errorResult, buffer, bufferSize);
                return buffer;
            }
            
            // Extract phonemes from labels
            var lines = labels.split('\n').filter(function(line) { return line.trim(); });
            var phonemes = [];
            
            // Multi-character phoneme mapping (same as piper-plus)
            var multiCharPhonemes = {
                'br': '\ue000',
                'ch': '\ue001',
                'cl': '\ue002',
                'dy': '\ue003',
                'gy': '\ue004',
                'hy': '\ue005',
                'ky': '\ue006',
                'my': '\ue007',
                'ny': '\ue008',
                'py': '\ue009',
                'ry': '\ue00a',
                'sh': '\ue00b',
                'ts': '\ue00c',
                'ty': '\ue00d'
            };
            
            // Add BOS marker
            phonemes.push('^');
            
            // Process each label line
            lines.forEach(function(line) {
                var match = line.match(/\-([^+]+)\+/);
                if (match && match[1] !== 'sil') {
                    var phoneme = match[1];
                    
                    // Convert multi-character phonemes to PUA
                    if (multiCharPhonemes[phoneme]) {
                        phoneme = multiCharPhonemes[phoneme];
                    }
                    
                    // Skip 'pau' (pause)
                    if (phoneme !== 'pau') {
                        phonemes.push(phoneme);
                    }
                }
            });
            
            // Add EOS marker
            phonemes.push('$');
            
            console.log('[uPiper] Extracted phonemes:', phonemes);
            
            var result = JSON.stringify({
                success: true,
                phonemes: phonemes
            });
            
            var bufferSize = lengthBytesUTF8(result) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(result, buffer, bufferSize);
            return buffer;
            
        } catch (error) {
            console.error('[uPiper] Phonemization failed:', error);
            var errorResult = JSON.stringify({
                success: false,
                error: error.toString(),
                phonemes: []
            });
            // Always use Unity's global functions for consistency
            var bufferSize = lengthBytesUTF8(errorResult) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
    },
    
    // Free allocated memory (OpenJTalk)
    FreeOpenJTalkMemory: function(ptr) {
        if (ptr && window.uPiperOpenJTalk && window.uPiperOpenJTalk._free) {
            window.uPiperOpenJTalk._free(ptr);
        }
    },
    
    // Free allocated memory (generic Unity)
    FreeWebGLMemory: function(ptr) {
        // Use Unity's global _free for memory allocated by Unity's Module
        if (ptr && typeof _free !== 'undefined') {
            _free(ptr);
        }
    }
});// Cache buster: 1754584334
