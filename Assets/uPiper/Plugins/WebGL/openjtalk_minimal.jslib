mergeInto(LibraryManager.library, {
    // WebGL simplified OpenJTalk interface
    openjtalk_initialize__proxy: 'sync',
    openjtalk_initialize: function(mecab_dict_path_ptr) {
        console.log('[uPiper OpenJTalk] Initialize called');
        
        var mecab_dict_path = UTF8ToString(mecab_dict_path_ptr);
        console.log('[uPiper OpenJTalk] Dictionary path:', mecab_dict_path);
        
        // Initialize global object
        window.uPiperOpenJTalk = window.uPiperOpenJTalk || {};
        window.uPiperOpenJTalk.isInitialized = false;
        window.uPiperOpenJTalk.initError = null;
        
        // Check if OpenJTalkModule is already loaded
        if (typeof window.OpenJTalkModule === 'undefined') {
            // Load openjtalk.js dynamically
            var script = document.createElement('script');
            script.src = 'StreamingAssets/openjtalk.js';
            script.onload = function() {
                console.log('[uPiper OpenJTalk] Script loaded, initializing module...');
                initializeModule();
            };
            script.onerror = function(error) {
                console.error('[uPiper OpenJTalk] Failed to load script:', error);
                window.uPiperOpenJTalk.initError = error;
            };
            document.head.appendChild(script);
        } else {
            initializeModule();
        }
        
        function initializeModule() {
            // Initialize the module
            window.OpenJTalkModule({
                locateFile: function(filename) {
                    if (filename === 'openjtalk.wasm' || filename === 'openjtalk_wrapper.wasm') {
                        return 'StreamingAssets/openjtalk.wasm';
                    }
                    return filename;
                },
                print: function(text) {
                    console.log('[OpenJTalk]', text);
                },
                printErr: function(text) {
                    console.error('[OpenJTalk Error]', text);
                },
                onRuntimeInitialized: function() {
                    console.log('[uPiper OpenJTalk] Runtime initialized');
                }
            }).then(function(module) {
                console.log('[uPiper OpenJTalk] Module loaded successfully');
                
                // Store module and functions
                window.uPiperOpenJTalk.module = module;
                window.uPiperOpenJTalk.HEAP8 = module.HEAP8;
                window.uPiperOpenJTalk.HEAPU8 = module.HEAPU8;
                window.uPiperOpenJTalk._malloc = module._malloc;
                window.uPiperOpenJTalk._free = module._free;
                window.uPiperOpenJTalk.allocateUTF8 = module.allocateUTF8;
                window.uPiperOpenJTalk.UTF8ToString = module.UTF8ToString;
                
                // Test the module
                if (module._openjtalk_test) {
                    var testStr = module.allocateUTF8("test");
                    var result = module._openjtalk_test(testStr);
                    console.log('[uPiper OpenJTalk] Test result:', result); // Should be 4
                    module._free(testStr);
                }
                
                window.uPiperOpenJTalk.isInitialized = true;
                
                console.log('[uPiper OpenJTalk] Module initialized with HEAP arrays:', {
                    HEAP8: !!module.HEAP8,
                    HEAPU8: !!module.HEAPU8
                });
                
            }).catch(function(error) {
                console.error('[uPiper OpenJTalk] Failed to load module:', error);
                window.uPiperOpenJTalk.initError = error;
            });
        }
        
        // Return success (async initialization)
        return 0;
    },
    
    openjtalk_synthesis_labels__proxy: 'sync',
    openjtalk_synthesis_labels: function(text_ptr) {
        console.log('[uPiper OpenJTalk] Synthesis labels called');
        
        // Check if module is initialized
        if (!window.uPiperOpenJTalk || !window.uPiperOpenJTalk.isInitialized) {
            console.error('[uPiper OpenJTalk] Module not initialized');
            return 0;
        }
        
        var text = UTF8ToString(text_ptr);
        console.log('[uPiper OpenJTalk] Input text:', text);
        
        // For now, return a dummy label string
        // In the real implementation, this would call the actual OpenJTalk functions
        var dummyLabels = "0 50000 sil\n50000 100000 -k+o\n100000 150000 -o+n\n150000 200000 -n+n\n200000 250000 -n+i\n250000 300000 -i+ch\n300000 350000 -ch+i\n350000 400000 -i+w\n400000 450000 -w+a\n450000 500000 sil\n";
        
        var module = window.uPiperOpenJTalk.module;
        var labelPtr = module.allocateUTF8(dummyLabels);
        
        return labelPtr;
    },
    
    openjtalk_free_string__proxy: 'sync', 
    openjtalk_free_string: function(ptr) {
        if (ptr && window.uPiperOpenJTalk && window.uPiperOpenJTalk._free) {
            window.uPiperOpenJTalk._free(ptr);
        }
    },
    
    openjtalk_clear__proxy: 'sync',
    openjtalk_clear: function() {
        console.log('[uPiper OpenJTalk] Clear called');
        return 0;
    }
});