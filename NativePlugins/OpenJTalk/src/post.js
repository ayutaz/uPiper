// Post-JS file for OpenJTalk WebAssembly module
// Additional exports and helper functions

// Ensure all required functions are accessible
if (typeof Module !== 'undefined') {
    // Re-export important functions at module level
    Module['malloc'] = Module['_malloc'] || _malloc;
    Module['free'] = Module['_free'] || _free;
    
    // Helper function to allocate and write string
    Module['allocateString'] = function(str) {
        var length = Module.lengthBytesUTF8(str) + 1;
        var ptr = Module._malloc(length);
        Module.stringToUTF8(str, ptr, length);
        return ptr;
    };
    
    // Helper function to read string
    Module['readString'] = function(ptr) {
        return Module.UTF8ToString(ptr);
    };
    
    console.log('[OpenJTalk Post-JS] Module exports configured');
}