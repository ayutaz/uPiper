// Unity WebGL専用OpenJTalkラッパー
// Unity環境とpiper-plusモジュールを完全分離

(function() {
    'use strict';
    
    console.log('[OpenJTalk Unity] Initializing isolated wrapper...');
    
    // Unity環境のグローバルを保護
    const unityGlobals = {
        Module: window.Module,
        HEAP8: window.HEAP8,
        HEAPU8: window.HEAPU8,
        HEAP16: window.HEAP16,
        HEAPU16: window.HEAPU16,
        HEAP32: window.HEAP32,
        HEAPU32: window.HEAPU32,
        HEAPF32: window.HEAPF32,
        HEAPF64: window.HEAPF64,
        _malloc: window._malloc,
        _free: window._free
    };
    
    // OpenJTalkモジュール用の分離された名前空間
    window.OpenJTalkModule = async function(userConfig) {
        console.log('[OpenJTalk Unity] Creating isolated module instance...');
        
        // iframeを使用して完全に分離された環境を作成
        const iframe = document.createElement('iframe');
        iframe.style.display = 'none';
        iframe.sandbox = 'allow-scripts allow-same-origin';
        document.body.appendChild(iframe);
        
        const iframeWindow = iframe.contentWindow;
        const iframeDocument = iframe.contentDocument;
        
        try {
            // iframeの中でOpenJTalkモジュールをロード
            await new Promise((resolve, reject) => {
                // piper-plusのOpenJTalkモジュールをiframe内でロード
                const script = iframeDocument.createElement('script');
                script.type = 'module';
                
                // ES6モジュールをiframe内で実行
                script.textContent = `
                    import OpenJTalkModuleFactory from '${window.location.origin}/StreamingAssets/openjtalk.js';
                    
                    // iframeウィンドウに設定
                    window.OpenJTalkModuleFactory = OpenJTalkModuleFactory;
                    
                    // 初期化
                    (async () => {
                        try {
                            const config = {
                                locateFile: (path) => {
                                    if (path.endsWith('.wasm')) {
                                        return '${window.location.origin}/StreamingAssets/openjtalk.wasm';
                                    }
                                    return path;
                                },
                                print: (text) => console.log('[OpenJTalk iframe]', text),
                                printErr: (text) => console.error('[OpenJTalk iframe]', text)
                            };
                            
                            const module = await OpenJTalkModuleFactory(config);
                            
                            // HEAP配列が存在しない場合は作成
                            if (!module.HEAP8 && module.asm && module.asm.memory) {
                                const buffer = module.asm.memory.buffer;
                                module.HEAP8 = new Int8Array(buffer);
                                module.HEAPU8 = new Uint8Array(buffer);
                                module.HEAP16 = new Int16Array(buffer);
                                module.HEAPU16 = new Uint16Array(buffer);
                                module.HEAP32 = new Int32Array(buffer);
                                module.HEAPU32 = new Uint32Array(buffer);
                                module.HEAPF32 = new Float32Array(buffer);
                                module.HEAPF64 = new Float64Array(buffer);
                            }
                            
                            window.OpenJTalkModule = module;
                            window.parent.postMessage({ type: 'openjtalk-ready' }, '*');
                        } catch (error) {
                            window.parent.postMessage({ 
                                type: 'openjtalk-error', 
                                error: error.toString() 
                            }, '*');
                        }
                    })();
                `;
                
                // メッセージ受信を待つ
                const messageHandler = (event) => {
                    if (event.source === iframeWindow) {
                        if (event.data.type === 'openjtalk-ready') {
                            window.removeEventListener('message', messageHandler);
                            resolve();
                        } else if (event.data.type === 'openjtalk-error') {
                            window.removeEventListener('message', messageHandler);
                            reject(new Error(event.data.error));
                        }
                    }
                };
                
                window.addEventListener('message', messageHandler);
                iframeDocument.head.appendChild(script);
                
                // タイムアウト設定
                setTimeout(() => {
                    window.removeEventListener('message', messageHandler);
                    reject(new Error('OpenJTalk module load timeout'));
                }, 10000);
            });
            
            // iframe内のモジュールを取得
            const iframeModule = iframeWindow.OpenJTalkModule;
            
            if (!iframeModule) {
                throw new Error('OpenJTalk module not found in iframe');
            }
            
            // Unity側で使えるようにプロキシオブジェクトを作成
            const proxyModule = {
                // 基本的な関数をプロキシ
                _malloc: (size) => {
                    return iframeModule._malloc ? iframeModule._malloc(size) : 0;
                },
                _free: (ptr) => {
                    if (iframeModule._free) iframeModule._free(ptr);
                },
                
                // UTF8関連の関数
                UTF8ToString: (ptr) => {
                    if (!iframeModule.UTF8ToString) {
                        // フォールバック実装
                        const HEAPU8 = iframeModule.HEAPU8;
                        let str = '';
                        let i = ptr;
                        while (HEAPU8[i]) {
                            str += String.fromCharCode(HEAPU8[i++]);
                        }
                        return str;
                    }
                    return iframeModule.UTF8ToString(ptr);
                },
                
                stringToUTF8: (str, outPtr, maxBytesToWrite) => {
                    if (!iframeModule.stringToUTF8) {
                        // フォールバック実装
                        const HEAPU8 = iframeModule.HEAPU8;
                        for (let i = 0; i < str.length && i < maxBytesToWrite - 1; i++) {
                            HEAPU8[outPtr + i] = str.charCodeAt(i);
                        }
                        HEAPU8[outPtr + str.length] = 0;
                        return str.length;
                    }
                    return iframeModule.stringToUTF8(str, outPtr, maxBytesToWrite);
                },
                
                lengthBytesUTF8: (str) => {
                    return iframeModule.lengthBytesUTF8 ? 
                        iframeModule.lengthBytesUTF8(str) : 
                        str.length + 1;
                },
                
                // OpenJTalk特有の関数
                _openjtalk_initialize: iframeModule._openjtalk_initialize || (() => 0),
                _openjtalk_synthesis_labels: iframeModule._openjtalk_synthesis_labels,
                _openjtalk_free_string: iframeModule._openjtalk_free_string,
                _openjtalk_clear: iframeModule._openjtalk_clear,
                
                // HEAP配列へのアクセス
                HEAP8: iframeModule.HEAP8,
                HEAPU8: iframeModule.HEAPU8,
                HEAP16: iframeModule.HEAP16,
                HEAPU16: iframeModule.HEAPU16,
                HEAP32: iframeModule.HEAP32,
                HEAPU32: iframeModule.HEAPU32,
                HEAPF32: iframeModule.HEAPF32,
                HEAPF64: iframeModule.HEAPF64,
                
                // ファイルシステム
                FS: iframeModule.FS || {
                    writeFile: () => {},
                    readFile: () => new Uint8Array(0),
                    mkdir: () => {},
                    unlink: () => {}
                },
                
                // ccall/cwrap
                ccall: iframeModule.ccall,
                cwrap: iframeModule.cwrap,
                
                // 元のiframeモジュールへの参照（デバッグ用）
                _iframeModule: iframeModule,
                _iframe: iframe
            };
            
            console.log('[OpenJTalk Unity] Module successfully isolated and initialized');
            console.log('[OpenJTalk Unity] Available exports:', Object.keys(proxyModule));
            
            return proxyModule;
            
        } catch (error) {
            console.error('[OpenJTalk Unity] Failed to initialize:', error);
            // iframeをクリーンアップ
            if (iframe.parentNode) {
                iframe.parentNode.removeChild(iframe);
            }
            throw error;
        }
    };
    
    // Unityのグローバルを復元（念のため）
    Object.keys(unityGlobals).forEach(key => {
        if (unityGlobals[key] !== undefined) {
            window[key] = unityGlobals[key];
        }
    });
    
    console.log('[OpenJTalk Unity] Wrapper ready');
})();