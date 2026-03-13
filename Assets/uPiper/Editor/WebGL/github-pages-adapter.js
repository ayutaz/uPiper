/**
 * GitHub Pages Adapter for uPiper WebGL.
 *
 * GitHub Pages デプロイ時のリポジトリ名自動検出とパス解決を行う。
 * username.github.io/repo-name/ 形式のURLを正しく処理する。
 */
(function(global) {
  'use strict';

  var LOG_PREFIX = '[GitHubPagesAdapter]';

  // GitHub Pages 環境検出
  var isGitHubPages = global.location.hostname.indexOf('github.io') !== -1;

  /**
   * リポジトリ名を URL パスから自動検出する。
   * username.github.io/repository-name/... の形式を想定。
   */
  function getRepositoryName() {
    if (!isGitHubPages) return '';

    var pathParts = global.location.pathname.split('/').filter(function(p) { return p; });
    // username.github.io/repository-name/index.html の場合、最初の要素がリポジトリ名
    return pathParts.length > 0 ? pathParts[0] : '';
  }

  /**
   * アプリケーションのベース URL を取得する。
   */
  function getBaseURL() {
    if (!isGitHubPages) {
      // ローカルまたは通常のサーバー: index.html のあるディレクトリ
      return global.location.origin + global.location.pathname.replace(/\/[^/]*$/, '');
    }

    var repo = getRepositoryName();
    return global.location.origin + '/' + repo;
  }

  /**
   * Build フォルダ内のファイルパスを解決する。
   */
  function resolveBuildPath(filename) {
    var base = getBaseURL();
    return base + '/Build/' + filename;
  }

  /**
   * StreamingAssets のパスを解決する。
   */
  function resolveStreamingAssetsPath(filename) {
    var base = getBaseURL();
    return base + '/StreamingAssets/' + filename;
  }

  /**
   * Unity WebGL ローダーの createUnityInstance をパッチし、
   * config 内のパスを GitHub Pages 用に調整する。
   */
  function patchUnityLoader() {
    if (!isGitHubPages) return;

    console.log(LOG_PREFIX, 'Patching Unity loader for GitHub Pages');

    var originalCreateUnityInstance = global.createUnityInstance;
    if (!originalCreateUnityInstance) {
      // createUnityInstance がまだロードされていない場合、遅延パッチ
      Object.defineProperty(global, 'createUnityInstance', {
        configurable: true,
        set: function(fn) {
          delete global.createUnityInstance;
          global.createUnityInstance = wrapCreateUnityInstance(fn);
        }
      });
      return;
    }

    global.createUnityInstance = wrapCreateUnityInstance(originalCreateUnityInstance);
  }

  function wrapCreateUnityInstance(originalFn) {
    return function(canvas, config, onProgress) {
      var base = getBaseURL();

      // パスを GitHub Pages 用に調整
      if (config.dataUrl && config.dataUrl.indexOf('://') === -1) {
        config.dataUrl = base + '/' + config.dataUrl;
      }
      if (config.frameworkUrl && config.frameworkUrl.indexOf('://') === -1) {
        config.frameworkUrl = base + '/' + config.frameworkUrl;
      }
      if (config.codeUrl && config.codeUrl.indexOf('://') === -1) {
        config.codeUrl = base + '/' + config.codeUrl;
      }
      if (config.streamingAssetsUrl) {
        config.streamingAssetsUrl = base + '/StreamingAssets';
      }

      console.log(LOG_PREFIX, 'Adjusted Unity config paths:', {
        dataUrl: config.dataUrl,
        frameworkUrl: config.frameworkUrl,
        codeUrl: config.codeUrl,
        streamingAssetsUrl: config.streamingAssetsUrl
      });

      return originalFn.call(this, canvas, config, onProgress);
    };
  }

  /**
   * 初期化処理
   */
  function init() {
    console.log(LOG_PREFIX, 'Initializing...');
    console.log(LOG_PREFIX, 'Is GitHub Pages:', isGitHubPages);
    console.log(LOG_PREFIX, 'Repository:', getRepositoryName());
    console.log(LOG_PREFIX, 'Base URL:', getBaseURL());

    patchUnityLoader();

    console.log(LOG_PREFIX, 'Initialization complete');
  }

  // Public API
  global.GitHubPagesAdapter = {
    isGitHubPages: isGitHubPages,
    getRepositoryName: getRepositoryName,
    getBaseURL: getBaseURL,
    resolveBuildPath: resolveBuildPath,
    resolveStreamingAssetsPath: resolveStreamingAssetsPath
  };

  // ページロード時に自動実行
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

})(window);