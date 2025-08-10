/**
 * OpenJTalk Unity WebGL Integration
 * 完全辞書版（107MB）対応
 * Unity WebGLビルドに統合するためのスクリプト
 */

(function() {
    'use strict';
    
    console.log('[OpenJTalk Integration] Starting initialization...');
    
    let moduleInstance = null;
    let initializePromise = null;
    
    // グローバルAPIを定義
    window.OpenJTalkWebGL = {
        isInitialized: false,
        
        /**
         * 初期化（非同期）
         * @returns {Promise<boolean>}
         */
        initialize: async function() {
            if (initializePromise) {
                return initializePromise;
            }
            
            if (this.isInitialized && moduleInstance) {
                console.log('[OpenJTalk] Already initialized');
                return Promise.resolve(true);
            }
            
            initializePromise = (async () => {
                try {
                    console.log('[OpenJTalk] Loading module...');
                    
                    if (typeof OpenJTalkModule !== 'function') {
                        throw new Error('OpenJTalkModule not found. Make sure openjtalk-unity.js is loaded.');
                    }
                    
                    // モジュールを初期化
                    moduleInstance = await OpenJTalkModule({
                        locateFile: (path) => {
                            // Unity WebGLビルドのパスに合わせる
                            return 'StreamingAssets/' + path;
                        },
                        print: (text) => console.log('[OpenJTalk]', text),
                        printErr: (text) => console.error('[OpenJTalk]', text),
                        setStatus: (text) => {
                            if (text && text.includes('Downloading')) {
                                const match = text.match(/\((\d+)\/(\d+)\)/);
                                if (match) {
                                    const percent = (parseInt(match[1]) / parseInt(match[2]) * 100).toFixed(0);
                                    console.log(`[OpenJTalk] Loading dictionary... ${percent}%`);
                                }
                            }
                        },
                        onRuntimeInitialized: () => {
                            console.log('[OpenJTalk] Runtime initialized');
                        }
                    });
                    
                    console.log('[OpenJTalk] Module created');
                    
                    // OpenJTalkを初期化
                    const initResult = moduleInstance._Open_JTalk_initialize();
                    if (initResult !== 0) {
                        throw new Error('OpenJTalk initialization failed');
                    }
                    console.log('[OpenJTalk] OpenJTalk initialized');
                    
                    // 辞書をロード
                    const loadResult = moduleInstance._Open_JTalk_load(0);
                    if (loadResult !== 0) {
                        throw new Error('Dictionary load failed');
                    }
                    console.log('[OpenJTalk] Dictionary loaded');
                    
                    // 辞書サイズを確認
                    if (moduleInstance.FS) {
                        try {
                            const stat = moduleInstance.FS.stat('/dict/sys.dic');
                            const sizeMB = (stat.size / 1024 / 1024).toFixed(2);
                            console.log(`[OpenJTalk] Dictionary size: ${sizeMB} MB`);
                            
                            if (stat.size < 100 * 1024 * 1024) {
                                console.warn('[OpenJTalk] Warning: Dictionary seems incomplete');
                            }
                        } catch (e) {
                            console.warn('[OpenJTalk] Could not verify dictionary size');
                        }
                    }
                    
                    this.isInitialized = true;
                    console.log('[OpenJTalk] ✅ Initialization complete!');
                    
                    // Unity側に通知（UnityのSendMessageを使用）
                    if (typeof unityInstance !== 'undefined' && unityInstance.SendMessage) {
                        try {
                            unityInstance.SendMessage('OpenJTalkManager', 'OnOpenJTalkReady', 'true');
                        } catch (e) {
                            console.log('[OpenJTalk] Unity notification skipped (no receiver)');
                        }
                    }
                    
                    return true;
                    
                } catch (error) {
                    console.error('[OpenJTalk] Initialization failed:', error);
                    this.isInitialized = false;
                    initializePromise = null;
                    throw error;
                }
            })();
            
            return initializePromise;
        },
        
        /**
         * テキストを音素に変換
         * @param {string} text - 日本語テキスト
         * @returns {string} - 音素文字列（スペース区切り）
         */
        phonemize: function(text) {
            if (!this.isInitialized || !moduleInstance) {
                console.error('[OpenJTalk] Not initialized. Call initialize() first.');
                return '';
            }
            
            if (!text || text.trim() === '') {
                return 'pau pau';
            }
            
            try {
                // テキストをエンコード
                const encoder = new TextEncoder();
                const textArray = encoder.encode(text);
                const textBytes = textArray.length + 1;
                
                // メモリ確保
                const textPtr = moduleInstance._malloc(textBytes);
                for (let i = 0; i < textArray.length; i++) {
                    moduleInstance.HEAPU8[textPtr + i] = textArray[i];
                }
                moduleInstance.HEAPU8[textPtr + textArray.length] = 0;
                
                // 出力バッファ
                const bufferSize = 1024;
                const outputPtr = moduleInstance._malloc(bufferSize);
                
                try {
                    // 音素変換実行
                    const resultLength = moduleInstance._Open_JTalk_synthesis(textPtr, outputPtr, bufferSize);
                    
                    if (resultLength <= 0) {
                        console.error('[OpenJTalk] Synthesis failed');
                        return '';
                    }
                    
                    // 結果を読み取り
                    let phonemeString = '';
                    for (let i = 0; i < resultLength; i++) {
                        phonemeString += String.fromCharCode(moduleInstance.HEAPU8[outputPtr + i]);
                    }
                    
                    // pauseマーカーを確認
                    if (!phonemeString.startsWith('pau ')) {
                        phonemeString = 'pau ' + phonemeString;
                    }
                    if (!phonemeString.endsWith(' pau')) {
                        phonemeString = phonemeString + ' pau';
                    }
                    
                    console.log(`[OpenJTalk] Phonemized: "${text}" -> "${phonemeString}"`);
                    return phonemeString;
                    
                } finally {
                    // メモリ解放
                    moduleInstance._free(textPtr);
                    moduleInstance._free(outputPtr);
                }
                
            } catch (error) {
                console.error('[OpenJTalk] Phonemization error:', error);
                return '';
            }
        },
        
        /**
         * Unity C#から呼び出し用のラッパー
         * @param {string} text
         * @returns {string}
         */
        phonemizeForUnity: function(text) {
            // Unity.jslib経由で呼ばれる
            return this.phonemize(text);
        },
        
        /**
         * ステータス取得
         * @returns {object}
         */
        getStatus: function() {
            return {
                initialized: this.isInitialized,
                hasModule: moduleInstance !== null,
                hasFS: moduleInstance && moduleInstance.FS !== undefined,
                hasFunctions: moduleInstance && moduleInstance._Open_JTalk_synthesis !== undefined
            };
        }
    };
    
    // 自動初期化（3秒後）
    setTimeout(() => {
        console.log('[OpenJTalk] Auto-initialization starting...');
        window.OpenJTalkWebGL.initialize().then(() => {
            console.log('[OpenJTalk] Auto-initialization succeeded');
        }).catch(error => {
            console.error('[OpenJTalk] Auto-initialization failed:', error);
        });
    }, 3000);
    
    // デバッグ用コンソール出力
    console.log('[OpenJTalk Integration] Script loaded. API available at window.OpenJTalkWebGL');
    console.log('[OpenJTalk Integration] Call window.OpenJTalkWebGL.initialize() to start manually');
    
})();