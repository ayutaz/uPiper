mergeInto(LibraryManager.library, {
    // Initialize IndexedDB for caching
    InitializeIndexedDBCache: function() {
        console.log('[uPiper] Initializing IndexedDB cache...');
        
        if (!window.indexedDB) {
            console.error('[uPiper] IndexedDB not supported in this browser');
            return false;
        }
        
        // Create or open database
        window.uPiper = window.uPiper || {};
        window.uPiper.dbName = 'uPiperCache';
        window.uPiper.dbVersion = 1;
        
        var request = window.indexedDB.open(window.uPiper.dbName, window.uPiper.dbVersion);
        
        request.onerror = function(event) {
            console.error('[uPiper] IndexedDB error:', event);
            return false;
        };
        
        request.onsuccess = function(event) {
            window.uPiper.db = event.target.result;
            console.log('[uPiper] IndexedDB initialized successfully');
        };
        
        request.onupgradeneeded = function(event) {
            var db = event.target.result;
            
            // Create object stores
            if (!db.objectStoreNames.contains('phonemeCache')) {
                var phonemeStore = db.createObjectStore('phonemeCache', { keyPath: 'key' });
                phonemeStore.createIndex('timestamp', 'timestamp', { unique: false });
                console.log('[uPiper] Created phonemeCache store');
            }
            
            if (!db.objectStoreNames.contains('audioCache')) {
                var audioStore = db.createObjectStore('audioCache', { keyPath: 'key' });
                audioStore.createIndex('timestamp', 'timestamp', { unique: false });
                console.log('[uPiper] Created audioCache store');
            }
            
            if (!db.objectStoreNames.contains('modelCache')) {
                var modelStore = db.createObjectStore('modelCache', { keyPath: 'key' });
                modelStore.createIndex('size', 'size', { unique: false });
                console.log('[uPiper] Created modelCache store');
            }
        };
        
        return true;
    },
    
    // Store phonemes in cache
    CachePhonemes: function(keyPtr, phonemesPtr) {
        var key = UTF8ToString(keyPtr);
        var phonemesJson = UTF8ToString(phonemesPtr);
        
        if (!window.uPiper || !window.uPiper.db) {
            console.error('[uPiper] IndexedDB not initialized');
            return false;
        }
        
        try {
            var phonemes = JSON.parse(phonemesJson);
            var transaction = window.uPiper.db.transaction(['phonemeCache'], 'readwrite');
            var store = transaction.objectStore('phonemeCache');
            
            var data = {
                key: key,
                phonemes: phonemes,
                timestamp: Date.now()
            };
            
            var request = store.put(data);
            
            request.onsuccess = function() {
                console.log('[uPiper] Cached phonemes for key:', key);
            };
            
            request.onerror = function(event) {
                console.error('[uPiper] Failed to cache phonemes:', event);
            };
            
            return true;
        } catch (error) {
            console.error('[uPiper] Error caching phonemes:', error);
            return false;
        }
    },
    
    // Retrieve phonemes from cache
    GetCachedPhonemes: function(keyPtr) {
        var key = UTF8ToString(keyPtr);
        
        if (!window.uPiper || !window.uPiper.db) {
            console.error('[uPiper] IndexedDB not initialized');
            var result = JSON.stringify({ found: false });
            var bufferSize = lengthBytesUTF8(result) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(result, buffer, bufferSize);
            return buffer;
        }
        
        // Create promise for async operation
        window.uPiper.phonemePromises = window.uPiper.phonemePromises || {};
        var promiseId = Date.now() + '_' + Math.random();
        
        window.uPiper.phonemePromises[promiseId] = new Promise(function(resolve, reject) {
            var transaction = window.uPiper.db.transaction(['phonemeCache'], 'readonly');
            var store = transaction.objectStore('phonemeCache');
            var request = store.get(key);
            
            request.onsuccess = function(event) {
                var data = event.target.result;
                if (data) {
                    console.log('[uPiper] Found cached phonemes for key:', key);
                    resolve(JSON.stringify({
                        found: true,
                        phonemes: data.phonemes
                    }));
                } else {
                    resolve(JSON.stringify({ found: false }));
                }
            };
            
            request.onerror = function(event) {
                console.error('[uPiper] Error retrieving cached phonemes:', event);
                resolve(JSON.stringify({ found: false }));
            };
        });
        
        // Store promise ID for later retrieval
        var idResult = JSON.stringify({ promiseId: promiseId });
        var bufferSize = lengthBytesUTF8(idResult) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(idResult, buffer, bufferSize);
        return buffer;
    },
    
    // Clear old cache entries
    ClearOldCache: function(storeName, maxAgeMs) {
        if (!window.uPiper || !window.uPiper.db) {
            console.error('[uPiper] IndexedDB not initialized');
            return false;
        }
        
        var cutoffTime = Date.now() - maxAgeMs;
        
        try {
            var transaction = window.uPiper.db.transaction([storeName], 'readwrite');
            var store = transaction.objectStore(storeName);
            var index = store.index('timestamp');
            var range = IDBKeyRange.upperBound(cutoffTime);
            
            var request = index.openCursor(range);
            var deletedCount = 0;
            
            request.onsuccess = function(event) {
                var cursor = event.target.result;
                if (cursor) {
                    store.delete(cursor.primaryKey);
                    deletedCount++;
                    cursor.continue();
                } else {
                    console.log('[uPiper] Cleared', deletedCount, 'old entries from', storeName);
                }
            };
            
            return true;
        } catch (error) {
            console.error('[uPiper] Error clearing cache:', error);
            return false;
        }
    },
    
    // Get cache statistics
    GetCacheStats: function() {
        if (!window.uPiper || !window.uPiper.db) {
            var errorResult = JSON.stringify({
                error: 'IndexedDB not initialized',
                phonemeCount: 0,
                audioCount: 0,
                modelCount: 0,
                totalSize: 0
            });
            var bufferSize = lengthBytesUTF8(errorResult) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(errorResult, buffer, bufferSize);
            return buffer;
        }
        
        var stats = {
            phonemeCount: 0,
            audioCount: 0,
            modelCount: 0,
            totalSize: 0
        };
        
        // This would be async in real implementation
        var result = JSON.stringify(stats);
        var bufferSize = lengthBytesUTF8(result) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(result, buffer, bufferSize);
        return buffer;
    }
});