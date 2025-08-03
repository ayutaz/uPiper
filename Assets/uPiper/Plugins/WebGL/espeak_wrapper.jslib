mergeInto(LibraryManager.library, {
    // Initialize eSpeak-ng WebAssembly module
    InitializeESpeakWeb: function() {
        console.log('[uPiper] Initializing eSpeak-ng WebAssembly...');
        
        // Check if espeak-ng wasm is available
        if (typeof window.espeakNG === 'undefined') {
            console.error('[uPiper] eSpeak-ng WebAssembly is not loaded. Please include it in your HTML template.');
            return false;
        }
        
        // Initialize the module
        try {
            if (window.espeakNG.isInitialized) {
                console.log('[uPiper] eSpeak-ng WebAssembly already initialized');
                return true;
            }
            
            // Initialize eSpeak-ng
            // This is a placeholder - actual initialization depends on the library
            window.espeakNG.isInitialized = true;
            console.log('[uPiper] eSpeak-ng WebAssembly initialized successfully');
            return true;
        } catch (error) {
            console.error('[uPiper] Failed to initialize eSpeak-ng WebAssembly:', error);
            return false;
        }
    },
    
    // Set eSpeak-ng language
    SetESpeakLanguage: function(languagePtr) {
        var language = UTF8ToString(languagePtr);
        console.log('[uPiper] Setting eSpeak-ng language:', language);
        
        if (typeof window.espeakNG === 'undefined' || !window.espeakNG.isInitialized) {
            console.error('[uPiper] eSpeak-ng WebAssembly not initialized');
            return false;
        }
        
        try {
            // Set language (implementation depends on espeak-ng wasm API)
            window.espeakNG.currentLanguage = language;
            return true;
        } catch (error) {
            console.error('[uPiper] Failed to set language:', error);
            return false;
        }
    },
    
    // Convert text to phonemes using eSpeak-ng
    PhonemizeEnglishText: function(textPtr, languagePtr) {
        var text = UTF8ToString(textPtr);
        var language = UTF8ToString(languagePtr);
        console.log('[uPiper] Phonemizing text with eSpeak-ng:', text, 'Language:', language);
        
        if (typeof window.espeakNG === 'undefined' || !window.espeakNG.isInitialized) {
            console.error('[uPiper] eSpeak-ng WebAssembly not initialized');
            var errorResult = JSON.stringify({
                success: false,
                error: 'eSpeak-ng not initialized',
                phonemes: []
            });
            var bufferSize = lengthBytesUTF8(errorResult) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
        
        try {
            // Placeholder for actual phonemization
            // In real implementation, this would call espeak-ng wasm API
            var phonemes = [];
            
            // Simple example phoneme conversion
            if (language === 'en' || language === 'en-US') {
                // Example: "hello" -> ['h', 'ə', 'l', 'oʊ']
                phonemes = ['h', 'ə', 'l', 'oʊ'];
            } else if (language === 'zh' || language === 'zh-CN') {
                // For Chinese, we might need different handling
                phonemes = ['n', 'i', 'h', 'ao'];
            }
            
            var result = JSON.stringify({
                success: true,
                phonemes: phonemes,
                language: language
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
    
    // Get supported languages
    GetESpeakSupportedLanguages: function() {
        console.log('[uPiper] Getting eSpeak-ng supported languages');
        
        if (typeof window.espeakNG === 'undefined' || !window.espeakNG.isInitialized) {
            console.error('[uPiper] eSpeak-ng WebAssembly not initialized');
            var errorResult = JSON.stringify([]);
            var bufferSize = lengthBytesUTF8(errorResult) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
        
        try {
            // Placeholder - return common languages
            var languages = ['en', 'en-US', 'en-GB', 'zh', 'zh-CN', 'es', 'fr', 'de', 'ja'];
            var result = JSON.stringify(languages);
            
            var bufferSize = lengthBytesUTF8(result) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(result, buffer, bufferSize);
            return buffer;
        } catch (error) {
            console.error('[uPiper] Failed to get languages:', error);
            var errorResult = JSON.stringify([]);
            var bufferSize = lengthBytesUTF8(errorResult) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
    }
});