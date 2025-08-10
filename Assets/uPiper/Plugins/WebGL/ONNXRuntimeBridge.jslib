mergeInto(LibraryManager.library, {
    /**
     * ONNX Runtime Web を初期化
     * @param {number} modelPathPtr - モデルファイルパスのポインタ
     * @param {number} configPathPtr - 設定ファイルパスのポインタ
     * @param {number} callbackPtr - コールバック関数のポインタ
     */
    ONNXRuntime_Initialize: function(modelPathPtr, configPathPtr, callbackPtr) {
        const modelPath = UTF8ToString(modelPathPtr);
        const configPath = UTF8ToString(configPathPtr);
        
        console.log('[ONNXRuntimeBridge] Initializing with model:', modelPath, 'config:', configPath);
        
        // onnx-runtime-wrapper.js がロードされているか確認
        if (!window.UnityONNX) {
            console.error('[ONNXRuntimeBridge] onnx-runtime-wrapper.js not loaded!');
            // Unity へエラーコールバック
            Module.dynCall_vi(callbackPtr, 0);
            return;
        }
        
        // 非同期で初期化
        window.UnityONNX.initialize(modelPath, configPath).then(result => {
            if (result.success) {
                console.log('[ONNXRuntimeBridge] Initialization successful');
                Module.dynCall_vi(callbackPtr, 1);
            } else {
                console.error('[ONNXRuntimeBridge] Initialization failed:', result.error);
                Module.dynCall_vi(callbackPtr, 0);
            }
        }).catch(error => {
            console.error('[ONNXRuntimeBridge] Initialization error:', error);
            Module.dynCall_vi(callbackPtr, 0);
        });
    },
    
    /**
     * 音素IDから音声を合成
     * @param {number} phonemeIdsPtr - 音素ID配列のポインタ
     * @param {number} length - 配列の長さ
     * @param {number} callbackPtr - コールバック関数のポインタ
     */
    ONNXRuntime_Synthesize: function(phonemeIdsPtr, length, callbackPtr) {
        console.log('[ONNXRuntimeBridge] Synthesize called with length:', length);
        
        if (!window.UnityONNX) {
            console.error('[ONNXRuntimeBridge] ONNX Runtime not initialized');
            Module.dynCall_viii(callbackPtr, 0, 0, 0);
            return;
        }
        
        // Unity から音素ID配列を取得
        const phonemeIds = [];
        for (let i = 0; i < length; i++) {
            phonemeIds.push(HEAP32[(phonemeIdsPtr >> 2) + i]);
        }
        
        console.log('[ONNXRuntimeBridge] Phoneme IDs:', phonemeIds);
        
        // 非同期で音声合成
        window.UnityONNX.synthesize(phonemeIds).then(result => {
            if (result.success) {
                const audioData = result.data;
                console.log('[ONNXRuntimeBridge] Synthesis successful, audio length:', audioData.length);
                
                // Float32Array を Unity に返すためのメモリ確保
                const bufferSize = audioData.length * 4; // float は 4 bytes
                const bufferPtr = _malloc(bufferSize);
                
                // データをコピー
                HEAPF32.set(audioData, bufferPtr >> 2);
                
                // Unity へコールバック (成功フラグ、データポインタ、データ長)
                Module.dynCall_viii(callbackPtr, 1, bufferPtr, audioData.length);
                
                // 注意: Unity 側でデータコピー後に _free(bufferPtr) を呼ぶ必要がある
            } else {
                console.error('[ONNXRuntimeBridge] Synthesis failed:', result.error);
                Module.dynCall_viii(callbackPtr, 0, 0, 0);
            }
        }).catch(error => {
            console.error('[ONNXRuntimeBridge] Synthesis error:', error);
            Module.dynCall_viii(callbackPtr, 0, 0, 0);
        });
    },
    
    /**
     * メモリを解放
     * @param {number} ptr - 解放するメモリのポインタ
     */
    ONNXRuntime_FreeMemory: function(ptr) {
        if (ptr !== 0) {
            _free(ptr);
        }
    },
    
    /**
     * ONNX Runtime のリソースを解放
     */
    ONNXRuntime_Dispose: function() {
        console.log('[ONNXRuntimeBridge] Disposing ONNX Runtime');
        
        if (window.UnityONNX) {
            window.UnityONNX.dispose().then(() => {
                console.log('[ONNXRuntimeBridge] Disposed successfully');
            }).catch(error => {
                console.error('[ONNXRuntimeBridge] Dispose error:', error);
            });
        }
    },
    
    /**
     * デバッグモードを設定
     * @param {number} enabled - 0: 無効, 1: 有効
     */
    ONNXRuntime_SetDebugMode: function(enabled) {
        if (window.UnityONNX) {
            window.UnityONNX.setDebugMode(enabled !== 0);
            console.log('[ONNXRuntimeBridge] Debug mode:', enabled !== 0 ? 'enabled' : 'disabled');
        }
    },
    
    /**
     * onnx-runtime-wrapper.js スクリプトをロード
     * @param {number} callbackPtr - ロード完了コールバック
     */
    ONNXRuntime_LoadWrapper: function(callbackPtr) {
        console.log('[ONNXRuntimeBridge] Loading onnx-runtime-wrapper.js...');
        
        // 既にロード済みかチェック
        if (window.UnityONNX) {
            console.log('[ONNXRuntimeBridge] Wrapper already loaded');
            Module.dynCall_vi(callbackPtr, 1);
            return;
        }
        
        // スクリプトを動的にロード
        const script = document.createElement('script');
        script.src = 'StreamingAssets/onnx-runtime-wrapper.js';
        script.onload = () => {
            console.log('[ONNXRuntimeBridge] Wrapper loaded successfully');
            Module.dynCall_vi(callbackPtr, 1);
        };
        script.onerror = (error) => {
            console.error('[ONNXRuntimeBridge] Failed to load wrapper:', error);
            Module.dynCall_vi(callbackPtr, 0);
        };
        document.head.appendChild(script);
    },
    
    /**
     * 初期化状態を確認
     * @return {number} 0: 未初期化, 1: 初期化済み
     */
    ONNXRuntime_IsInitialized: function() {
        if (window.UnityONNXRuntime && window.UnityONNXRuntime.isInitialized()) {
            return 1;
        }
        return 0;
    }
});