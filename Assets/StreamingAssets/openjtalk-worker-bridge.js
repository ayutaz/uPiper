// OpenJTalk Worker Bridge
// This manages communication between Unity and the WebWorker

(function(global) {
    'use strict';
    
    console.log('[OpenJTalkBridge] Initializing...');
    
    class OpenJTalkBridge {
        constructor() {
            this.worker = null;
            this.pendingRequests = new Map();
            this.requestId = 0;
            this.initialized = false;
        }
        
        async initialize(config = {}) {
            console.log('[OpenJTalkBridge] Creating worker...');
            
            try {
                // Create worker
                this.worker = new Worker('StreamingAssets/openjtalk-worker.js');
                
                // Set up message handler
                this.worker.addEventListener('message', (e) => {
                    this.handleWorkerMessage(e.data);
                });
                
                // Set up error handler
                this.worker.addEventListener('error', (error) => {
                    console.error('[OpenJTalkBridge] Worker error:', error);
                    this.rejectAllPending('Worker error: ' + error.message);
                });
                
                // Send initialization message
                const result = await this.sendRequest('init', {
                    scriptPath: config.scriptPath || 'StreamingAssets/openjtalk.js',
                    wasmPath: config.wasmPath || 'StreamingAssets/openjtalk.wasm',
                    dictData: config.dictData
                });
                
                console.log('[OpenJTalkBridge] Worker initialized:', result);
                this.initialized = true;
                
                return result;
                
            } catch (error) {
                console.error('[OpenJTalkBridge] Initialization failed:', error);
                throw error;
            }
        }
        
        async phonemize(text) {
            if (!this.initialized) {
                throw new Error('Bridge not initialized');
            }
            
            console.log(`[OpenJTalkBridge] Phonemizing: "${text}"`);
            
            try {
                const result = await this.sendRequest('phonemize', { text });
                console.log('[OpenJTalkBridge] Phonemization result:', result);
                return result;
            } catch (error) {
                console.error('[OpenJTalkBridge] Phonemization failed:', error);
                throw error;
            }
        }
        
        sendRequest(type, data) {
            return new Promise((resolve, reject) => {
                const id = ++this.requestId;
                
                // Store callback
                this.pendingRequests.set(id, { resolve, reject });
                
                // Send message to worker
                this.worker.postMessage({ id, type, data });
                
                // Set timeout
                setTimeout(() => {
                    if (this.pendingRequests.has(id)) {
                        this.pendingRequests.delete(id);
                        reject(new Error('Request timeout'));
                    }
                }, 30000); // 30 second timeout
            });
        }
        
        handleWorkerMessage(message) {
            const { id, type, result, error } = message;
            
            const pending = this.pendingRequests.get(id);
            if (!pending) {
                console.warn('[OpenJTalkBridge] Received message for unknown request:', id);
                return;
            }
            
            this.pendingRequests.delete(id);
            
            if (type === 'success') {
                pending.resolve(result);
            } else {
                pending.reject(new Error(error || 'Unknown error'));
            }
        }
        
        rejectAllPending(error) {
            for (const [id, pending] of this.pendingRequests) {
                pending.reject(new Error(error));
            }
            this.pendingRequests.clear();
        }
        
        destroy() {
            if (this.worker) {
                this.worker.terminate();
                this.worker = null;
            }
            this.rejectAllPending('Bridge destroyed');
            this.initialized = false;
        }
    }
    
    // Global instance
    global.OpenJTalkBridge = OpenJTalkBridge;
    
    // Convenience function for Unity
    global.CreateOpenJTalkBridge = function() {
        return new OpenJTalkBridge();
    };
    
    console.log('[OpenJTalkBridge] Bridge class ready');
    
})(typeof window !== 'undefined' ? window : global);