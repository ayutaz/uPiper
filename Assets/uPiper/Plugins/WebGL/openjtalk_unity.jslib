mergeInto(LibraryManager.library, {
  /**
   * OpenJTalk Unity統合の初期化
   * Unity WebGLからOpenJTalkモジュールを初期化
   */
  InitializeOpenJTalkUnity: function() {
    console.log('[Unity] InitializeOpenJTalkUnity called');
    
    try {
      // すでに初期化済みかチェック
      if (window.OpenJTalkUnityAPI && window.OpenJTalkUnityAPI.isReady && window.OpenJTalkUnityAPI.isReady()) {
        console.log('[Unity] Already initialized');
        return 0;
      }
      
      // ベースURLを取得（Unity WebGLビルドの場所）
      const baseUrl = window.location.href.substring(0, window.location.href.lastIndexOf('/') + 1);
      console.log('[Unity] Base URL:', baseUrl);
      
      // ロード状態を追跡
      window.OpenJTalkUnityState = window.OpenJTalkUnityState || {
        moduleLoaded: false,
        wrapperLoaded: false,
        initialized: false,
        initializing: false
      };
      
      console.log('[Unity] Current state:', window.OpenJTalkUnityState);
      console.log('[Unity] OpenJTalkModule type:', typeof window.OpenJTalkModule);
      console.log('[Unity] OpenJTalkUnityAPI type:', typeof window.OpenJTalkUnityAPI);
      
      // 両方のスクリプトが完全にロードされていることを確認
      const moduleReady = typeof window.OpenJTalkModule === 'function';
      const apiReady = typeof window.OpenJTalkUnityAPI === 'object' && window.OpenJTalkUnityAPI !== null;
      
      console.log('[Unity] Module ready:', moduleReady, 'API ready:', apiReady);
      
      // OpenJTalkモジュールスクリプトが未読み込みの場合
      if (!moduleReady && !window.OpenJTalkUnityState.moduleLoaded) {
        console.log('[Unity] Loading OpenJTalk module...');
        const script = document.createElement('script');
        script.src = baseUrl + 'StreamingAssets/openjtalk-unity.js';
        console.log('[Unity] Loading from:', script.src);
        
        script.onload = () => {
          console.log('[Unity] OpenJTalk module script loaded, type:', typeof window.OpenJTalkModule);
          window.OpenJTalkUnityState.moduleLoaded = true;
          
          // モジュールロード後、すぐにラッパーも読み込む
          if (!window.OpenJTalkUnityState.wrapperLoaded) {
            console.log('[Unity] Loading wrapper after module...');
            const wrapperScript = document.createElement('script');
            wrapperScript.src = baseUrl + 'StreamingAssets/openjtalk-unity-wrapper.js';
            console.log('[Unity] Loading wrapper from:', wrapperScript.src);
            
            wrapperScript.onload = () => {
              console.log('[Unity] OpenJTalk wrapper script loaded, type:', typeof window.OpenJTalkUnityAPI);
              window.OpenJTalkUnityState.wrapperLoaded = true;
            };
            wrapperScript.onerror = (error) => {
              console.error('[Unity] Failed to load wrapper:', error);
              window.OpenJTalkUnityState.wrapperLoaded = false;
            };
            
            document.head.appendChild(wrapperScript);
          }
        };
        script.onerror = (error) => {
          console.error('[Unity] Failed to load OpenJTalk module:', error);
          window.OpenJTalkUnityState.moduleLoaded = false;
        };
        
        document.head.appendChild(script);
        return -2; // 非同期読み込み中
      }
      
      // 両方のスクリプトがロード済みでも、まだラッパーが読み込まれていない場合（フォールバック）
      if (moduleReady && !apiReady && !window.OpenJTalkUnityState.wrapperLoaded) {
        console.log('[Unity] Loading wrapper...');
        const wrapperScript = document.createElement('script');
        wrapperScript.src = baseUrl + 'StreamingAssets/openjtalk-unity-wrapper.js';
        console.log('[Unity] Loading wrapper from:', wrapperScript.src);
        
        wrapperScript.onload = () => {
          console.log('[Unity] OpenJTalk wrapper script loaded, type:', typeof window.OpenJTalkUnityAPI);
          window.OpenJTalkUnityState.wrapperLoaded = true;
        };
        wrapperScript.onerror = (error) => {
          console.error('[Unity] Failed to load wrapper:', error);
          window.OpenJTalkUnityState.wrapperLoaded = false;
        };
        
        document.head.appendChild(wrapperScript);
        return -2; // 非同期読み込み中
      }
      
      // 両方のスクリプトが読み込まれている場合、API初期化を試行
      if (moduleReady && apiReady) {
        console.log('[Unity] Both scripts loaded, checking initialization...');
        
        // まず、すでに初期化済みかチェック
        if (window.OpenJTalkUnityAPI.isReady && window.OpenJTalkUnityAPI.isReady()) {
          console.log('[Unity] API already initialized and ready');
          window.OpenJTalkUnityState.initialized = true;
          return 0; // 成功
        }
        
        // 初期化が進行中でない場合のみ開始
        if (!window.OpenJTalkUnityState.initializing) {
          window.OpenJTalkUnityState.initializing = true;
          
          // 初期化を試行
          if (window.OpenJTalkUnityAPI.initialize) {
            console.log('[Unity] Calling API initialize...');
            const result = window.OpenJTalkUnityAPI.initialize();
            console.log('[Unity] API initialize result:', result);
            
            if (result === 0) {
              // 初期化開始は成功したが、非同期で完了するため再チェックが必要
              console.log('[Unity] API initialization started, will check again...');
              return -2; // 再試行が必要（isReady()のチェックのため）
            } else if (result === true) {
              // 同期的に初期化完了
              window.OpenJTalkUnityState.initialized = true;
              window.OpenJTalkUnityState.initializing = false;
              console.log('[Unity] OpenJTalk Unity integration ready');
              return 0; // 成功
            } else {
              console.warn('[Unity] API initialization returned unexpected result:', result);
              window.OpenJTalkUnityState.initializing = false;
              return -2; // 再試行が必要
            }
          } else {
            console.error('[Unity] API initialize method not found');
            window.OpenJTalkUnityState.initializing = false;
            return -1;
          }
        } else {
          console.log('[Unity] Initialization already in progress');
          return -2; // 再試行が必要
        }
      }
      
      // まだ準備ができていない
      const waitReason = !moduleReady ? 'module not ready' : !apiReady ? 'API not ready' : 'initialization not complete';
      console.log('[Unity] Still waiting:', waitReason);
      return -2; // 再試行が必要
      
    } catch (error) {
      console.error('[Unity] Initialization failed:', error.message, error.stack);
      return -1; // エラー
    }
  },

  /**
   * 初期化状態の確認
   * @returns {number} 初期化済みなら1、未初期化なら0
   */
  IsOpenJTalkUnityInitialized: function() {
    console.log('[Unity] IsOpenJTalkUnityInitialized called');
    const ready = (window.OpenJTalkUnityAPI && 
                   window.OpenJTalkUnityAPI.isReady && 
                   window.OpenJTalkUnityAPI.isReady());
    console.log('[Unity] IsReady result:', ready);
    
    // より詳細なデバッグ情報
    if (window.OpenJTalkUnityAPI) {
      console.log('[Unity] API exists, isReady function:', typeof window.OpenJTalkUnityAPI.isReady);
    } else {
      console.log('[Unity] API does not exist');
    }
    
    return ready ? 1 : 0;
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
      moduleLoaded: typeof window.OpenJTalkModule !== 'undefined',
      apiLoaded: typeof window.OpenJTalkUnityAPI !== 'undefined',
      initialized: window.OpenJTalkUnityAPI && window.OpenJTalkUnityAPI.isReady && window.OpenJTalkUnityAPI.isReady(),
      version: window.OpenJTalkUnityAPI && window.OpenJTalkUnityAPI.version
    };
    
    const result = JSON.stringify(debugInfo);
    const bufferSize = lengthBytesUTF8(result) + 1;
    const buffer = _malloc(bufferSize);
    stringToUTF8(result, buffer, bufferSize);
    
    return buffer;
  }
});