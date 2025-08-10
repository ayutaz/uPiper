/**
 * GitHub Pages Adapter for Unity WebGL
 * 
 * GitHub Pages デプロイ時のパス解決とファイルロード対応
 */

(function(global) {
    'use strict';
    
    // GitHub Pages 環境検出
    const isGitHubPages = window.location.hostname.includes('github.io');
    
    // リポジトリ名を自動検出
    const getRepositoryName = () => {
        if (!isGitHubPages) return '';
        
        const pathParts = window.location.pathname.split('/').filter(p => p);
        // username.github.io/repository-name/... の形式
        return pathParts.length > 0 ? pathParts[0] : '';
    };
    
    // ベースURLを取得
    const getBaseURL = () => {
        if (!isGitHubPages) {
            // ローカルまたは通常のサーバー
            return window.location.origin + window.location.pathname.replace(/\/[^\/]*$/, '');
        }
        
        // GitHub Pages
        const repo = getRepositoryName();
        return `${window.location.origin}/${repo}`;
    };
    
    // StreamingAssets のパスを解決
    const resolveStreamingAssetsPath = (filename) => {
        const baseURL = getBaseURL();
        
        if (isGitHubPages) {
            // GitHub Pages では Build/StreamingAssets/ に配置される
            return `${baseURL}/Build/StreamingAssets/${filename}`;
        }
        
        // 通常のWebサーバー
        return `StreamingAssets/${filename}`;
    };
    
    // Build フォルダのパスを解決
    const resolveBuildPath = (filename) => {
        const baseURL = getBaseURL();
        
        if (isGitHubPages) {
            return `${baseURL}/Build/${filename}`;
        }
        
        return `Build/${filename}`;
    };
    
    // 大きなファイルの分割読み込み（100MB制限対策）
    const loadLargeFile = async (url, onProgress) => {
        try {
            // まず通常の読み込みを試みる
            const response = await fetch(url);
            
            if (!response.ok) {
                // ファイルが分割されている可能性をチェック
                const splitUrl = url + '.split';
                const manifestResponse = await fetch(splitUrl + '.manifest');
                
                if (manifestResponse.ok) {
                    // 分割ファイルを読み込み
                    return await loadSplitFile(splitUrl, manifestResponse, onProgress);
                }
                
                throw new Error(`Failed to load: ${url}`);
            }
            
            // 通常のファイル読み込み
            const contentLength = response.headers.get('content-length');
            const total = parseInt(contentLength, 10);
            const reader = response.body.getReader();
            
            let received = 0;
            const chunks = [];
            
            while (true) {
                const { done, value } = await reader.read();
                
                if (done) break;
                
                chunks.push(value);
                received += value.length;
                
                if (onProgress) {
                    onProgress(received, total);
                }
            }
            
            // 結合
            const result = new Uint8Array(received);
            let position = 0;
            for (const chunk of chunks) {
                result.set(chunk, position);
                position += chunk.length;
            }
            
            return result.buffer;
            
        } catch (error) {
            console.error('Failed to load large file:', error);
            throw error;
        }
    };
    
    // 分割ファイルの読み込み
    const loadSplitFile = async (baseUrl, manifestResponse, onProgress) => {
        const manifest = await manifestResponse.json();
        const { parts, totalSize } = manifest;
        
        const chunks = [];
        let received = 0;
        
        for (let i = 0; i < parts; i++) {
            const partUrl = `${baseUrl}.${i.toString().padStart(3, '0')}`;
            const response = await fetch(partUrl);
            const data = await response.arrayBuffer();
            
            chunks.push(new Uint8Array(data));
            received += data.byteLength;
            
            if (onProgress) {
                onProgress(received, totalSize);
            }
        }
        
        // 結合
        const result = new Uint8Array(totalSize);
        let position = 0;
        for (const chunk of chunks) {
            result.set(chunk, position);
            position += chunk.length;
        }
        
        return result.buffer;
    };
    
    // Unity WebGL ローダーのパッチ
    const patchUnityLoader = () => {
        if (!isGitHubPages) return;
        
        console.log('[GitHub Pages Adapter] Patching Unity loader for GitHub Pages');
        
        // createUnityInstance のオーバーライド
        const originalCreateUnityInstance = window.createUnityInstance;
        if (originalCreateUnityInstance) {
            window.createUnityInstance = function(canvas, config, onProgress) {
                // パスを GitHub Pages 用に調整
                if (config.dataUrl) {
                    config.dataUrl = resolveBuildPath(config.dataUrl.replace('Build/', ''));
                }
                if (config.frameworkUrl) {
                    config.frameworkUrl = resolveBuildPath(config.frameworkUrl.replace('Build/', ''));
                }
                if (config.codeUrl) {
                    config.codeUrl = resolveBuildPath(config.codeUrl.replace('Build/', ''));
                }
                if (config.streamingAssetsUrl) {
                    config.streamingAssetsUrl = getBaseURL() + '/Build/StreamingAssets';
                }
                
                return originalCreateUnityInstance.call(this, canvas, config, onProgress);
            };
        }
    };
    
    // OpenJTalk Unity API のパッチ
    const patchOpenJTalkAPI = () => {
        if (!isGitHubPages) return;
        
        // OpenJTalkUnityAPI の初期化を待つ
        const checkInterval = setInterval(() => {
            if (window.OpenJTalkUnityAPI) {
                clearInterval(checkInterval);
                
                const originalInitialize = window.OpenJTalkUnityAPI.initialize;
                window.OpenJTalkUnityAPI.initialize = async function() {
                    console.log('[GitHub Pages Adapter] Patching OpenJTalk paths');
                    
                    // WASMとデータファイルのパスを調整
                    if (window.OpenJTalkModule) {
                        const originalLocateFile = window.OpenJTalkModule.locateFile;
                        window.OpenJTalkModule.locateFile = function(path) {
                            if (path.endsWith('.wasm') || path.endsWith('.data')) {
                                return resolveStreamingAssetsPath(path);
                            }
                            return originalLocateFile ? originalLocateFile(path) : path;
                        };
                    }
                    
                    return originalInitialize.call(this);
                };
            }
        }, 100);
    };
    
    // ONNX Runtime のパッチ
    const patchONNXRuntime = () => {
        if (!isGitHubPages) return;
        
        // UnityONNX の初期化を待つ
        const checkInterval = setInterval(() => {
            if (window.UnityONNX) {
                clearInterval(checkInterval);
                
                const originalInitialize = window.UnityONNX.initialize;
                window.UnityONNX.initialize = async function(modelPath, configPath) {
                    console.log('[GitHub Pages Adapter] Patching ONNX model paths');
                    
                    // モデルパスを調整
                    if (!modelPath.startsWith('http')) {
                        modelPath = resolveStreamingAssetsPath(modelPath.replace('StreamingAssets/', ''));
                    }
                    if (configPath && !configPath.startsWith('http')) {
                        configPath = resolveStreamingAssetsPath(configPath.replace('StreamingAssets/', ''));
                    }
                    
                    return originalInitialize.call(this, modelPath, configPath);
                };
            }
        }, 100);
    };
    
    // エクスポート
    global.GitHubPagesAdapter = {
        isGitHubPages,
        getRepositoryName,
        getBaseURL,
        resolveStreamingAssetsPath,
        resolveBuildPath,
        loadLargeFile,
        
        // 自動初期化
        init: function() {
            console.log('[GitHub Pages Adapter] Initializing...');
            console.log('  Is GitHub Pages:', isGitHubPages);
            console.log('  Repository:', getRepositoryName());
            console.log('  Base URL:', getBaseURL());
            
            patchUnityLoader();
            patchOpenJTalkAPI();
            patchONNXRuntime();
            
            console.log('[GitHub Pages Adapter] Initialization complete');
        }
    };
    
    // ページロード時に自動実行
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', global.GitHubPagesAdapter.init);
    } else {
        global.GitHubPagesAdapter.init();
    }
    
})(window);