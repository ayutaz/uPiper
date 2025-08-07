// OpenJTalk WebAssembly Loader for WebGL
// This file wraps the ES6 module to make it compatible with Unity WebGL

(function() {
    // Create a global OpenJTalkModule function
    window.OpenJTalkModule = async function(moduleArg) {
        // Default module configuration
        moduleArg = moduleArg || {};
        
        // Set locateFile to handle wasm file loading
        if (!moduleArg.locateFile) {
            moduleArg.locateFile = function(path, scriptDirectory) {
                // Unity StreamingAssets path
                if (path.endsWith('.wasm')) {
                    return 'StreamingAssets/' + path;
                }
                return scriptDirectory + path;
            };
        }
        
        // Load the actual module
        try {
            // Dynamic import to handle ES6 module
            const response = await fetch('StreamingAssets/openjtalk.js');
            const moduleText = await response.text();
            
            // Replace import.meta references
            const modifiedText = moduleText
                .replace(/import\.meta\.url/g, '"' + window.location.href + '"')
                .replace(/new URL\((.*?),\s*import\.meta\.url\)/g, function(match, p1) {
                    return 'new URL(' + p1 + ', window.location.href)';
                });
            
            // Create and execute the module
            const moduleFunc = new Function('moduleArg', modifiedText + '\nreturn OpenJTalkModule(moduleArg);');
            return await moduleFunc(moduleArg);
        } catch (error) {
            console.error('[OpenJTalk Loader] Failed to load module:', error);
            throw error;
        }
    };
    
    console.log('[OpenJTalk Loader] Loader initialized');
})();