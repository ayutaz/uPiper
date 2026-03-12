var IndexedDBCacheLib = {
  $IndexedDBCacheState: {
    db: null,
    dbName: "uPiper-cache",
    storeName: "dictionaries",
    dbVersion: 1,
    callbackGameObject: "IndexedDBCallbackReceiver",

    openDB: function() {
      return new Promise(function(resolve, reject) {
        if (IndexedDBCacheState.db) {
          resolve(IndexedDBCacheState.db);
          return;
        }
        var request = indexedDB.open(IndexedDBCacheState.dbName, IndexedDBCacheState.dbVersion);
        request.onupgradeneeded = function(e) {
          var db = e.target.result;
          if (!db.objectStoreNames.contains(IndexedDBCacheState.storeName)) {
            db.createObjectStore(IndexedDBCacheState.storeName, { keyPath: "key" });
          }
        };
        request.onsuccess = function(e) {
          IndexedDBCacheState.db = e.target.result;
          resolve(IndexedDBCacheState.db);
        };
        request.onerror = function(e) {
          reject(e.target.error);
        };
      });
    }
  },

  IndexedDB_Store: function(keyPtr, dataPtr, dataLength, versionPtr, callbackId) {
    var key = UTF8ToString(keyPtr);
    var version = UTF8ToString(versionPtr);
    var data = new Uint8Array(HEAPU8.buffer, dataPtr, dataLength).slice();

    IndexedDBCacheState.openDB().then(function(db) {
      var tx = db.transaction(IndexedDBCacheState.storeName, "readwrite");
      var store = tx.objectStore(IndexedDBCacheState.storeName);
      var entry = { key: key, version: version, data: data };
      var request = store.put(entry);
      request.onsuccess = function() {
        SendMessage(IndexedDBCacheState.callbackGameObject, "OnStoreComplete", callbackId.toString());
      };
      request.onerror = function(e) {
        console.error("[IndexedDBCache] Store error for key '" + key + "':", e.target.error);
        SendMessage(IndexedDBCacheState.callbackGameObject, "OnStoreError", callbackId + "|" + e.target.error);
      };
    }).catch(function(err) {
      console.error("[IndexedDBCache] Failed to open DB for Store:", err);
      SendMessage(IndexedDBCacheState.callbackGameObject, "OnStoreError", callbackId + "|" + err);
    });
  },

  IndexedDB_Load: function(keyPtr, callbackId) {
    var key = UTF8ToString(keyPtr);

    IndexedDBCacheState.openDB().then(function(db) {
      var tx = db.transaction(IndexedDBCacheState.storeName, "readonly");
      var store = tx.objectStore(IndexedDBCacheState.storeName);
      var request = store.get(key);
      request.onsuccess = function(e) {
        var result = e.target.result;
        if (!result || !result.data) {
          SendMessage(IndexedDBCacheState.callbackGameObject, "OnLoadComplete", callbackId + "|0|0");
          return;
        }
        var data = result.data;
        var dataLength = data.length;
        var bufferPtr = _malloc(dataLength);
        HEAPU8.set(data, bufferPtr);
        SendMessage(IndexedDBCacheState.callbackGameObject, "OnLoadComplete", callbackId + "|" + bufferPtr + "|" + dataLength);
        _free(bufferPtr);
      };
      request.onerror = function(e) {
        console.error("[IndexedDBCache] Load error for key '" + key + "':", e.target.error);
        SendMessage(IndexedDBCacheState.callbackGameObject, "OnLoadError", callbackId + "|" + e.target.error);
      };
    }).catch(function(err) {
      console.error("[IndexedDBCache] Failed to open DB for Load:", err);
      SendMessage(IndexedDBCacheState.callbackGameObject, "OnLoadError", callbackId + "|" + err);
    });
  },

  IndexedDB_HasKey: function(keyPtr, versionPtr, callbackId) {
    var key = UTF8ToString(keyPtr);
    var version = UTF8ToString(versionPtr);

    IndexedDBCacheState.openDB().then(function(db) {
      var tx = db.transaction(IndexedDBCacheState.storeName, "readonly");
      var store = tx.objectStore(IndexedDBCacheState.storeName);
      var request = store.get(key);
      request.onsuccess = function(e) {
        var result = e.target.result;
        var hasKey = result && result.version === version ? 1 : 0;
        SendMessage(IndexedDBCacheState.callbackGameObject, "OnHasKeyComplete", callbackId + "|" + hasKey);
      };
      request.onerror = function(e) {
        console.error("[IndexedDBCache] HasKey error for key '" + key + "':", e.target.error);
        SendMessage(IndexedDBCacheState.callbackGameObject, "OnHasKeyComplete", callbackId + "|0");
      };
    }).catch(function(err) {
      console.error("[IndexedDBCache] Failed to open DB for HasKey:", err);
      SendMessage(IndexedDBCacheState.callbackGameObject, "OnHasKeyComplete", callbackId + "|0");
    });
  },

  IndexedDB_Delete: function(keyPtr) {
    var key = UTF8ToString(keyPtr);

    IndexedDBCacheState.openDB().then(function(db) {
      var tx = db.transaction(IndexedDBCacheState.storeName, "readwrite");
      var store = tx.objectStore(IndexedDBCacheState.storeName);
      store.delete(key);
    }).catch(function(err) {
      console.error("[IndexedDBCache] Failed to open DB for Delete:", err);
    });
  }
};

autoAddDeps(IndexedDBCacheLib, '$IndexedDBCacheState');
mergeInto(LibraryManager.library, IndexedDBCacheLib);