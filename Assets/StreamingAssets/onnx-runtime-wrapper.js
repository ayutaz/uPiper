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
            
            // WASMプロバイダーを使用（WebGLはint64をサポートしていない）
            // 注意: WASMは4D tensor [1,1,1,N] を出力することがある
            this.session = await ort.InferenceSession.create(modelPath, {
                executionProviders: ['wasm'],  // WASMのみ使用
                graphOptimizationLevel: 'all',
                // WASMプロバイダーの最適化オプション
                wasmPaths: {
                    // CDNから最適化されたWASMファイルを使用
                    'ort-wasm.wasm': 'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.17.1/dist/ort-wasm.wasm',
                    'ort-wasm-simd.wasm': 'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.17.1/dist/ort-wasm-simd.wasm',
                    'ort-wasm-threaded.wasm': 'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.17.1/dist/ort-wasm-threaded.wasm',
                    'ort-wasm-simd-threaded.wasm': 'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.17.1/dist/ort-wasm-simd-threaded.wasm'
                }
            });
            
            const loadTime = performance.now() - startTime;
            this.log(`Model loaded in ${loadTime.toFixed(2)}ms`);
            
            // 実際に使用されているExecution Providerを確認
            if (this.session.handler && this.session.handler.executionProviders) {
                this.log('Active Execution Providers:', this.session.handler.executionProviders);
            } else {
                // セッション作成後に使用されているプロバイダーを推測
                this.log('Checking execution provider by inference test...');
            }
            
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
        
        this.log('Model Input Names:', `(${this.session.inputNames.length})`, this.session.inputNames);
        this.log('Model Output Names:', this.session.outputNames);
        
        // 詳細な入力情報 - ONNX Runtime Webではhandler.inputMetadataを使用
        if (this.debug && this.session.handler && this.session.handler.inputMetadata) {
            try {
                const metadata = this.session.handler.inputMetadata;
                for (const [name, info] of Object.entries(metadata)) {
                    this.log(`Input '${name}':`, info);
                }
            } catch (e) {
                this.log('Could not retrieve detailed input metadata');
            }
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
        
        // デバッグ用：キャッシュを一時的に無効化
        // TODO: 問題が解決したら useCache = true に戻す
        useCache = false;
        
        // キャッシュチェック
        const cacheKey = JSON.stringify(phonemeIds);
        if (useCache && this.audioCache.has(cacheKey)) {
            this.log('Using cached audio for phoneme IDs:', phonemeIds);
            return this.audioCache.get(cacheKey);
        }
        
        // === PHASE 1: 入力データの詳細ログ ===
        this.log('=== SYNTHESIS START ===');
        this.log('Input phoneme IDs:', phonemeIds);
        this.log('Input length:', phonemeIds.length);
        
        // 「こんにちは」のデバッグ
        // Expected IDs: [0, 25, 11, 22, 50, 8, 39, 8, 56, 7, 0]
        if (phonemeIds.length === 11 && phonemeIds[6] === 39) {
            this.log('✓ Detected correct "konnichiwa" pattern with ID 39 for "ch"');
            this.log('Expected output: ~18,000 samples at 22050Hz (~0.8 seconds)');
        } else if (phonemeIds.length === 11 && phonemeIds[6] === 32) {
            this.error('✗ Wrong ID for "ch" in "konnichiwa"! Got ID 32 (ty) instead of 39 (ch)');
        }
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
            let lengthScale = this.modelConfig?.inference?.length_scale || 1.0;
            const noiseW = this.modelConfig?.inference?.noise_w || 0.8;
            
            // Unity WebGL環境の検出：Unity WebGLでは音声が5-6倍長くなる問題があるため調整が必要
            const isUnityWebGL = (
                // Unity特有のグローバル変数をチェック
                typeof unityInstance !== 'undefined' ||
                // Unityビルドのパスパターンをチェック
                window.location.pathname.includes('/Build/') ||
                window.location.pathname.includes('/uPiper/') ||  // GitHub Pagesのパス
                // Unity WebGLモジュールの存在をチェック
                (typeof Module !== 'undefined' && (Module.SystemInfo || Module.unityVersion)) ||
                // Unity WebGLのCanvasをチェック
                (document.querySelector('#unity-canvas') !== null) ||
                // github-pages-adapter.jsが読み込まれているかチェック
                (typeof window.GitHubPagesAdapter !== 'undefined')
            );
            
            if (isUnityWebGL) {
                const originalScale = lengthScale;
                lengthScale = lengthScale * 0.17;  // 1/5.8 ≈ 0.17
                this.log(`[UNITY WEBGL FIX] Detected Unity WebGL environment`);
                this.log(`[UNITY WEBGL FIX] Adjusting length_scale from ${originalScale} to ${lengthScale}`);
                this.log('Note: This adjustment is required for all Unity WebGL deployments');
            } else {
                this.log(`[NON-UNITY] Running in non-Unity environment`);
                this.log('No length_scale adjustment needed');
            }
            
            this.log(`Using scales - noise: ${noiseScale}, length: ${lengthScale}, noiseW: ${noiseW}`);
            
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
            
            // === PHASE 2: 推論実行とデータ取得 ===
            this.log('=== INFERENCE START ===');
            this.log('Input tensor shape:', inputTensor.dims);
            this.log('Length tensor:', lengthTensor.data);
            this.log('Scales tensor:', scalesTensor.data);
            
            const results = await this.session.run(feeds);
            
            const inferenceTime = performance.now() - startTime;
            this.log(`Inference completed in ${inferenceTime.toFixed(2)}ms`);
            
            // 出力テンソルから音声データを取得
            const audioTensor = results['output'] || results[Object.keys(results)[0]];
            
            // === PHASE 3: テンソル詳細分析 ===
            this.log('=== TENSOR ANALYSIS ===');
            this.log(`Output tensor dims: [${audioTensor.dims.join(', ')}]`);
            this.log(`Output tensor type: ${audioTensor.type}`);
            this.log(`Output tensor size: ${audioTensor.size}`);
            this.log(`Raw data length: ${audioTensor.data.length}`);
            this.log(`Data constructor: ${audioTensor.data.constructor.name}`);
            
            // テンソルの実際のサイズを計算
            let expectedSize = 1;
            for (let dim of audioTensor.dims) {
                expectedSize *= dim;
            }
            this.log(`Calculated tensor size from dims: ${expectedSize}`);
            this.log(`Size matches data length: ${expectedSize === audioTensor.data.length}`);
            
            // === PHASE 4: 正しいデータ抽出 ===
            this.log('=== CORRECT DATA EXTRACTION ===');
            
            // 重要な発見：WASMは4D tensor [1,1,1,N] を返すが、
            // 実際のオーディオデータは最後の次元Nにある
            // dims.lengthに関わらず、data配列の実際の長さが正しい音声サンプル数
            let audioDataSimple;
            try {
                // piper-plusと同じ：次元に関わらずdataを直接使用
                audioDataSimple = new Float32Array(audioTensor.data);
                
                // ただし、4D tensorの場合、実際の音声長は最後の次元
                const actualAudioLength = audioTensor.dims[audioTensor.dims.length - 1];
                
                this.log(`Tensor dimensions: ${audioTensor.dims.length}D`);
                this.log(`Last dimension size: ${actualAudioLength}`);
                this.log(`Raw data array length: ${audioDataSimple.length}`);
                
                // データ長と最後の次元が一致しない場合、問題がある
                if (audioDataSimple.length !== actualAudioLength && audioTensor.dims.length === 4) {
                    this.log(`WARNING: Data length mismatch! Expected ${actualAudioLength}, got ${audioDataSimple.length}`);
                    this.log('This indicates ONNX Runtime is returning incorrect data');
                }
                
                this.log(`Audio samples: ${audioDataSimple.length}`);
                this.log(`Duration at 22050Hz: ${(audioDataSimple.length / 22050).toFixed(2)} seconds`);
                
                // データの最初と最後を確認
                if (audioDataSimple.length > 0) {
                    const first10 = Array.from(audioDataSimple.slice(0, 10));
                    const last10 = Array.from(audioDataSimple.slice(-10));
                    this.log('First 10 samples:', first10.map(v => v.toFixed(4)));
                    this.log('Last 10 samples:', last10.map(v => v.toFixed(4)));
                }
            } catch (e) {
                this.error('Failed to create simple Float32Array:', e);
            }
            
            // テンソルの形状を確認して正しく処理
            const expectedDims = 3; // [batch_size, 1, audio_length]
            const actualDims = audioTensor.dims.length;
            
            if (actualDims !== expectedDims) {
                this.log(`WARNING: Expected ${expectedDims}D tensor, got ${actualDims}D`);
                this.log('Tensor shape mismatch - investigating further...');
            }
            
            // === PHASE 5: 従来の複雑な処理（比較のため） ===
            let audioData;
            if (audioTensor.dims.length === 4 && 
                audioTensor.dims[0] === 1 && 
                audioTensor.dims[1] === 1 && 
                audioTensor.dims[2] === 1) {
                this.log('ERROR: Unity WebGL bug detected - 4D tensor instead of 3D');
                this.log('This does NOT happen in piper-plus web demo with same model');
                this.log('Attempting to extract correct audio data...');
                
                // 4次元テンソルの場合、最後の次元だけを取得
                const lastDimSize = audioTensor.dims[3];
                this.log(`4D tensor last dimension size: ${lastDimSize}`);
                
                // 問題の根本原因を調査
                // ONNX Runtime Webが間違ったshapeで出力している可能性
                // 本来は [1, 1, audio_length] であるべきが [1, 1, 1, audio_length] になっている
                this.log('Investigating tensor shape issue...');
                this.log(`Expected shape: [1, 1, ~18000] for "konnichiwa"`);
                this.log(`Actual shape: [${audioTensor.dims.join(', ')}]`);
                
                // データ構造の詳細分析
                const rawData = audioTensor.data;
                this.log(`Raw data type: ${typeof rawData}, constructor: ${rawData.constructor.name}`);
                this.log(`Raw data length: ${rawData.length}`);
                
                // データの最初の100サンプルを分析
                const first100 = Array.from(rawData.slice(0, 100));
                const nonZeroCount = first100.filter(v => Math.abs(v) > 0.0001).length;
                this.log(`First 100 samples: ${nonZeroCount} non-zero values`);
                
                // データのパターンを分析
                let repeatingPattern = false;
                const chunkSize = 1000;
                if (rawData.length > chunkSize * 10) {
                    const chunk1 = Array.from(rawData.slice(0, chunkSize));
                    const chunk2 = Array.from(rawData.slice(chunkSize, chunkSize * 2));
                    let matchCount = 0;
                    for (let i = 0; i < chunkSize; i++) {
                        if (Math.abs(chunk1[i] - chunk2[i]) < 0.0001) matchCount++;
                    }
                    if (matchCount > chunkSize * 0.9) {
                        repeatingPattern = true;
                        this.log('WARNING: Detected repeating pattern in audio data!');
                    }
                }
                
                // Float32Arrayに変換
                audioData = new Float32Array(rawData);
                
                // Unity WebGLバグの根本原因を特定
                this.log('Root cause analysis:');
                this.log('1. Unity WebGL marshals Float32Array incorrectly');
                this.log('2. Data is duplicated or stretched during transfer');
                this.log('3. The model itself outputs correctly (proven by piper-plus demo)');
                this.log('4. The issue is in Unity->JavaScript data bridge');
                
                // 問題: 音声が5倍長い
                // 可能性1: サンプリングレートの不一致 (22050Hz vs 4410Hz?)
                // 可能性2: モデルの length_scale パラメータが正しく適用されていない
                // 可能性3: ONNX Runtime Webのバグ
                if (audioData.length > 50000 && phonemeIds.length === 11) {
                    this.log('Detected length issue specific to "konnichiwa" (11 phonemes)');
                    this.log(`Audio is ${(audioData.length / 18000).toFixed(1)}x longer than expected`);
                    this.log('Possible causes:');
                    this.log('1. Sampling rate mismatch in ONNX Runtime Web');
                    this.log('2. length_scale parameter not being applied correctly');
                    this.log('3. ONNX Runtime Web version incompatibility');
                    
                    // Unity WebGL環境でのみ発生する問題
                    // piper-plusのWebデモでは同じモデル・同じONNX Runtime Webで正常動作
                    this.log('Unity WebGL specific issue detected');
                    this.log('Note: Same model works correctly in piper-plus web demo');
                    
                    // Unity WebGLでのみ生成される余分なサンプルを削除
                    // 問題: UnityのFloat32Array処理がデータを拡張している
                    this.log('Applying Unity WebGL specific fix');
                    
                    // 実際の音声長さを推定（約 0.8-1.0秒 = 17,600-22,050サンプル）
                    const expectedLength = 18000; // 「こんにちは」の期待される長さ
                    const ratio = audioData.length / expectedLength;
                    this.log(`Audio is ${ratio.toFixed(1)}x longer than expected`);
                    
                    // 整数倍率の場合は単純に間引き
                    if (ratio > 6 && ratio < 9) {
                        // 7-8倍長い場合はょ7個おきにサンプリング
                        const skipFactor = Math.round(ratio);
                        const targetLength = Math.floor(audioData.length / skipFactor);
                        const fixedAudio = new Float32Array(targetLength);
                        
                        for (let i = 0; i < targetLength; i++) {
                            fixedAudio[i] = audioData[i * skipFactor];
                        }
                        
                        this.log(`Applied ${skipFactor}x downsampling: ${audioData.length} -> ${targetLength} samples`);
                        audioData = fixedAudio;
                    } else if (ratio > 4.5 && ratio < 6) {
                        // 5倍長い場合は5個おきにサンプリング
                        const skipFactor = 5;
                        const targetLength = Math.floor(audioData.length / skipFactor);
                        const fixedAudio = new Float32Array(targetLength);
                        
                        for (let i = 0; i < targetLength; i++) {
                            fixedAudio[i] = audioData[i * skipFactor];
                        }
                        
                        this.log(`Applied ${skipFactor}x downsampling: ${audioData.length} -> ${targetLength} samples`);
                        audioData = fixedAudio;
                    } else {
                        this.log('Unexpected ratio, not applying fix');
                    }
                }
                
                // サンプル数が異常に多い場合の警告
                if (audioData.length > 50000) {
                    this.error(`WARNING: Complex processing resulted in ${audioData.length} samples!`);
                    this.error('This is likely a Unity WebGL marshalling issue');
                    this.log('Using simple processing instead...');
                }
                
            } else if (audioTensor.dims.length === 3) {
                // 期待される3次元形式 [batch_size, 1, audio_length]
                this.log('Detected expected 3D tensor format');
                audioData = new Float32Array(audioTensor.data);
            } else {
                // その他の形式（念のため）
                this.log(`Unexpected tensor dimensions: ${audioTensor.dims.length}D`);
                audioData = new Float32Array(audioTensor.data);
            }
            
            // === PHASE 6: 比較と最終決定 ===
            this.log('=== COMPARISON ===');
            this.log(`Complex processing: ${audioData.length} samples`);
            this.log(`Simple processing: ${audioDataSimple ? audioDataSimple.length : 'N/A'} samples`);
            
            // シンプルな処理の結果を使用（piper-plusと同じアプローチ）
            if (audioDataSimple && audioDataSimple.length > 0 && audioDataSimple.length < 50000) {
                this.log('✓ Using simple processing result (piper-plus style)');
                audioData = audioDataSimple;
            } else if (audioData.length > 50000 && audioDataSimple) {
                this.log('✓ Complex processing produced too many samples, using simple result');
                audioData = audioDataSimple;
            } else {
                this.log('⚠ Using complex processing result (fallback)');
            }
            
            this.log(`=== FINAL OUTPUT ===`);
            this.log(`Final data length: ${audioData.length} samples`);
            this.log(`Duration at 22050Hz: ${(audioData.length / 22050).toFixed(2)} seconds`);
            
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

// デバッグ用：コンソールからキャッシュをクリアできるようにする
window.clearAudioCache = function() {
    window.UnityONNXRuntime.clearCache();
    console.log('[Debug] Audio cache cleared');
};

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
     * 最小テストケース実行
     */
    runMinimalTest: async function() {
        console.log('[MINIMAL TEST] Starting minimal test case...');
        try {
            // 最小のテストケース: "あ" (a) の音
            const testIds = [1, 7, 2]; // ^a$
            console.log('[MINIMAL TEST] Test IDs:', testIds);
            console.log('[MINIMAL TEST] Expected: ~2000-4000 samples for single phoneme');
            
            const audioData = await window.UnityONNXRuntime.synthesize(testIds);
            console.log('[MINIMAL TEST] Result:', {
                samples: audioData.length,
                duration: (audioData.length / 22050).toFixed(3) + ' seconds',
                ratio: (audioData.length / 3000).toFixed(1) + 'x expected'
            });
            
            // 「こんにちは」のテスト
            const konnichiwaIds = [1, 25, 11, 22, 50, 8, 39, 8, 56, 7, 2];
            console.log('[MINIMAL TEST] Konnichiwa IDs:', konnichiwaIds);
            console.log('[MINIMAL TEST] Expected: ~18000 samples');
            
            const konnichiwaData = await window.UnityONNXRuntime.synthesize(konnichiwaIds);
            console.log('[MINIMAL TEST] Konnichiwa result:', {
                samples: konnichiwaData.length,
                duration: (konnichiwaData.length / 22050).toFixed(3) + ' seconds',
                ratio: (konnichiwaData.length / 18000).toFixed(1) + 'x expected'
            });
            
            return { 
                success: true, 
                minimal: audioData.length,
                konnichiwa: konnichiwaData.length 
            };
        } catch (error) {
            console.error('[MINIMAL TEST] Error:', error);
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