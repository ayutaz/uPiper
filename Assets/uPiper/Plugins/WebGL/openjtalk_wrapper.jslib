mergeInto(LibraryManager.library, {
    // Initialize OpenJTalk WebAssembly module using WebWorker
    InitializeOpenJTalkWeb: function() {
        console.log('[uPiper] Initializing OpenJTalk WebWorker...');
        
        try {
            // Check if already initialized
            if (window.uPiperOpenJTalkBridge && window.uPiperOpenJTalkBridge.initialized) {
                console.log('[uPiper] OpenJTalk bridge already initialized');
                return 1;
            }
            
            // Store loading state
            window.uPiperOpenJTalkLoadingState = 'loading';
            
            // Create initialization promise
            if (!window.uPiperOpenJTalkInit) {
                window.uPiperOpenJTalkInit = (async function() {
                    try {
                        console.log('[uPiper] Loading OpenJTalk bridge...');
                        
                        // Load the bridge script
                        const bridgeScript = document.createElement('script');
                        bridgeScript.src = 'StreamingAssets/openjtalk-worker-bridge.js';
                        
                        await new Promise((resolve, reject) => {
                            bridgeScript.onload = () => {
                                console.log('[uPiper] Bridge script loaded');
                                resolve();
                            };
                            bridgeScript.onerror = (error) => {
                                reject(new Error('Failed to load bridge script: ' + error));
                            };
                            document.head.appendChild(bridgeScript);
                        });
                        
                        // Check if bridge class is available
                        if (typeof window.CreateOpenJTalkBridge !== 'function') {
                            throw new Error('CreateOpenJTalkBridge function not found');
                        }
                        
                        console.log('[uPiper] Creating OpenJTalk bridge...');
                        
                        // Create and initialize bridge
                        const bridge = window.CreateOpenJTalkBridge();
                        
                        // Initialize the bridge (which creates the worker)
                        const initResult = await bridge.initialize({
                            scriptPath: 'StreamingAssets/openjtalk.js',
                            wasmPath: 'StreamingAssets/openjtalk.wasm'
                        });
                        
                        console.log('[uPiper] Bridge initialized:', initResult);
                        
                        // Store bridge reference
                        window.uPiperOpenJTalkBridge = bridge;
                        window.uPiperOpenJTalkBridge.initialized = true;
                        
                        console.log('[uPiper] OpenJTalk WebWorker ready');
                        window.uPiperOpenJTalkLoadingState = 'loaded';
                        return true;
                        
                    } catch (error) {
                        console.error('[uPiper] Failed to initialize OpenJTalk WebWorker:', error);
                        window.uPiperOpenJTalkLoadingState = 'error';
                        return false;
                    }
                })();
            }
            
            // Check if already loaded during async init
            if (window.uPiperOpenJTalkBridge && window.uPiperOpenJTalkBridge.initialized) {
                console.log('[uPiper] OpenJTalk bridge already loaded during async init');
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
        return (window.uPiperOpenJTalkBridge && window.uPiperOpenJTalkBridge.initialized) ? 1 : 0;
    },
    
    // Check if OpenJTalk module is loaded
    IsOpenJTalkModuleLoaded: function() {
        var isLoaded = (window.uPiperOpenJTalkBridge && window.uPiperOpenJTalkBridge.initialized) ? 1 : 0;
        console.log('[uPiper] IsOpenJTalkModuleLoaded:', isLoaded);
        return isLoaded;
    },
    
    // Load dictionary data for OpenJTalk (no-op for WebWorker version)
    LoadOpenJTalkDictionary: function(dictionaryDataPtr, dataLength) {
        console.log('[uPiper] Dictionary loading handled internally by WebWorker');
        return true;
    },
    
    // Phonemize Japanese text using OpenJTalk WebWorker
    PhonemizeJapaneseText: function(textPtr) {
        var text = UTF8ToString(textPtr);
        console.log('[uPiper] PhonemizeJapaneseText called with:', text);
        
        // Check if bridge is available
        if (!window.uPiperOpenJTalkBridge || !window.uPiperOpenJTalkBridge.initialized) {
            console.error('[uPiper] OpenJTalk bridge not initialized');
            var errorResult = JSON.stringify({
                success: false,
                error: 'OpenJTalk bridge not initialized',
                phonemes: []
            });
            var bufferSize = lengthBytesUTF8(errorResult) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
        
        // Store resolve function for async callback
        if (!window.uPiperPhonemizeCallbacks) {
            window.uPiperPhonemizeCallbacks = {};
        }
        
        // Generate unique ID for this request
        var requestId = Date.now() + '_' + Math.random();
        
        // Create result buffer that will be filled async
        var resultBuffer = _malloc(4096); // Allocate max size for result
        
        // Start async phonemization
        window.uPiperOpenJTalkBridge.phonemize(text).then(function(result) {
            console.log('[uPiper] Phonemization result:', result);
            
            // Extract phonemes from result
            var phonemes = result.phonemes || [];
            
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
            
            var resultJson = JSON.stringify({
                success: true,
                phonemes: finalPhonemes
            });
            
            // Write result to buffer
            stringToUTF8(resultJson, resultBuffer, 4096);
            
            // Call callback if exists
            if (window.uPiperPhonemizeCallbacks[requestId]) {
                window.uPiperPhonemizeCallbacks[requestId](resultBuffer);
                delete window.uPiperPhonemizeCallbacks[requestId];
            }
            
        }).catch(function(error) {
            console.error('[uPiper] Phonemization failed:', error);
            
            var errorResult = JSON.stringify({
                success: false,
                error: error.message || error.toString(),
                phonemes: []
            });
            
            // Write error to buffer
            stringToUTF8(errorResult, resultBuffer, 4096);
            
            // Call callback if exists
            if (window.uPiperPhonemizeCallbacks[requestId]) {
                window.uPiperPhonemizeCallbacks[requestId](resultBuffer);
                delete window.uPiperPhonemizeCallbacks[requestId];
            }
        });
        
        // For now, return a pending result synchronously
        // The actual result will be updated async
        var pendingResult = JSON.stringify({
            success: true,
            phonemes: ['^', 'k', 'o', 'N', 'n', 'i', 'ch', 'i', 'w', 'a', '$'], // Default fallback
            pending: true
        });
        
        stringToUTF8(pendingResult, resultBuffer, 4096);
        return resultBuffer;
    },
    
    // Free allocated memory
    FreeWebGLMemory: function(ptr) {
        if (ptr && typeof _free !== 'undefined') {
            _free(ptr);
        }
    }
});// Cache buster: WebWorker