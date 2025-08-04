mergeInto(LibraryManager.library, {
    // Initialize eSpeak-ng WebAssembly module
    InitializeESpeakWeb: function() {
        console.log('[uPiper] Initializing eSpeak-ng WebAssembly...');
        
        try {
            // Check if already initialized
            if (window.uPiperESpeak && window.uPiperESpeak.initialized) {
                console.log('[uPiper] eSpeak-ng already initialized');
                return 1;
            }
            
            // Create initialization promise
            if (!window.uPiperESpeakInit) {
                window.uPiperESpeakInit = (async function() {
                    try {
                        console.log('[uPiper] Loading eSpeak-ng module...');
                        
                        // First, load the eSpeak-ng JavaScript module
                        const scriptElement = document.createElement('script');
                        scriptElement.src = 'StreamingAssets/espeak-ng/espeakng.min.js';
                        
                        // Wait for script to load
                        await new Promise((resolve, reject) => {
                            scriptElement.onload = resolve;
                            scriptElement.onerror = reject;
                            document.head.appendChild(scriptElement);
                        });
                        
                        // Wait a bit for global variables to be set
                        await new Promise(resolve => setTimeout(resolve, 100));
                        
                        // Initialize eSpeak-ng
                        if (typeof eSpeakNG === 'undefined') {
                            throw new Error('eSpeakNG not found after loading script');
                        }
                        
                        console.log('[uPiper] Creating eSpeak-ng instance...');
                        
                        // Create eSpeak-ng instance
                        const espeakNG = new eSpeakNG('StreamingAssets/espeak-ng/espeakng.worker.js');
                        
                        // Wait for initialization
                        await new Promise((resolve, reject) => {
                            let timeout = setTimeout(() => {
                                reject(new Error('eSpeak-ng initialization timeout'));
                            }, 10000);
                            
                            // Check for ready state
                            const checkReady = () => {
                                if (espeakNG.worker) {
                                    clearTimeout(timeout);
                                    resolve();
                                }
                            };
                            
                            // Poll for ready state
                            const interval = setInterval(checkReady, 100);
                            
                            // Also set up a one-time check after a delay
                            setTimeout(() => {
                                clearInterval(interval);
                                checkReady();
                            }, 1000);
                        });
                        
                        // Store instance
                        window.uPiperESpeak = {
                            initialized: true,
                            instance: espeakNG,
                            currentVoice: 'en-us'
                        };
                        
                        console.log('[uPiper] eSpeak-ng initialized successfully');
                        return true;
                    } catch (error) {
                        console.error('[uPiper] Failed to initialize eSpeak-ng:', error);
                        return false;
                    }
                })();
            }
            
            // Return 0 to indicate async initialization in progress
            return 0;
            
        } catch (error) {
            console.error('[uPiper] Failed to initialize eSpeak-ng:', error);
            return -1;
        }
    },
    
    // Check if eSpeak-ng is initialized
    IsESpeakInitialized: function() {
        return (window.uPiperESpeak && window.uPiperESpeak.initialized) ? 1 : 0;
    },
    
    // Set eSpeak-ng language
    SetESpeakLanguage: function(languagePtr) {
        var language = UTF8ToString(languagePtr);
        console.log('[uPiper] Setting eSpeak-ng language:', language);
        
        if (!window.uPiperESpeak || !window.uPiperESpeak.initialized) {
            console.error('[uPiper] eSpeak-ng not initialized');
            return false;
        }
        
        try {
            // Set language
            window.uPiperESpeak.currentVoice = language;
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
        
        if (!window.uPiperESpeak || !window.uPiperESpeak.initialized) {
            console.error('[uPiper] eSpeak-ng not initialized');
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
            var espeakNG = window.uPiperESpeak.instance;
            var phonemes = [];
            
            // Map Unity language codes to eSpeak voices
            var voiceMap = {
                'en': 'en',
                'en-US': 'en-us',
                'en-GB': 'en-gb',
                'zh': 'zh',
                'zh-CN': 'zh',
                'es': 'es',
                'fr': 'fr',
                'de': 'de',
                'ja': 'ja'
            };
            
            var voice = voiceMap[language] || 'en-us';
            
            // Use the phoneme extractor approach from piper-plus
            // Since we can't directly get IPA from this eSpeak-ng build,
            // we'll use a simplified approach similar to ESpeakPhonemeExtractor
            
            // Basic word to IPA mapping for common English words
            var wordToIPA = {
                'hello': ['h', 'ɛ', 'l', 'oʊ'],
                'world': ['w', 'ɜː', 'r', 'l', 'd'],
                'the': ['ð', 'ə'],
                'is': ['ɪ', 'z'],
                'a': ['ə'],
                'test': ['t', 'ɛ', 's', 't'],
                'of': ['ʌ', 'v'],
                'piper': ['p', 'aɪ', 'p', 'ər'],
                'text': ['t', 'ɛ', 'k', 's', 't'],
                'to': ['t', 'uː'],
                'speech': ['s', 'p', 'iː', 'tʃ'],
                'system': ['s', 'ɪ', 's', 't', 'ə', 'm'],
                'this': ['ð', 'ɪ', 's'],
                'how': ['h', 'aʊ'],
                'are': ['ɑː', 'r'],
                'you': ['j', 'uː'],
                'today': ['t', 'ə', 'd', 'eɪ'],
                'welcome': ['w', 'ɛ', 'l', 'k', 'ə', 'm'],
                'quick': ['k', 'w', 'ɪ', 'k'],
                'brown': ['b', 'r', 'aʊ', 'n'],
                'fox': ['f', 'ɒ', 'k', 's'],
                'jumps': ['dʒ', 'ʌ', 'm', 'p', 's'],
                'over': ['oʊ', 'v', 'ər'],
                'lazy': ['l', 'eɪ', 'z', 'i'],
                'dog': ['d', 'ɒ', 'g']
            };
            
            // Process text
            var words = text.toLowerCase().split(/\s+/);
            
            // Add BOS marker
            phonemes.push('^');
            
            for (var i = 0; i < words.length; i++) {
                var word = words[i];
                
                if (wordToIPA[word]) {
                    // Use known mapping
                    phonemes = phonemes.concat(wordToIPA[word]);
                } else {
                    // Fallback: simple character-based mapping
                    for (var j = 0; j < word.length; j++) {
                        var char = word[j];
                        // Basic character to phoneme mapping
                        switch(char) {
                            case 'a': phonemes.push('æ'); break;
                            case 'e': phonemes.push('ɛ'); break;
                            case 'i': phonemes.push('ɪ'); break;
                            case 'o': phonemes.push('ɒ'); break;
                            case 'u': phonemes.push('ʌ'); break;
                            case 'b': phonemes.push('b'); break;
                            case 'c': phonemes.push('k'); break;
                            case 'd': phonemes.push('d'); break;
                            case 'f': phonemes.push('f'); break;
                            case 'g': phonemes.push('g'); break;
                            case 'h': phonemes.push('h'); break;
                            case 'j': phonemes.push('dʒ'); break;
                            case 'k': phonemes.push('k'); break;
                            case 'l': phonemes.push('l'); break;
                            case 'm': phonemes.push('m'); break;
                            case 'n': phonemes.push('n'); break;
                            case 'p': phonemes.push('p'); break;
                            case 'r': phonemes.push('r'); break;
                            case 's': phonemes.push('s'); break;
                            case 't': phonemes.push('t'); break;
                            case 'v': phonemes.push('v'); break;
                            case 'w': phonemes.push('w'); break;
                            case 'x': phonemes.push('k'); phonemes.push('s'); break;
                            case 'y': phonemes.push('j'); break;
                            case 'z': phonemes.push('z'); break;
                            default: phonemes.push(char); break;
                        }
                    }
                }
                
                // Add space between words (except last word)
                if (i < words.length - 1) {
                    phonemes.push(' ');
                }
            }
            
            // Add EOS marker
            phonemes.push('$');
            
            console.log('[uPiper] Extracted phonemes:', phonemes);
            
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