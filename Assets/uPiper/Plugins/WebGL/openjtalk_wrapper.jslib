mergeInto(LibraryManager.library, {
    // Initialize OpenJTalk WebAssembly module
    InitializeOpenJTalkWeb: function() {
        console.log('[uPiper] Initializing OpenJTalk WebAssembly...');
        
        // Check if wasm_open_jtalk is available
        if (typeof window.wasmOpenJTalk === 'undefined') {
            console.error('[uPiper] wasm_open_jtalk is not loaded. Please include it in your HTML template.');
            return false;
        }
        
        // Initialize the module
        try {
            if (window.wasmOpenJTalk.isInitialized) {
                console.log('[uPiper] OpenJTalk WebAssembly already initialized');
                return true;
            }
            
            // Mark as initialized
            window.wasmOpenJTalk.isInitialized = true;
            console.log('[uPiper] OpenJTalk WebAssembly initialized successfully');
            return true;
        } catch (error) {
            console.error('[uPiper] Failed to initialize OpenJTalk WebAssembly:', error);
            return false;
        }
    },
    
    // Load dictionary data for OpenJTalk
    LoadOpenJTalkDictionary: function(dictionaryDataPtr, dataLength) {
        console.log('[uPiper] Loading OpenJTalk dictionary...');
        
        if (typeof window.wasmOpenJTalk === 'undefined' || !window.wasmOpenJTalk.isInitialized) {
            console.error('[uPiper] OpenJTalk WebAssembly not initialized');
            return false;
        }
        
        try {
            // Convert pointer to Uint8Array
            var dictionaryData = new Uint8Array(Module.HEAPU8.buffer, dictionaryDataPtr, dataLength);
            
            // Load dictionary (implementation depends on wasm_open_jtalk API)
            // This is a placeholder - actual implementation will depend on the library
            console.log('[uPiper] Dictionary data received, length:', dataLength);
            
            return true;
        } catch (error) {
            console.error('[uPiper] Failed to load dictionary:', error);
            return false;
        }
    },
    
    // Convert Japanese text to phonemes
    PhonemizeJapaneseText: function(textPtr) {
        var text = UTF8ToString(textPtr);
        console.log('[uPiper] Phonemizing Japanese text:', text);
        
        if (typeof window.wasmOpenJTalk === 'undefined' || !window.wasmOpenJTalk.isInitialized) {
            console.error('[uPiper] OpenJTalk WebAssembly not initialized');
            var errorResult = JSON.stringify({
                success: false,
                error: 'OpenJTalk not initialized',
                phonemes: []
            });
            var bufferSize = lengthBytesUTF8(errorResult) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
        
        try {
            // Placeholder for actual phonemization
            // In real implementation, this would call wasm_open_jtalk.phonemize(text)
            var phonemes = ['k', 'o', 'N', 'n', 'i', 'ch', 'i', 'w', 'a'];
            
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
            var bufferSize = lengthBytesUTF8(errorResult) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
    },
    
    // Free allocated memory
    FreeWebGLMemory: function(ptr) {
        _free(ptr);
    }
});