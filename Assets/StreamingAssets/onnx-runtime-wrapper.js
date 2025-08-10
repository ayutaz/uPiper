/**
 * Unity WebGL × ONNX Runtime Web Integration
 * 
 * Unity WebGL から ONNX Runtime Web を使用して音声合成を実行するラッパー
 */

class UnityONNXRuntime {
    constructor() {
        this.session = null;
        this.modelConfig = null;
        this.initialized = false;
        this.initPromise = null;
        
        // デバッグモード
        this.debug = true;
        
        // キャッシュ（同じ音素IDの再推論を避ける）
        this.audioCache = new Map();
        this.maxCacheSize = 50; // 最大50個の音声をキャッシュ
    }
    
    /**
     * デバッグログ出力
     */
    log(message, ...args) {
        if (this.debug) {
            console.log(`[UnityONNXRuntime] ${message}`, ...args);
        }
    }
    
    /**
     * エラーログ出力
     */
    error(message, ...args) {
        console.error(`[UnityONNXRuntime] ${message}`, ...args);
    }
    
    /**
     * ONNX Runtime Web をロード
     */
    async loadONNXRuntime() {
        if (window.ort) {
            this.log('ONNX Runtime already loaded');
            return;
        }
        
        this.log('Loading ONNX Runtime Web...');
        
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.17.1/dist/ort.min.js';
            script.onload = () => {
                this.log('ONNX Runtime Web loaded successfully');
                resolve();
            };
            script.onerror = (error) => {
                this.error('Failed to load ONNX Runtime Web:', error);
                reject(error);
            };
            document.head.appendChild(script);
        });
    }
    
    /**
     * モデルと設定を初期化
     */
    async initialize(modelPath, configPath) {
        // 既に初期化中または完了している場合
        if (this.initPromise) {
            return this.initPromise;
        }
        
        this.initPromise = this._initializeInternal(modelPath, configPath);
        return this.initPromise;
    }
    
    async _initializeInternal(modelPath, configPath) {
        try {
            this.log('Initializing ONNX Runtime with model:', modelPath);
            
            // ONNX Runtime Web をロード
            await this.loadONNXRuntime();
            
            // モデル設定をロード
            if (configPath) {
                this.log('Loading model config from:', configPath);
                const configResponse = await fetch(configPath);
                if (!configResponse.ok) {
                    throw new Error(`Failed to load config: ${configResponse.status}`);
                }
                this.modelConfig = await configResponse.json();
                this.log('Model config loaded:', this.modelConfig);
            }
            
            // ONNXモデルをロード
            this.log('Creating ONNX InferenceSession...');
            const startTime = performance.now();
            
            this.session = await ort.InferenceSession.create(modelPath, {
                executionProviders: ['wasm'],
                graphOptimizationLevel: 'all',
                enableCpuMemArena: true,
                enableMemPattern: true,
                // WebGL specific optimizations
                interOpNumThreads: 1,
                intraOpNumThreads: 1,
                executionMode: 'sequential'
            });
            
            const loadTime = performance.now() - startTime;
            this.log(`Model loaded in ${loadTime.toFixed(2)}ms`);
            
            // モデル情報をログ出力
            this.logModelInfo();
            
            // ウォームアップ（最初の推論は遅いため）
            await this.warmUp();
            
            this.initialized = true;
            this.log('ONNX Runtime initialization complete');
            
            return true;
        } catch (error) {
            this.error('Initialization failed:', error);
            this.initialized = false;
            throw error;
        }
    }
    
    /**
     * モデル情報をログ出力
     */
    logModelInfo() {
        if (!this.session) return;
        
        this.log('Model Input Names:', this.session.inputNames);
        this.log('Model Output Names:', this.session.outputNames);
        
        // 詳細な入力情報
        if (this.debug) {
            this.session.inputNames.forEach(name => {
                const info = this.session.inputs[name];
                if (info) {
                    this.log(`Input '${name}':`, info);
                }
            });
        }
    }
    
    /**
     * モデルのウォームアップ
     */
    async warmUp() {
        this.log('Warming up model...');
        
        try {
            // 最小限の入力でウォームアップ推論
            const dummyPhonemeIds = [1, 7, 2]; // "^a$"
            await this.synthesize(dummyPhonemeIds, false); // キャッシュしない
            
            this.log('Model warmed up');
        } catch (error) {
            this.error('Warm-up failed (non-critical):', error);
        }
    }
    
    /**
     * 音素IDから音声を合成
     */
    async synthesize(phonemeIds, useCache = true) {
        if (!this.initialized) {
            throw new Error('ONNX Runtime not initialized. Call initialize() first.');
        }
        
        // キャッシュチェック
        const cacheKey = JSON.stringify(phonemeIds);
        if (useCache && this.audioCache.has(cacheKey)) {
            this.log('Using cached audio for phoneme IDs:', phonemeIds);
            return this.audioCache.get(cacheKey);
        }
        
        this.log('Synthesizing audio for phoneme IDs:', phonemeIds);
        const startTime = performance.now();
        
        try {
            // 入力テンソルを作成
            const inputTensor = new ort.Tensor('int64', 
                new BigInt64Array(phonemeIds.map(id => BigInt(id))), 
                [1, phonemeIds.length]
            );
            
            const lengthTensor = new ort.Tensor('int64', 
                new BigInt64Array([BigInt(phonemeIds.length)]), 
                [1]
            );
            
            // スケールパラメータ（設定から取得、なければデフォルト値）
            const noiseScale = this.modelConfig?.inference?.noise_scale || 0.667;
            const lengthScale = this.modelConfig?.inference?.length_scale || 1.0;
            const noiseW = this.modelConfig?.inference?.noise_w || 0.8;
            
            const scalesTensor = new ort.Tensor('float32', 
                new Float32Array([noiseScale, lengthScale, noiseW]), 
                [3]
            );
            
            // 推論用の入力
            const feeds = {
                'input': inputTensor,
                'input_lengths': lengthTensor,
                'scales': scalesTensor
            };
            
            // スピーカーIDが必要な場合
            if (this.modelConfig?.num_speakers > 1) {
                feeds['sid'] = new ort.Tensor('int64', 
                    new BigInt64Array([BigInt(0)]), // デフォルトスピーカーID: 0
                    [1]
                );
            }
            
            // 推論実行
            const results = await this.session.run(feeds);
            
            const inferenceTime = performance.now() - startTime;
            this.log(`Inference completed in ${inferenceTime.toFixed(2)}ms`);
            
            // 出力テンソルから音声データを取得
            const audioTensor = results['output'] || results[Object.keys(results)[0]];
            let audioData = new Float32Array(audioTensor.data);
            
            // 多次元テンソルの場合、フラット化
            if (audioTensor.dims.length > 1) {
                const audioLength = audioTensor.dims[audioTensor.dims.length - 1];
                audioData = audioData.slice(0, audioLength);
            }
            
            // 音声統計情報
            this.logAudioStats(audioData);
            
            // キャッシュに保存
            if (useCache) {
                this.addToCache(cacheKey, audioData);
            }
            
            return audioData;
            
        } catch (error) {
            this.error('Synthesis failed:', error);
            throw error;
        }
    }
    
    /**
     * 音声データの統計情報をログ出力
     */
    logAudioStats(audioData) {
        if (!this.debug || !audioData || audioData.length === 0) return;
        
        const min = Math.min(...audioData);
        const max = Math.max(...audioData);
        const avg = audioData.reduce((a, b) => a + b, 0) / audioData.length;
        const absAvg = audioData.reduce((a, b) => a + Math.abs(b), 0) / audioData.length;
        
        this.log(`Audio stats - Samples: ${audioData.length}, Min: ${min.toFixed(4)}, Max: ${max.toFixed(4)}, Avg: ${avg.toFixed(4)}, AbsAvg: ${absAvg.toFixed(4)}`);
    }
    
    /**
     * キャッシュに追加（LRU）
     */
    addToCache(key, value) {
        // キャッシュサイズ制限
        if (this.audioCache.size >= this.maxCacheSize) {
            // 最も古いエントリを削除（MapはIteratorの順番を保持）
            const firstKey = this.audioCache.keys().next().value;
            this.audioCache.delete(firstKey);
        }
        
        this.audioCache.set(key, value);
    }
    
    /**
     * キャッシュをクリア
     */
    clearCache() {
        this.audioCache.clear();
        this.log('Audio cache cleared');
    }
    
    /**
     * リソースを解放
     */
    async dispose() {
        this.log('Disposing ONNX Runtime resources...');
        
        if (this.session) {
            await this.session.release();
            this.session = null;
        }
        
        this.clearCache();
        this.initialized = false;
        this.initPromise = null;
        this.modelConfig = null;
        
        this.log('Resources disposed');
    }
    
    /**
     * 初期化状態を取得
     */
    isInitialized() {
        return this.initialized;
    }
    
    /**
     * デバッグモードの設定
     */
    setDebugMode(enabled) {
        this.debug = enabled;
        this.log(`Debug mode ${enabled ? 'enabled' : 'disabled'}`);
    }
}

// グローバルインスタンスを作成
window.UnityONNXRuntime = new UnityONNXRuntime();

// Unity からアクセス可能な簡易API
window.UnityONNX = {
    /**
     * 初期化
     */
    initialize: async function(modelPath, configPath) {
        try {
            await window.UnityONNXRuntime.initialize(modelPath, configPath);
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    },
    
    /**
     * 音声合成
     */
    synthesize: async function(phonemeIds) {
        try {
            const audioData = await window.UnityONNXRuntime.synthesize(phonemeIds);
            return { success: true, data: audioData };
        } catch (error) {
            return { success: false, error: error.message };
        }
    },
    
    /**
     * リソース解放
     */
    dispose: async function() {
        await window.UnityONNXRuntime.dispose();
    },
    
    /**
     * デバッグモード設定
     */
    setDebugMode: function(enabled) {
        window.UnityONNXRuntime.setDebugMode(enabled);
    }
};

console.log('[UnityONNXRuntime] ONNX Runtime wrapper loaded and ready');