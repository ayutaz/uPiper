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
  const MULTI_CHAR_PHONEMES = {
    'br': '\ue000',
    'ch': '\ue001',
    'cl': '\ue002',
    'dy': '\ue003',
    'gy': '\ue004',
    'hy': '\ue005',
    'ky': '\ue006',
    'my': '\ue007',
    'ny': '\ue008',
    'py': '\ue009',
    'ry': '\ue00a',
    'sh': '\ue00b',
    'ts': '\ue00c',
    'ty': '\ue00d'
  };

  global.OpenJTalkUnityAPI = {
    /**
     * 初期化
     * @returns {Promise<boolean>} 初期化成功時true
     */
    async initialize() {
      if (initPromise) {
        return initPromise;
      }

      initPromise = (async () => {
        try {
          console.log('[OpenJTalkUnity] Initializing...');

          // Unity Moduleとは完全に別の名前空間で初期化
          const config = {
            locateFile: (path) => {
              if (path.endsWith('.wasm')) {
                return this.adjustPathForGitHubPages('StreamingAssets/openjtalk-unity.wasm');
              }
              return path;
            },
            print: (text) => console.log('[OpenJTalk]', text),
            printErr: (text) => console.error('[OpenJTalk]', text),
            onRuntimeInitialized: () => {
              console.log('[OpenJTalkUnity] Runtime initialized');
            }
          };

          // OpenJTalkModuleを使用（Unity Moduleとは別）
          if (typeof OpenJTalkModule === 'undefined') {
            throw new Error('OpenJTalkModule not found. Make sure openjtalk-unity.js is loaded.');
          }

          moduleInstance = await OpenJTalkModule(config);

          // OpenJTalk初期化
          const initResult = moduleInstance._Open_JTalk_initialize();
          if (initResult !== 0) {
            throw new Error(`OpenJTalk initialization failed with code: ${initResult}`);
          }

          // ファイルシステムの準備
          this.setupFileSystem();

          // 初期化完了
          isInitialized = true;
          console.log('[OpenJTalkUnity] Initialization complete');
          return true;

        } catch (error) {
          console.error('[OpenJTalkUnity] Initialization failed:', error);
          initPromise = null;
          throw error;
        }
      })();

      return initPromise;
    },

    /**
     * ファイルシステムのセットアップ
     */
    setupFileSystem() {
      if (!moduleInstance || !moduleInstance.FS) {
        throw new Error('Module not initialized');
      }

      console.log('[OpenJTalkUnity] Setting up file system...');

      // 必要なディレクトリを作成
      const dirs = ['/dict', '/voice', '/tmp'];
      dirs.forEach(dir => {
        try {
          const analysis = moduleInstance.FS.analyzePath(dir);
          if (!analysis.exists) {
            moduleInstance.FS.mkdir(dir);
            console.log(`[OpenJTalkUnity] Created directory: ${dir}`);
          }
        } catch (e) {
          console.warn(`[OpenJTalkUnity] Directory ${dir} might already exist:`, e.message);
        }
      });
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

      // テキストをメモリに書き込み
      const textBytes = moduleInstance.lengthBytesUTF8(text) + 1;
      const textPtr = moduleInstance._malloc(textBytes);
      moduleInstance.stringToUTF8(text, textPtr, textBytes);

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
        const phonemeString = moduleInstance.UTF8ToString(outputPtr);
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
      
      for (const phoneme of parts) {
        if (phoneme && phoneme !== 'sil' && phoneme !== 'pau') {
          // マルチ文字音素の変換
          if (MULTI_CHAR_PHONEMES[phoneme]) {
            phonemes.push(MULTI_CHAR_PHONEMES[phoneme]);
          } else {
            // 単一文字音素はそのまま追加
            for (const char of phoneme) {
              phonemes.push(char);
            }
          }
        }
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