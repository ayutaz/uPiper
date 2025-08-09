// OpenJTalk WebAssembly Wrapper for Unity WebGL
// This wrapper properly handles module loading and path resolution

(function() {
    // Store the original OpenJTalkModule function
    let originalOpenJTalkModule = null;
    
    // Create a wrapper that intercepts and fixes the module configuration
    window.OpenJTalkModuleWrapper = async function(config) {
        // Ensure we have the original module
        if (!originalOpenJTalkModule) {
            // Load the original module
            const response = await fetch('StreamingAssets/openjtalk.js');
            const jsText = await response.text();
            
            // Extract the OpenJTalkModule function
            // The module is defined as: async function OpenJTalkModule(moduleArg={})
            // and exported as: export default OpenJTalkModule
            
            // Remove the export statement
            const modifiedJs = jsText.replace('export default OpenJTalkModule;', '');
            
            // Create a function that returns the module
            const moduleFunction = new Function('return ' + modifiedJs + '; return OpenJTalkModule;');
            originalOpenJTalkModule = moduleFunction();
        }
        
        // Override the locateFile function to fix WASM path
        const fixedConfig = Object.assign({}, config, {
            locateFile: function(path) {
                console.log('[OpenJTalkWrapper] locateFile called with:', path);
                if (path.endsWith('.wasm')) {
                    // Always return the correct path relative to the root
                    return 'StreamingAssets/openjtalk.wasm';
                }
                return path;
            },
            // Override instantiateWasm to have full control over WASM loading
            instantiateWasm: function(imports, successCallback) {
                console.log('[OpenJTalkWrapper] Custom instantiateWasm called');
                
                // Manually load the WASM file
                fetch('StreamingAssets/openjtalk.wasm')
                    .then(response => response.arrayBuffer())
                    .then(bytes => WebAssembly.instantiate(bytes, imports))
                    .then(result => {
                        successCallback(result.instance, result.module);
                    })
                    .catch(error => {
                        console.error('[OpenJTalkWrapper] Failed to load WASM:', error);
                    });
                
                // Return empty object to indicate we're handling it
                return {};
            }
        });
        
        // Call the original module with fixed configuration
        return await originalOpenJTalkModule(fixedConfig);
    };
    
    // Make it available globally
    window.OpenJTalkModule = window.OpenJTalkModuleWrapper;
})();