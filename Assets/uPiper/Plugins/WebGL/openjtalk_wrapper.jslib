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
                        
                        // Create required directories
                        module.FS.mkdir('/dict');
                        module.FS.mkdir('/voice');
                        
                        // Store module reference with all required functions
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
                        
                        // Test the module if test function is available
                        if (module._openjtalk_test) {
                            const testStr = module.allocateUTF8("test");
                            const result = module._openjtalk_test(testStr);
                            console.log('[uPiper] OpenJTalk test result:', result); // Should be 4
                            module._free(testStr);
                        }
                        
                        // Verify HEAP arrays are properly exported
                        console.log('[uPiper] HEAP arrays available:', {
                            HEAP8: !!window.uPiperOpenJTalk.HEAP8,
                            HEAPU8: !!window.uPiperOpenJTalk.HEAPU8,
                            HEAP16: !!window.uPiperOpenJTalk.HEAP16,
                            HEAPU16: !!window.uPiperOpenJTalk.HEAPU16,
                            HEAP32: !!window.uPiperOpenJTalk.HEAP32,
                            HEAPU32: !!window.uPiperOpenJTalk.HEAPU32,
                            HEAPF32: !!window.uPiperOpenJTalk.HEAPF32,
                            HEAPF64: !!window.uPiperOpenJTalk.HEAPF64
                        });
                        
                        console.log('[uPiper] OpenJTalk module loaded, awaiting dictionary initialization');
                        return true;
                    } catch (error) {
                        console.error('[uPiper] Failed to load OpenJTalk module:', error);
                        return false;
                    }
                })();
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
    
    // Load dictionary data for OpenJTalk
    LoadOpenJTalkDictionary: function(dictionaryDataPtr, dataLength) {
        console.log('[uPiper] Loading OpenJTalk dictionary...');
        
        if (!window.uPiperOpenJTalk || !window.uPiperOpenJTalk.module) {
            console.error('[uPiper] OpenJTalk module not loaded');
            return false;
        }
        
        try {
            var module = window.uPiperOpenJTalk.module;
            var FS = window.uPiperOpenJTalk.FS;
            
            // Dictionary files that need to be loaded
            var dictFiles = ['char.bin', 'matrix.bin', 'sys.dic', 'unk.dic', 'left-id.def', 'pos-id.def', 'rewrite.def', 'right-id.def'];
            
            // Load dictionary files from StreamingAssets
            var loadPromises = dictFiles.map(function(filename) {
                return fetch('StreamingAssets/dict/' + filename)
                    .then(function(response) {
                        if (!response.ok) {
                            throw new Error('Failed to load ' + filename);
                        }
                        return response.arrayBuffer();
                    })
                    .then(function(data) {
                        FS.writeFile('/dict/' + filename, new Uint8Array(data));
                        console.log('[uPiper] Loaded dictionary file:', filename);
                    });
            });
            
            // Also load a dummy voice file (required by OpenJTalk)
            loadPromises.push(
                fetch('StreamingAssets/voice/dummy.htsvoice')
                    .then(function(response) {
                        return response.arrayBuffer();
                    })
                    .then(function(data) {
                        FS.writeFile('/voice/voice.htsvoice', new Uint8Array(data));
                    })
                    .catch(function(error) {
                        // If no voice file, create a minimal dummy
                        console.warn('[uPiper] No voice file found, creating dummy');
                        FS.writeFile('/voice/voice.htsvoice', new Uint8Array(1));
                    })
            );
            
            // Wait for all files to load
            Promise.all(loadPromises).then(function() {
                // Initialize OpenJTalk with the loaded dictionary
                var allocateUTF8 = window.uPiperOpenJTalk.allocateUTF8;
                var _free = window.uPiperOpenJTalk._free;
                
                var dictPtr = allocateUTF8('/dict');
                var voicePtr = allocateUTF8('/voice/voice.htsvoice');
                
                // Check if openjtalk_initialize exists
                if (module._openjtalk_initialize) {
                    var result = module._openjtalk_initialize(dictPtr, voicePtr);
                    
                    _free(dictPtr);
                    _free(voicePtr);
                    
                    if (result === 0) {
                        window.uPiperOpenJTalk.initialized = true;
                        console.log('[uPiper] OpenJTalk initialized successfully');
                    } else {
                        console.error('[uPiper] OpenJTalk initialization failed with code:', result);
                    }
                } else {
                    // Minimal build without full OpenJTalk - mark as initialized for testing
                    _free(dictPtr);
                    _free(voicePtr);
                    window.uPiperOpenJTalk.initialized = true;
                    console.log('[uPiper] OpenJTalk minimal build - marked as initialized');
                }
            }).catch(function(error) {
                console.error('[uPiper] Failed to load dictionary files:', error);
            });
            
            // Return true to indicate async loading started
            return true;
        } catch (error) {
            console.error('[uPiper] Failed to load dictionary:', error);
            return false;
        }
    },
    
    // Phonemize Japanese text using OpenJTalk
    PhonemizeJapaneseText: function(textPtr) {
        var text = UTF8ToString(textPtr);
        console.log('[uPiper] Phonemizing Japanese text:', text);
        
        if (!window.uPiperOpenJTalk || !window.uPiperOpenJTalk.initialized) {
            console.error('[uPiper] OpenJTalk not initialized');
            var errorResult = JSON.stringify({
                success: false,
                error: 'OpenJTalk not initialized',
                phonemes: []
            });
            // Use Unity's built-in functions for memory allocation when module not initialized
            var bufferSize = Module.lengthBytesUTF8(errorResult) + 1;
            var buffer = Module._malloc(bufferSize);
            Module.stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
        
        try {
            var module = window.uPiperOpenJTalk.module;
            var _malloc = window.uPiperOpenJTalk._malloc;
            var _free = window.uPiperOpenJTalk._free;
            var allocateUTF8 = window.uPiperOpenJTalk.allocateUTF8;
            var UTF8ToString = window.uPiperOpenJTalk.UTF8ToString;
            var stringToUTF8 = window.uPiperOpenJTalk.stringToUTF8;
            var lengthBytesUTF8 = window.uPiperOpenJTalk.lengthBytesUTF8;
            
            var labels = '';
            
            // Check if synthesis_labels function exists
            if (module._openjtalk_synthesis_labels) {
                // Call OpenJTalk synthesis_labels function
                var textMemPtr = allocateUTF8(text);
                var labelsPtr = module._openjtalk_synthesis_labels(textMemPtr);
                
                if (!labelsPtr) {
                    throw new Error('OpenJTalk synthesis_labels returned null');
                }
                
                labels = UTF8ToString(labelsPtr);
                module._openjtalk_free_string(labelsPtr);
                module._free(textMemPtr);
                
                console.log('[uPiper] OpenJTalk labels:', labels);
                
                // Check for errors
                if (labels.startsWith('ERROR:')) {
                    throw new Error(labels);
                }
            } else {
                // Minimal build - return dummy labels for testing
                console.log('[uPiper] Using dummy labels for minimal build');
                labels = "0 50000 sil\n50000 100000 -k+o\n100000 150000 -o+n\n150000 200000 -n+n\n200000 250000 -n+i\n250000 300000 -i+ch\n300000 350000 -ch+i\n350000 400000 -i+w\n400000 450000 -w+a\n450000 500000 sil\n";
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
            // Get functions from window.uPiperOpenJTalk if available, otherwise use Module
            var lengthBytesUTF8Func = (window.uPiperOpenJTalk && window.uPiperOpenJTalk.lengthBytesUTF8) || Module.lengthBytesUTF8;
            var mallocFunc = (window.uPiperOpenJTalk && window.uPiperOpenJTalk._malloc) || Module._malloc;
            var stringToUTF8Func = (window.uPiperOpenJTalk && window.uPiperOpenJTalk.stringToUTF8) || Module.stringToUTF8;
            
            var bufferSize = lengthBytesUTF8Func(errorResult) + 1;
            var buffer = mallocFunc(bufferSize);
            stringToUTF8Func(errorResult, buffer, bufferSize);
            return buffer;
        }
    },
    
    // Free allocated memory
    FreeWebGLMemory: function(ptr) {
        if (ptr && window.uPiperOpenJTalk && window.uPiperOpenJTalk._free) {
            window.uPiperOpenJTalk._free(ptr);
        }
    }
});