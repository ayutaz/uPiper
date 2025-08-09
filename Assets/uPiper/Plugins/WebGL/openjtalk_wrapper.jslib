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
                        
                        // Set up proxy for HEAP arrays before loading the module
                        // This prevents "HEAP8 not exported" errors during module initialization
                        const heapNames = ['HEAP8', 'HEAPU8', 'HEAP16', 'HEAPU16', 'HEAP32', 'HEAPU32', 'HEAPF32', 'HEAPF64'];
                        const originalValues = {};
                        
                        heapNames.forEach(name => {
                            if (typeof window[name] !== 'undefined') {
                                originalValues[name] = window[name];
                            }
                            
                            // Create a getter that returns a dummy array initially
                            Object.defineProperty(window, name, {
                                configurable: true,
                                get: function() {
                                    // Return a dummy array if the real one isn't available yet
                                    if (window['_real_' + name]) {
                                        return window['_real_' + name];
                                    }
                                    return new (name.includes('F') ? Float32Array : 
                                           name.includes('U') ? Uint8Array : 
                                           name.includes('16') ? Int16Array :
                                           name.includes('32') ? Int32Array : Int8Array)(0);
                                },
                                set: function(value) {
                                    window['_real_' + name] = value;
                                }
                            });
                        });
                        
                        // Load OpenJTalk module safely using multiple strategies
                        console.log('[uPiper] Loading OpenJTalk ES6 module...');
                        
                        // Strategy 1: Try using iframe to load ES6 module safely
                        let moduleLoaded = false;
                        
                        try {
                            // Create hidden iframe to load the module
                            const iframe = document.createElement('iframe');
                            iframe.style.display = 'none';
                            iframe.src = 'StreamingAssets/openjtalk-loader.html';
                            
                            // Wait for the module to load in iframe
                            await new Promise((resolve, reject) => {
                                const timeout = setTimeout(() => {
                                    reject(new Error('Iframe loading timeout'));
                                }, 5000);
                                
                                window.addEventListener('message', function handler(e) {
                                    if (e.data && e.data.type === 'openjtalk-loaded') {
                                        clearTimeout(timeout);
                                        window.removeEventListener('message', handler);
                                        
                                        // Get the module from iframe
                                        if (iframe.contentWindow && iframe.contentWindow.OpenJTalkModule) {
                                            window.OpenJTalkModule = iframe.contentWindow.OpenJTalkModule;
                                            moduleLoaded = true;
                                            console.log('[uPiper] OpenJTalk loaded via iframe');
                                        }
                                        resolve();
                                    }
                                });
                                
                                document.body.appendChild(iframe);
                            });
                        } catch (iframeError) {
                            console.warn('[uPiper] Iframe loading failed:', iframeError);
                        }
                        
                        // Strategy 2: Fallback to fetch and modify (if iframe fails)
                        if (!moduleLoaded) {
                            try {
                                const response = await fetch('StreamingAssets/openjtalk.js');
                                let jsText = await response.text();
                                console.log('[uPiper] Fetched OpenJTalk module, size:', jsText.length);
                                
                                // Fix the scriptDirectory issue first
                                // Replace the problematic scriptDirectory assignment
                                jsText = jsText.replace(
                                    /scriptDirectory=new URL\("\.",_scriptName\)\.href/g,
                                    'scriptDirectory=""'  // Empty string so it doesn't add extra path
                                );
                                
                                // More robust replacement - handle both minified and formatted versions
                                if (jsText.includes('export default OpenJTalkModule')) {
                                    jsText = jsText.replace(/export\s+default\s+OpenJTalkModule[;\s]*$/, 'window.OpenJTalkModule = OpenJTalkModule;');
                                } else if (jsText.includes('export{')) {
                                    // Handle minified export format
                                    jsText = jsText.replace(/export\{[^}]*OpenJTalkModule[^}]*\}/, 'window.OpenJTalkModule = OpenJTalkModule');
                                }
                                
                                // Don't wrap in IIFE as it might break import.meta references
                                const wrappedJs = jsText;
                                
                                // Create script element and execute
                                const scriptTag = document.createElement('script');
                                scriptTag.textContent = wrappedJs;
                                document.head.appendChild(scriptTag);
                                
                                // Wait for execution
                                await new Promise(resolve => setTimeout(resolve, 200));
                                
                                if (typeof window.OpenJTalkModule === 'function') {
                                    moduleLoaded = true;
                                    console.log('[uPiper] OpenJTalk loaded via script injection');
                                }
                            } catch (fetchError) {
                                console.error('[uPiper] Fetch and modify failed:', fetchError);
                            }
                        }
                        
                        if (!moduleLoaded) {
                            throw new Error('Failed to load OpenJTalk module with all strategies');
                        }
                        
                        console.log('[uPiper] OpenJTalkModule function loaded successfully');
                        
                        // Module has been loaded and is available as window.OpenJTalkModule
                        
                        if (typeof window.OpenJTalkModule === 'undefined') {
                            throw new Error('OpenJTalkModule not found after dynamic import');
                        }
                        
                        console.log('[uPiper] OpenJTalkModule loaded, initializing...');
                        
                        // Initialize the module with required exports
                        // We need to ensure HEAP arrays are accessible
                        const moduleConfig = {
                            locateFile: function(path) {
                                console.log('[uPiper] locateFile called with:', path);
                                if (path.endsWith('.wasm')) {
                                    // Fix the path based on current location
                                    // Check if we're already in StreamingAssets context
                                    const currentPath = window.location.pathname;
                                    if (currentPath.includes('/StreamingAssets/')) {
                                        // We're in iframe context, use relative path
                                        return 'openjtalk.wasm';
                                    } else {
                                        // We're in main context, use StreamingAssets path
                                        return 'StreamingAssets/openjtalk.wasm';
                                    }
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
                        // OpenJTalkModule might be the module itself or a function to create the module
                        let module;
                        if (typeof window.OpenJTalkModule === 'function') {
                            console.log('[uPiper] OpenJTalkModule is a function, calling it...');
                            module = await window.OpenJTalkModule(moduleConfig);
                        } else if (window.OpenJTalkModule && window.OpenJTalkModule.default) {
                            console.log('[uPiper] OpenJTalkModule has default export, using it...');
                            if (typeof window.OpenJTalkModule.default === 'function') {
                                module = await window.OpenJTalkModule.default(moduleConfig);
                            } else {
                                module = window.OpenJTalkModule.default;
                            }
                        } else {
                            console.log('[uPiper] OpenJTalkModule is already initialized');
                            module = window.OpenJTalkModule;
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
                        
                        // Clean up the proxy definitions and restore original values
                        heapNames.forEach(name => {
                            // Get the real value if it was set
                            const realValue = window['_real_' + name];
                            delete window['_real_' + name];
                            
                            // Remove the property descriptor
                            delete window[name];
                            
                            // If there was a real value, set it directly
                            if (realValue) {
                                window[name] = realValue;
                            } else if (originalValues[name]) {
                                // Restore original value if there was one
                                window[name] = originalValues[name];
                            }
                        });
                        
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
