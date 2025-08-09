mergeInto(LibraryManager.library, {
  /**
   * OpenJTalk Unity統合の初期化
   * Unity WebGLからOpenJTalkモジュールを初期化
   */
  InitializeOpenJTalkUnity: async function() {
    console.log('[Unity] Initializing OpenJTalk Unity integration...');
    
    try {
      // OpenJTalkモジュールスクリプトが既に読み込まれているか確認
      if (typeof OpenJTalkModule === 'undefined') {
        // OpenJTalkモジュールスクリプトを動的に読み込み
        const script = document.createElement('script');
        script.src = 'StreamingAssets/openjtalk-unity.js';
        document.head.appendChild(script);
        
        await new Promise((resolve, reject) => {
          script.onload = () => {
            console.log('[Unity] OpenJTalk module loaded');
            resolve();
          };
          script.onerror = (error) => {
            console.error('[Unity] Failed to load OpenJTalk module:', error);
            reject(error);
          };
        });
      }
      
      // ラッパースクリプトが既に読み込まれているか確認
      if (typeof OpenJTalkUnityAPI === 'undefined') {
        // ラッパースクリプトを動的に読み込み
        const wrapperScript = document.createElement('script');
        wrapperScript.src = 'StreamingAssets/openjtalk-unity-wrapper.js';
        document.head.appendChild(wrapperScript);
        
        await new Promise((resolve, reject) => {
          wrapperScript.onload = () => {
            console.log('[Unity] OpenJTalk wrapper loaded');
            resolve();
          };
          wrapperScript.onerror = (error) => {
            console.error('[Unity] Failed to load wrapper:', error);
            reject(error);
          };
        });
      }
      
      // API初期化
      if (window.OpenJTalkUnityAPI) {
        await window.OpenJTalkUnityAPI.initialize();
        console.log('[Unity] OpenJTalk Unity integration ready');
        return 0; // 成功
      } else {
        console.error('[Unity] OpenJTalkUnityAPI not found');
        return -1; // エラー
      }
      
    } catch (error) {
      console.error('[Unity] Initialization failed:', error);
      return -1; // エラー
    }
  },

  /**
   * 初期化状態の確認
   * @returns {number} 初期化済みなら1、未初期化なら0
   */
  IsOpenJTalkUnityInitialized: function() {
    return (window.OpenJTalkUnityAPI && 
            window.OpenJTalkUnityAPI.isReady && 
            window.OpenJTalkUnityAPI.isReady()) ? 1 : 0;
  },

  /**
   * 日本語テキストの音素化
   * @param {number} textPtr テキストへのポインタ
   * @returns {number} 結果JSONへのポインタ
   */
  PhonemizeWithOpenJTalk: function(textPtr) {
    const text = UTF8ToString(textPtr);
    console.log('[Unity] Phonemizing:', text);
    
    try {
      // API確認
      if (!window.OpenJTalkUnityAPI) {
        throw new Error('OpenJTalkUnityAPI not available');
      }
      
      if (!window.OpenJTalkUnityAPI.isReady()) {
        throw new Error('OpenJTalk not initialized');
      }
      
      // 音素化実行
      const phonemes = window.OpenJTalkUnityAPI.phonemize(text);
      console.log('[Unity] Phonemes:', phonemes);
      
      // 成功結果をJSON形式で返す
      const result = JSON.stringify({
        success: true,
        phonemes: phonemes,
        count: phonemes.length
      });
      
      // Unity側にメモリ確保して結果を書き込み
      const bufferSize = lengthBytesUTF8(result) + 1;
      const buffer = _malloc(bufferSize);
      stringToUTF8(result, buffer, bufferSize);
      
      return buffer;
      
    } catch (error) {
      console.error('[Unity] Phonemization failed:', error);
      
      // エラー結果をJSON形式で返す
      const errorResult = JSON.stringify({
        success: false,
        error: error.message || 'Unknown error',
        phonemes: []
      });
      
      const bufferSize = lengthBytesUTF8(errorResult) + 1;
      const buffer = _malloc(bufferSize);
      stringToUTF8(errorResult, buffer, bufferSize);
      
      return buffer;
    }
  },

  /**
   * メモリ解放
   * @param {number} ptr 解放するメモリのポインタ
   */
  FreeOpenJTalkMemory: function(ptr) {
    if (ptr && typeof _free !== 'undefined') {
      _free(ptr);
    }
  },

  /**
   * クリーンアップ
   */
  DisposeOpenJTalkUnity: function() {
    console.log('[Unity] Disposing OpenJTalk Unity integration');
    
    if (window.OpenJTalkUnityAPI && window.OpenJTalkUnityAPI.dispose) {
      window.OpenJTalkUnityAPI.dispose();
    }
  },

  /**
   * デバッグ情報の取得
   * @returns {number} デバッグ情報JSONへのポインタ
   */
  GetOpenJTalkDebugInfo: function() {
    const debugInfo = {
      moduleLoaded: typeof OpenJTalkModule !== 'undefined',
      apiLoaded: typeof OpenJTalkUnityAPI !== 'undefined',
      initialized: window.OpenJTalkUnityAPI && window.OpenJTalkUnityAPI.isReady(),
      version: window.OpenJTalkUnityAPI && window.OpenJTalkUnityAPI.version
    };
    
    const result = JSON.stringify(debugInfo);
    const bufferSize = lengthBytesUTF8(result) + 1;
    const buffer = _malloc(bufferSize);
    stringToUTF8(result, buffer, bufferSize);
    
    return buffer;
  }
});