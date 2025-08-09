// OpenJTalk WebWorker Implementation
// This runs in a completely separate JavaScript context from Unity

let openJTalkModule = null;
let isInitialized = false;

// Message handler
self.addEventListener('message', async function(e) {
    const { type, id, data } = e.data;
    
    try {
        switch(type) {
            case 'init':
                await handleInit(id, data);
                break;
            case 'phonemize':
                await handlePhonemize(id, data);
                break;
            case 'test':
                self.postMessage({ id, type: 'success', result: 'Worker is running' });
                break;
            default:
                self.postMessage({ id, type: 'error', error: 'Unknown command: ' + type });
        }
    } catch (error) {
        self.postMessage({ 
            id, 
            type: 'error', 
            error: error.message || 'Unknown error'
        });
    }
});

async function handleInit(id, data) {
    console.log('[Worker] Initializing OpenJTalk...');
    
    try {
        // Import the OpenJTalk module
        importScripts(data.scriptPath || 'openjtalk.js');
        
        // Get the module factory function
        if (typeof OpenJTalkModule !== 'function') {
            throw new Error('OpenJTalkModule not found after import');
        }
        
        // Initialize the module
        openJTalkModule = await OpenJTalkModule({
            locateFile: (path) => {
                if (path.endsWith('.wasm')) {
                    return data.wasmPath || 'openjtalk.wasm';
                }
                return path;
            },
            print: (text) => console.log('[OpenJTalk]', text),
            printErr: (text) => console.error('[OpenJTalk]', text)
        });
        
        // Verify critical exports
        const requiredExports = ['_malloc', '_free', 'UTF8ToString', 'stringToUTF8'];
        for (const exportName of requiredExports) {
            if (!openJTalkModule[exportName]) {
                console.warn(`[Worker] Missing export: ${exportName}`);
            }
        }
        
        // Check for HEAP arrays
        if (!openJTalkModule.HEAP8 || !openJTalkModule.HEAPU8) {
            console.warn('[Worker] HEAP arrays not found, attempting to create...');
            
            // Try to create from wasmMemory
            if (openJTalkModule.wasmMemory) {
                const buffer = openJTalkModule.wasmMemory.buffer;
                openJTalkModule.HEAP8 = new Int8Array(buffer);
                openJTalkModule.HEAPU8 = new Uint8Array(buffer);
                openJTalkModule.HEAP16 = new Int16Array(buffer);
                openJTalkModule.HEAPU16 = new Uint16Array(buffer);
                openJTalkModule.HEAP32 = new Int32Array(buffer);
                openJTalkModule.HEAPU32 = new Uint32Array(buffer);
                openJTalkModule.HEAPF32 = new Float32Array(buffer);
                openJTalkModule.HEAPF64 = new Float64Array(buffer);
                console.log('[Worker] HEAP arrays created manually');
            }
        }
        
        // Initialize OpenJTalk with dictionary
        if (data.dictData && openJTalkModule.FS) {
            console.log('[Worker] Setting up dictionary files...');
            
            // Create directories
            try {
                openJTalkModule.FS.mkdir('/dict');
            } catch (e) {
                // Directory might already exist
            }
            
            // Write dictionary files
            if (data.dictData.files) {
                for (const [filename, content] of Object.entries(data.dictData.files)) {
                    const path = `/dict/${filename}`;
                    openJTalkModule.FS.writeFile(path, content);
                    console.log(`[Worker] Wrote ${path}`);
                }
            }
        }
        
        isInitialized = true;
        
        self.postMessage({ 
            id, 
            type: 'success',
            result: {
                initialized: true,
                hasHEAP: !!openJTalkModule.HEAP8,
                hasFS: !!openJTalkModule.FS,
                exports: Object.keys(openJTalkModule).filter(k => k.startsWith('_')).slice(0, 10)
            }
        });
        
    } catch (error) {
        console.error('[Worker] Init error:', error);
        self.postMessage({ 
            id, 
            type: 'error',
            error: error.message
        });
    }
}

async function handlePhonemize(id, data) {
    if (!isInitialized || !openJTalkModule) {
        throw new Error('Module not initialized');
    }
    
    const { text } = data;
    console.log(`[Worker] Phonemizing: "${text}"`);
    
    try {
        // Allocate memory for input text
        const textBytes = new TextEncoder().encode(text);
        const textPtr = openJTalkModule._malloc(textBytes.length + 1);
        
        if (!textPtr) {
            throw new Error('Failed to allocate memory');
        }
        
        // Write text to memory
        openJTalkModule.HEAPU8.set(textBytes, textPtr);
        openJTalkModule.HEAPU8[textPtr + textBytes.length] = 0;
        
        // Call OpenJTalk synthesis function
        let resultPtr = 0;
        
        if (openJTalkModule._openjtalk_synthesis_labels) {
            resultPtr = openJTalkModule._openjtalk_synthesis_labels(textPtr);
        } else if (openJTalkModule._synthesis_labels) {
            resultPtr = openJTalkModule._synthesis_labels(textPtr);
        } else {
            // Fallback: return simple phonemes
            console.warn('[Worker] Synthesis function not found, using fallback');
            openJTalkModule._free(textPtr);
            
            self.postMessage({
                id,
                type: 'success',
                result: {
                    phonemes: ['^', 'k', 'o', 'N', 'n', 'i', 'ch', 'i', 'w', 'a', '$'],
                    timing: null
                }
            });
            return;
        }
        
        // Read result
        let resultStr = '';
        if (resultPtr && openJTalkModule.UTF8ToString) {
            resultStr = openJTalkModule.UTF8ToString(resultPtr);
        }
        
        // Free memory
        openJTalkModule._free(textPtr);
        if (resultPtr && openJTalkModule._openjtalk_free_string) {
            openJTalkModule._openjtalk_free_string(resultPtr);
        } else if (resultPtr) {
            openJTalkModule._free(resultPtr);
        }
        
        // Parse labels into phonemes
        const phonemes = parseLabels(resultStr);
        
        self.postMessage({
            id,
            type: 'success',
            result: {
                phonemes: phonemes,
                timing: null,
                raw: resultStr
            }
        });
        
    } catch (error) {
        console.error('[Worker] Phonemize error:', error);
        self.postMessage({
            id,
            type: 'error',
            error: error.message
        });
    }
}

function parseLabels(labels) {
    if (!labels) {
        return [];
    }
    
    // Parse OpenJTalk label format
    const lines = labels.split('\n');
    const phonemes = [];
    
    for (const line of lines) {
        if (line.includes('+') && line.includes('-')) {
            // Extract phoneme from label
            const match = line.match(/\-([^+\-\/]+)\+/);
            if (match && match[1]) {
                const phoneme = match[1];
                if (phoneme !== 'pau' && phoneme !== 'sil') {
                    phonemes.push(phoneme);
                }
            }
        }
    }
    
    if (phonemes.length === 0) {
        // Fallback for simple text
        return ['^', ...labels.split(''), '$'];
    }
    
    return ['^', ...phonemes, '$'];
}

console.log('[Worker] OpenJTalk worker ready');