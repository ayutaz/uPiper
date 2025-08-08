// OpenJTalk WASM Loader for Unity WebGL
// This wrapper handles ES6 module loading for Unity compatibility

(function() {
    console.log('[OpenJTalk Loader] Initializing...');
    
    // Create a wrapper that Unity can use
    window.OpenJTalkModule = function(config) {
        // Load the actual module dynamically
        return new Promise(async (resolve, reject) => {
            try {
                // Check if the module is already loaded
                if (window._OpenJTalkModuleInstance) {
                    console.log('[OpenJTalk Loader] Using cached module instance');
                    resolve(window._OpenJTalkModuleInstance);
                    return;
                }
                
                // Dynamically import the ES6 module
                const script = document.createElement('script');
                script.type = 'module';
                script.textContent = `
                    import('./StreamingAssets/openjtalk.js').then(async (module) => {
                        console.log('[OpenJTalk Loader] Module imported successfully');
                        
                        // The default export should be the OpenJTalkModule function
                        const OpenJTalkModule = module.default || module.OpenJTalkModule || module;
                        
                        // Initialize the module
                        const instance = await OpenJTalkModule({
                            locateFile: function(path) {
                                if (path.endsWith('.wasm')) {
                                    return 'StreamingAssets/openjtalk.wasm';
                                }
                                return path;
                            },
                            print: function(text) {
                                console.log('[OpenJTalk]', text);
                            },
                            printErr: function(text) {
                                console.error('[OpenJTalk]', text);
                            }
                        });
                        
                        // Store the instance globally
                        window._OpenJTalkModuleInstance = instance;
                        
                        // Notify Unity's wrapper
                        if (window.onOpenJTalkModuleReady) {
                            window.onOpenJTalkModuleReady(instance);
                        }
                    }).catch(error => {
                        console.error('[OpenJTalk Loader] Failed to import module:', error);
                        if (window.onOpenJTalkModuleError) {
                            window.onOpenJTalkModuleError(error);
                        }
                    });
                `;
                document.head.appendChild(script);
                
                // Set up callbacks
                window.onOpenJTalkModuleReady = function(instance) {
                    console.log('[OpenJTalk Loader] Module ready');
                    resolve(instance);
                };
                
                window.onOpenJTalkModuleError = function(error) {
                    console.error('[OpenJTalk Loader] Module error:', error);
                    reject(error);
                };
                
            } catch (error) {
                console.error('[OpenJTalk Loader] Error:', error);
                reject(error);
            }
        });
    };
    
    console.log('[OpenJTalk Loader] Wrapper installed');
})();