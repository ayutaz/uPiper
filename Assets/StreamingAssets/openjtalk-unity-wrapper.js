/**
 * OpenJTalk Unity Integration Wrapper
 * Unity WebGLビルドとの統合用ラッパー
 * M3 Task 3.2: ラッパー実装
 */
(function(global) {
  'use strict';

  let moduleInstance = null;
  let initPromise = null;
  let isInitialized = false;

  // マルチ文字音素のPUA（Private Use Area）マッピング
  // OpenJTalkToPiperMapping.csと同じマッピングを使用
  const MULTI_CHAR_PHONEMES = {
    'ky': '\ue006',  // きゃ、きゅ、きょ
    'gy': '\ue008',  // ぎゃ、ぎゅ、ぎょ
    'sy': '\ue010',  // しゃ、しゅ、しょ (= sh)
    'zy': '\ue011',  // じゃ、じゅ、じょ
    'ty': '\ue00a',  // ちゃ、ちゅ、ちょ
    'dy': '\ue00b',  // でゃ、でゅ、でょ
    'ny': '\ue013',  // にゃ、にゅ、にょ
    'hy': '\ue012',  // ひゃ、ひゅ、ひょ
    'by': '\ue00d',  // びゃ、びゅ、びょ
    'py': '\ue00c',  // ぴゃ、ぴゅ、ぴょ
    'my': '\ue014',  // みゃ、みゅ、みょ
    'ry': '\ue015',  // りゃ、りゅ、りょ
    'ch': '\ue00a',  // ち、ちゃ、ちゅ、ちょ (maps to same as ty)
    'ts': '\ue00f',  // つ
    'sh': '\ue010'   // し、しゃ、しゅ、しょ (same as sy)
  };

  global.OpenJTalkUnityAPI = {
    /**
     * 非同期初期化
     * @returns {Promise<boolean>} 初期化成功時true
     */
    async initializeAsync() {
      if (initPromise) {
        return initPromise;
      }

      initPromise = (async () => {
        try {
          console.log('[OpenJTalkUnity] Starting async initialization...');
          console.log('[OpenJTalkUnity] OpenJTalkModule type:', typeof OpenJTalkModule);

          // OpenJTalkModuleが使用可能か再チェック
          if (typeof OpenJTalkModule !== 'function') {
            throw new Error('OpenJTalkModule not available as function. Type: ' + typeof OpenJTalkModule);
          }

          // Unity Moduleとは完全に別の名前空間で初期化
          const baseUrl = window.location.href.substring(0, window.location.href.lastIndexOf('/') + 1);
          console.log('[OpenJTalkUnity] Base URL for WASM:', baseUrl);
          
          const config = {
            locateFile: (path) => {
              // Unity WebGLビルドのパスに合わせる
              if (path.endsWith('.wasm')) {
                return 'StreamingAssets/openjtalk-unity.wasm';
              }
              if (path.endsWith('.data')) {
                return 'StreamingAssets/openjtalk-unity.data';
              }
              return 'StreamingAssets/' + path;
            },
            print: (text) => console.log('[OpenJTalk]', text),
            printErr: (text) => console.error('[OpenJTalk]', text),
            onRuntimeInitialized: () => {
              console.log('[OpenJTalkUnity] WASM runtime initialized');
            },
            // WASMインポートエラーの修正
            instantiateWasm: function(imports, successCallback) {
              // 不足しているインポートを追加
              if (!imports.env.segfault) {
                imports.env.segfault = function() {
                  console.error('Segmentation fault!');
                  throw new Error('segfault');
                };
              }
              if (!imports.env.alignfault) {
                imports.env.alignfault = function() {
                  console.error('Alignment fault!');
                  throw new Error('alignfault');
                };
              }
              
              // WASMをロード
              WebAssembly.instantiateStreaming(
                fetch('StreamingAssets/openjtalk-unity.wasm'),
                imports
              ).then(function(result) {
                successCallback(result.instance);
              }).catch(function(error) {
                console.error('[OpenJTalkUnity] WASM streaming failed:', error);
                // フォールバック
                fetch('StreamingAssets/openjtalk-unity.wasm')
                  .then(response => response.arrayBuffer())
                  .then(bytes => WebAssembly.instantiate(bytes, imports))
                  .then(result => successCallback(result.instance))
                  .catch(err => console.error('[OpenJTalkUnity] Fatal error:', err));
              });
              
              return {};
            }
          };

          console.log('[OpenJTalkUnity] Creating module instance...');
          moduleInstance = await OpenJTalkModule(config);
          console.log('[OpenJTalkUnity] Module instance created:', !!moduleInstance);

          // WASMモジュールが完全に初期化されるまで待機
          if (!moduleInstance._Open_JTalk_initialize) {
            console.log('[OpenJTalkUnity] Waiting for WASM functions to be available...');
            await new Promise(resolve => {
              const checkFunctions = () => {
                if (moduleInstance._Open_JTalk_initialize) {
                  console.log('[OpenJTalkUnity] WASM functions now available');
                  resolve();
                } else {
                  setTimeout(checkFunctions, 50);
                }
              };
              checkFunctions();
            });
          }

          // OpenJTalk初期化
          console.log('[OpenJTalkUnity] Calling OpenJTalk initialize...');
          const initResult = moduleInstance._Open_JTalk_initialize();
          console.log('[OpenJTalkUnity] OpenJTalk initialize result:', initResult);
          
          if (initResult !== 0) {
            throw new Error(`OpenJTalk initialization failed with code: ${initResult}`);
          }

          // 辞書のロード（ファイルシステムは使用しない）
          console.log('[OpenJTalkUnity] Loading dictionary...');
          const loadResult = moduleInstance._Open_JTalk_load(0);
          console.log('[OpenJTalkUnity] Dictionary load result:', loadResult);
          
          if (loadResult !== 0) {
            throw new Error(`Dictionary load failed with code: ${loadResult}`);
          }

          // 初期化完了
          isInitialized = true;
          console.log('[OpenJTalkUnity] Initialization complete successfully');
          return true;

        } catch (error) {
          console.error('[OpenJTalkUnity] Async initialization failed:', error.message, error.stack);
          initPromise = null;
          throw error;
        }
      })();

      return initPromise;
    },

    /**
     * 同期的な初期化状態チェック（JSLibから呼び出し用）
     * @returns {boolean|number} 初期化済みならtrue、エラーなら-1、進行中なら0
     */
    initialize() {
      console.log('[OpenJTalkUnity] initialize() called, current state:', {
        isInitialized,
        hasModule: !!moduleInstance,
        hasInitPromise: !!initPromise,
        openJTalkModuleType: typeof OpenJTalkModule
      });
      
      // 既に初期化済み
      if (isInitialized && moduleInstance) {
        console.log('[OpenJTalkUnity] Already initialized (sync check)');
        return true;
      }

      // 初期化が進行中
      if (initPromise) {
        console.log('[OpenJTalkUnity] Initialization in progress (sync check)');
        
        // Promiseの状態を非同期でチェック
        initPromise.then(result => {
          console.log('[OpenJTalkUnity] Async initialization finished with result:', result);
          if (result && this.isReady()) {
            isInitialized = true;
            console.log('[OpenJTalkUnity] State updated to initialized');
          }
        }).catch(err => {
          console.error('[OpenJTalkUnity] Async initialization failed:', err);
          initPromise = null; // エラー時はPromiseをリセット
          isInitialized = false;
        });
        
        return 0; // 進行中
      }

      // OpenJTalkModuleが利用可能かチェック
      if (typeof OpenJTalkModule !== 'function') {
        console.log('[OpenJTalkUnity] OpenJTalkModule not available yet, type:', typeof OpenJTalkModule);
        return 0; // まだ準備中
      }

      // 初期化開始
      console.log('[OpenJTalkUnity] Starting initialization (sync)');
      this.initializeAsync().then(result => {
        console.log('[OpenJTalkUnity] Async initialization completed:', result);
        if (result) {
          isInitialized = true;
          console.log('[OpenJTalkUnity] Initialization successful, state updated');
        }
      }).catch(err => {
        console.error('[OpenJTalkUnity] Async initialization failed:', err);
        initPromise = null; // エラー時はPromiseをリセット
        isInitialized = false;
      });
      return 0; // 進行中
    },


    /**
     * テキストを音素に変換
     * @param {string} text 日本語テキスト
     * @returns {Array<string>} 音素配列
     */
    phonemize(text) {
      if (!isInitialized || !moduleInstance) {
        throw new Error('OpenJTalk Unity module not initialized. Call initialize() first.');
      }

      // 空テキストの処理
      if (!text || text.trim() === '') {
        return ['^', '$'];
      }

      console.log(`[OpenJTalkUnity] Phonemizing: "${text}"`);

      // テキストをUTF-8バイト配列に変換
      const encoder = new TextEncoder();
      const textArray = encoder.encode(text);
      const textBytes = textArray.length + 1; // null終端を含む
      
      // メモリを確保してテキストを書き込み
      const textPtr = moduleInstance._malloc(textBytes);
      for (let i = 0; i < textArray.length; i++) {
        moduleInstance.HEAPU8[textPtr + i] = textArray[i];
      }
      moduleInstance.HEAPU8[textPtr + textArray.length] = 0; // null終端

      // 出力バッファを確保
      const bufferSize = 1024;
      const outputPtr = moduleInstance._malloc(bufferSize);

      try {
        // OpenJTalkで音素化
        const resultLength = moduleInstance._Open_JTalk_synthesis(textPtr, outputPtr, bufferSize);
        
        if (resultLength <= 0) {
          throw new Error(`Phonemization failed with result: ${resultLength}`);
        }

        // 結果を文字列として取得
        let phonemeString = '';
        for (let i = 0; i < resultLength; i++) {
          phonemeString += String.fromCharCode(moduleInstance.HEAPU8[outputPtr + i]);
        }
        console.log(`[OpenJTalkUnity] Raw phonemes: "${phonemeString}"`);
        
        // 音素を解析して配列に変換
        return this.parsePhonemes(phonemeString);

      } finally {
        // メモリ解放
        moduleInstance._free(textPtr);
        moduleInstance._free(outputPtr);
      }
    },

    /**
     * 音素文字列を解析
     * @param {string} phonemeString スペース区切りの音素文字列
     * @returns {Array<string>} 音素配列
     */
    parsePhonemes(phonemeString) {
      const phonemes = ['^']; // BOS marker
      
      // スペースで分割
      const parts = phonemeString.trim().split(/\s+/);
      
      // 各音素を処理 - 特殊処理なしでそのまま送信
      // Windows/Android/piper-plusと同じ処理にする
      for (let i = 0; i < parts.length; i++) {
        const phoneme = parts[i];
        
        if (!phoneme || phoneme === 'sil' || phoneme === 'pau') {
          continue;
        }
        
        // 通常の音素として処理（特殊処理なし）
        phonemes.push(phoneme);
      }

      phonemes.push('$'); // EOS marker
      return phonemes;
    },

    /**
     * GitHub Pages環境かどうかを判定
     * @returns {boolean}
     */
    isGitHubPages() {
      return !!(global.window && 
                global.window.location && 
                global.window.location.hostname.includes('github.io'));
    },

    /**
     * GitHub Pages用のパス調整
     * @param {string} path 元のパス
     * @returns {string} 調整後のパス
     */
    adjustPathForGitHubPages(path) {
      if (!this.isGitHubPages()) {
        return path;
      }

      if (!global.window || !global.window.location) {
        return path;
      }

      const pathname = global.window.location.pathname;
      const pathParts = pathname.split('/').filter(p => p);
      
      if (pathParts.length > 0) {
        const repoName = pathParts[0];
        if (!path.startsWith('/')) {
          return `/${repoName}/${path}`;
        }
      }
      
      return path;
    },

    /**
     * リソースのクリーンアップ
     */
    dispose() {
      if (moduleInstance) {
        try {
          moduleInstance._Open_JTalk_clear();
          console.log('[OpenJTalkUnity] Resources disposed');
        } catch (error) {
          console.error('[OpenJTalkUnity] Error during disposal:', error);
        }
      }
      
      moduleInstance = null;
      initPromise = null;
      isInitialized = false;
    },

    /**
     * 初期化状態を取得
     * @returns {boolean}
     */
    isReady() {
      return isInitialized;
    },

    /**
     * バージョン情報
     */
    version: '1.0.0',

    /**
     * テスト用エクスポート（開発環境のみ）
     */
    _test: {
      MULTI_CHAR_PHONEMES: MULTI_CHAR_PHONEMES,
      getModule: () => moduleInstance,
      isInitialized: () => isInitialized
    }
  };

  // デバッグ用（開発環境のみ）
  if (typeof process !== 'undefined' && process.env && process.env.NODE_ENV === 'development') {
    global.OpenJTalkUnityAPI._debug = {
      getModule: () => moduleInstance,
      isInitialized: () => isInitialized,
      getMultiCharPhonemes: () => MULTI_CHAR_PHONEMES
    };
  }

})(typeof window !== 'undefined' ? window : global);