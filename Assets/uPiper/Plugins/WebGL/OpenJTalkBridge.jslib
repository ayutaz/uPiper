mergeInto(LibraryManager.library, {
    OpenJTalk_Initialize: function(callbackPtr) {
        console.log('[OpenJTalkBridge] Initializing OpenJTalk WASM...');
        
        // Initialize OpenJTalk WASM
        if (typeof window.OpenJTalkWrapper === 'undefined') {
            console.error('[OpenJTalkBridge] OpenJTalkWrapper not found. Make sure openjtalk-unity-wrapper.js is loaded.');
            Module.dynCall_vi(callbackPtr, 0);
            return;
        }
        
        window.OpenJTalkWrapper.initialize().then(function(success) {
            console.log('[OpenJTalkBridge] OpenJTalk initialized:', success);
            Module.dynCall_vi(callbackPtr, success ? 1 : 0);
        }).catch(function(error) {
            console.error('[OpenJTalkBridge] Failed to initialize OpenJTalk:', error);
            Module.dynCall_vi(callbackPtr, 0);
        });
    },
    
    OpenJTalk_Phonemize: function(textPtr, callbackPtr) {
        var text = UTF8ToString(textPtr);
        console.log('[OpenJTalkBridge] Phonemizing text:', text);
        
        if (typeof window.OpenJTalkWrapper === 'undefined' || !window.OpenJTalkWrapper.isInitialized()) {
            console.error('[OpenJTalkBridge] OpenJTalk not initialized');
            var emptyStr = _malloc(1);
            HEAP8[emptyStr] = 0;
            Module.dynCall_vii(callbackPtr, 0, emptyStr);
            return;
        }
        
        window.OpenJTalkWrapper.phonemize(text).then(function(phonemes) {
            console.log('[OpenJTalkBridge] Phonemization result:', phonemes);
            
            if (phonemes && phonemes.length > 0) {
                // Join phonemes with space
                var phonemeStr = phonemes.join(' ');
                var bufferSize = lengthBytesUTF8(phonemeStr) + 1;
                var buffer = _malloc(bufferSize);
                stringToUTF8(phonemeStr, buffer, bufferSize);
                Module.dynCall_vii(callbackPtr, 1, buffer);
            } else {
                var emptyStr = _malloc(1);
                HEAP8[emptyStr] = 0;
                Module.dynCall_vii(callbackPtr, 0, emptyStr);
            }
        }).catch(function(error) {
            console.error('[OpenJTalkBridge] Phonemization failed:', error);
            var emptyStr = _malloc(1);
            HEAP8[emptyStr] = 0;
            Module.dynCall_vii(callbackPtr, 0, emptyStr);
        });
    },
    
    OpenJTalk_Dispose: function() {
        console.log('[OpenJTalkBridge] Disposing OpenJTalk...');
        if (typeof window.OpenJTalkWrapper !== 'undefined') {
            window.OpenJTalkWrapper.dispose();
        }
    }
});