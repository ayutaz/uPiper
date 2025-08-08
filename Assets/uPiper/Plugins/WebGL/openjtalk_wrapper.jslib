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
                        
                        // Try to dynamically import the ES6 module
                        const moduleScript = document.createElement('script');
                        moduleScript.type = 'module';
                        moduleScript.textContent = `
                            (async function() {
                                try {
                                    // Set up global HEAP arrays that the module might expect
                                    // These will be overwritten by the actual module
                                    window.HEAP8 = new Int8Array(0);
                                    window.HEAPU8 = new Uint8Array(0);
                                    window.HEAP16 = new Int16Array(0);
                                    window.HEAPU16 = new Uint16Array(0);
                                    window.HEAP32 = new Int32Array(0);
                                    window.HEAPU32 = new Uint32Array(0);
                                    window.HEAPF32 = new Float32Array(0);
                                    window.HEAPF64 = new Float64Array(0);
                                    
                                    // Dynamically import the OpenJTalk module
                                    const response = await fetch('StreamingAssets/openjtalk.js');
                                    const moduleText = await response.text();
                                    
                                    // Create a blob URL for the module
                                    const blob = new Blob([moduleText], { type: 'application/javascript' });
                                    const moduleUrl = URL.createObjectURL(blob);
                                    
                                    // Import the module
                                    const module = await import(moduleUrl);
                                    
                                    // Get the function (it's the default export)
                                    window.OpenJTalkModule = module.default || module.OpenJTalkModule || module;
                                    
                                    console.log('[uPiper] OpenJTalk module loaded via dynamic import');
                                    
                                    // Clean up temporary global HEAP arrays
                                    // They should be replaced by the module's own versions
                                    ['HEAP8', 'HEAPU8', 'HEAP16', 'HEAPU16', 'HEAP32', 'HEAPU32', 'HEAPF32', 'HEAPF64'].forEach(name => {
                                        if (window[name] && window[name].length === 0) {
                                            delete window[name];
                                        }
                                    });
                                } catch (error) {
                                    console.error('[uPiper] Failed to import OpenJTalk module:', error);
                                    window.OpenJTalkModuleError = error;
                                }
                            })();
                        `;
                        document.head.appendChild(moduleScript);
                        
                        // Wait for OpenJTalkModule to be available
                        let attempts = 0;
                        while (typeof window.OpenJTalkModule === 'undefined' && !window.OpenJTalkModuleError && attempts < 100) {
                            await new Promise(resolve => setTimeout(resolve, 100));
                            attempts++;
                        }
                        
                        if (window.OpenJTalkModuleError) {
                            throw window.OpenJTalkModuleError;
                        }
                        
                        if (typeof window.OpenJTalkModule === 'undefined') {
                            throw new Error('OpenJTalkModule not found after dynamic import');
                        }
                        
                        console.log('[uPiper] OpenJTalkModule loaded, initializing...');
                        
                        // Initialize the module with required exports
                        // We need to ensure HEAP arrays are accessible
                        const moduleConfig = {
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
                        };
                        
                        // Call the module function
                        let module;
                        try {
                            module = await window.OpenJTalkModule(moduleConfig);
                        } catch (err) {
                            // If there's an error about HEAP8, it might be because the module
                            // expects it to be global. Let's try to provide it.
                            if (err.toString().includes('HEAP8')) {
                                console.log('[uPiper] Retrying with global HEAP workaround');
                                
                                // Create a temporary global scope for the module
                                const tempGlobals = {};
                                const originalGlobals = {};
                                
                                // Save and create temporary globals
                                ['HEAP8', 'HEAPU8', 'HEAP16', 'HEAPU16', 'HEAP32', 'HEAPU32', 'HEAPF32', 'HEAPF64'].forEach(name => {
                                    if (typeof window[name] !== 'undefined') {
                                        originalGlobals[name] = window[name];
                                    }
                                    window[name] = null; // Placeholder
                                });
                                
                                try {
                                    module = await window.OpenJTalkModule(moduleConfig);
                                } finally {
                                    // Restore original globals
                                    Object.keys(originalGlobals).forEach(name => {
                                        window[name] = originalGlobals[name];
                                    });
                                    
                                    // Clean up any placeholders
                                    ['HEAP8', 'HEAPU8', 'HEAP16', 'HEAPU16', 'HEAP32', 'HEAPU32', 'HEAPF32', 'HEAPF64'].forEach(name => {
                                        if (!(name in originalGlobals) && window[name] === null) {
                                            delete window[name];
                                        }
                                    });
                                }
                            } else {
                                throw err;
                            }
                        }
                        
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
                        
                        // The module should already have HEAP arrays exposed
                        // Check and log what's available
                        console.log('[uPiper] Module exports available:', Object.keys(module).filter(k => !k.startsWith('_')).slice(0, 20));
                        console.log('[uPiper] HEAP8 available:', !!module.HEAP8);
                        console.log('[uPiper] _malloc available:', !!module._malloc);
                        console.log('[uPiper] UTF8ToString available:', !!module.UTF8ToString);
                        
                        // Store module reference immediately after loading
                        window.uPiperOpenJTalk = {
                            initialized: false, // Will be set to true after dictionary load
                            module: module,
                            FS: module.FS,
                            ccall: module.ccall || module.cwrap,
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
            var openJTalkAllocateUTF8 = function(str) {
                if (!window.uPiperOpenJTalk.lengthBytesUTF8 || !window.uPiperOpenJTalk.stringToUTF8) {
                    console.error('[uPiper] UTF8 functions not available in OpenJTalk module');
                    // Fallback to simple ASCII encoding
                    var len = str.length + 1;
                    var ptr = openJTalkMalloc(len);
                    for (var i = 0; i < str.length; i++) {
                        window.uPiperOpenJTalk.HEAPU8[ptr + i] = str.charCodeAt(i) & 0xFF;
                    }
                    window.uPiperOpenJTalk.HEAPU8[ptr + str.length] = 0;
                    return ptr;
                }
                var len = window.uPiperOpenJTalk.lengthBytesUTF8(str) + 1;
                var ptr = openJTalkMalloc(len);
                window.uPiperOpenJTalk.stringToUTF8(str, ptr, len);
                return ptr;
            };
            var openJTalkUTF8ToString = window.uPiperOpenJTalk.UTF8ToString;
            
            var labels = '';
            
            // Convert text to proper encoding using OpenJTalk's allocator
            var textPtr = openJTalkAllocateUTF8(text);
            
            // Check if OpenJTalk synthesis_labels function is available
            if (window.uPiperOpenJTalk._openjtalk_synthesis_labels) {
                // Call OpenJTalk synthesis_labels function
                var resultPtr = window.uPiperOpenJTalk._openjtalk_synthesis_labels(textPtr);
                openJTalkFree(textPtr);
                
                if (!resultPtr) {
                    throw new Error('OpenJTalk synthesis_labels returned null');
                }
                
                if (!window.uPiperOpenJTalk.UTF8ToString) {
                    console.error('[uPiper] UTF8ToString not available, using fallback');
                    // Fallback to simple ASCII decoding
                    var str = '';
                    var i = resultPtr;
                    while (window.uPiperOpenJTalk.HEAPU8[i]) {
                        str += String.fromCharCode(window.uPiperOpenJTalk.HEAPU8[i++]);
                    }
                    labels = str;
                } else {
                    labels = openJTalkUTF8ToString(resultPtr);
                }
                window.uPiperOpenJTalk._openjtalk_free_string(resultPtr);
                
                console.log('[uPiper] OpenJTalk synthesis_labels returned:', labels.substring(0, 200));
                
                // Process labels to extract phonemes
                var lines = labels.split('\n').filter(function(line) { return line.trim(); });
                var phonemes = ['^']; // BOS marker
                
                lines.forEach(function(line) {
                    var match = line.match(/\-([^+]+)\+/);
                    if (match && match[1] !== 'sil') {
                        var phoneme = match[1];
                        if (phoneme !== 'pau') {
                            phonemes.push(phoneme);
                        }
                    }
                });
                
                phonemes.push('$'); // EOS marker
                console.log('[uPiper] Extracted phonemes from OpenJTalk:', phonemes);
                
                // Apply multi-character phoneme mapping
                var multiCharPhonemes = {
                    'ch': '\ue001',
                    'ky': '\ue006',
                    'ny': '\ue008',
                    'ry': '\ue00a',
                    'sh': '\ue00b',
                    'ts': '\ue00c'
                };
                
                var finalPhonemes = [];
                for (var i = 0; i < phonemes.length; i++) {
                    if (multiCharPhonemes[phonemes[i]]) {
                        finalPhonemes.push(multiCharPhonemes[phonemes[i]]);
                    } else {
                        finalPhonemes.push(phonemes[i]);
                    }
                }
                
                var result = JSON.stringify({
                    success: true,
                    phonemes: finalPhonemes
                });
                
                var bufferSize = lengthBytesUTF8(result) + 1;
                var buffer = _malloc(bufferSize);
                stringToUTF8(result, buffer, bufferSize);
                return buffer;
                
            } else {
                // Fallback: Use simplified phonemization  
                openJTalkFree(textPtr);
                console.log('[uPiper] Using simplified Japanese phonemization for:', text);
            
            // Simple Japanese to phoneme conversion
            // This is a very basic implementation for demonstration
            var phonemeMap = {
                'あ': 'a', 'い': 'i', 'う': 'u', 'え': 'e', 'お': 'o',
                'か': 'ka', 'き': 'ki', 'く': 'ku', 'け': 'ke', 'こ': 'ko',
                'さ': 'sa', 'し': 'shi', 'す': 'su', 'せ': 'se', 'そ': 'so',
                'た': 'ta', 'ち': 'chi', 'つ': 'tsu', 'て': 'te', 'と': 'to',
                'な': 'na', 'に': 'ni', 'ぬ': 'nu', 'ね': 'ne', 'の': 'no',
                'は': 'ha', 'ひ': 'hi', 'ふ': 'fu', 'へ': 'he', 'ほ': 'ho',
                'ま': 'ma', 'み': 'mi', 'む': 'mu', 'め': 'me', 'も': 'mo',
                'や': 'ya', 'ゆ': 'yu', 'よ': 'yo',
                'ら': 'ra', 'り': 'ri', 'る': 'ru', 'れ': 're', 'ろ': 'ro',
                'わ': 'wa', 'を': 'wo', 'ん': 'n',
                'が': 'ga', 'ぎ': 'gi', 'ぐ': 'gu', 'げ': 'ge', 'ご': 'go',
                'ざ': 'za', 'じ': 'ji', 'ず': 'zu', 'ぜ': 'ze', 'ぞ': 'zo',
                'だ': 'da', 'ぢ': 'ji', 'づ': 'zu', 'で': 'de', 'ど': 'do',
                'ば': 'ba', 'び': 'bi', 'ぶ': 'bu', 'べ': 'be', 'ぼ': 'bo',
                'ぱ': 'pa', 'ぴ': 'pi', 'ぷ': 'pu', 'ぺ': 'pe', 'ぽ': 'po'
            };
            
            // Convert text to phonemes
            var phonemes = ['^']; // BOS marker
            
            for (var i = 0; i < text.length; i++) {
                var char = text[i];
                var phoneme = phonemeMap[char];
                
                if (phoneme) {
                    // Split multi-character phonemes
                    for (var j = 0; j < phoneme.length; j++) {
                        phonemes.push(phoneme[j]);
                    }
                } else if (char === 'こ') {
                    phonemes.push('k');
                    phonemes.push('o');
                } else if (char === 'ん') {
                    phonemes.push('N');
                } else if (char === 'に') {
                    phonemes.push('n');
                    phonemes.push('i');
                } else if (char === 'ち') {
                    phonemes.push('ch');
                    phonemes.push('i');
                } else if (char === 'は' || char === 'わ') {
                    phonemes.push('w');
                    phonemes.push('a');
                }
            }
            
            phonemes.push('$'); // EOS marker
            
            // Apply multi-character phoneme mapping for Piper
            var multiCharPhonemes = {
                'ch': '\ue001',
                'ky': '\ue006',
                'ny': '\ue008',
                'ry': '\ue00a',
                'sh': '\ue00b',
                'ts': '\ue00c'
            };
            
            // Replace multi-character phonemes
            var finalPhonemes = [];
            for (var i = 0; i < phonemes.length; i++) {
                if (i < phonemes.length - 1) {
                    var twoChar = phonemes[i] + phonemes[i+1];
                    if (multiCharPhonemes[twoChar]) {
                        finalPhonemes.push(multiCharPhonemes[twoChar]);
                        i++; // Skip next character
                        continue;
                    }
                }
                finalPhonemes.push(phonemes[i]);
            }
            
            console.log('[uPiper] Final phonemes:', finalPhonemes);
            
            var result = JSON.stringify({
                success: true,
                phonemes: finalPhonemes
            });
            
            var bufferSize = lengthBytesUTF8(result) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(result, buffer, bufferSize);
            return buffer;
            }
            
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
