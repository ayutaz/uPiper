// Pre-JS file for OpenJTalk WebAssembly module
// This ensures HEAP arrays and other runtime methods are properly exported

// Store the original Module configuration
var originalModuleConfig = Module || {};

// Ensure Module exists
var Module = Module || {};

// Merge with original configuration
for (var key in originalModuleConfig) {
    Module[key] = originalModuleConfig[key];
}

// Add onRuntimeInitialized callback to export HEAP arrays
var originalOnRuntimeInitialized = Module['onRuntimeInitialized'];
Module['onRuntimeInitialized'] = function() {
    // Call original callback if exists
    if (originalOnRuntimeInitialized) {
        originalOnRuntimeInitialized();
    }
    
    // Export HEAP arrays to Module object
    Module['HEAP8'] = HEAP8;
    Module['HEAPU8'] = HEAPU8;
    Module['HEAP16'] = HEAP16;
    Module['HEAPU16'] = HEAPU16;
    Module['HEAP32'] = HEAP32;
    Module['HEAPU32'] = HEAPU32;
    Module['HEAPF32'] = HEAPF32;
    Module['HEAPF64'] = HEAPF64;
    
    console.log('[OpenJTalk Pre-JS] Runtime initialized, HEAP arrays exported');
};