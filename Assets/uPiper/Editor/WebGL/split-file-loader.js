/**
 * Split File Loader for uPiper WebGL / GitHub Pages deployment.
 *
 * WebGLSplitDataProcessor が生成した分割チャンク (.partaa, .partab, ...)
 * を透過的に並列ダウンロードし、元のファイルとして結合して返す。
 *
 * 仕組み:
 *  1. fetch / XMLHttpRequest をインターセプト
 *  2. 対象ファイルの .split-meta を確認
 *  3. メタデータが存在すれば、チャンクを並列 DL → 結合 → Response 返却
 *  4. メタデータが無ければ、オリジナルの fetch にフォールバック
 */
(function() {
  'use strict';

  var LOG_PREFIX = '[SplitFileLoader]';

  // Store original fetch immediately
  var originalFetch = window.fetch;

  /**
   * Generate chunk suffixes: aa, ab, ..., az, ba, bb, ...
   */
  function chunkSuffix(index) {
    var first = String.fromCharCode(97 + Math.floor(index / 26));
    var second = String.fromCharCode(97 + (index % 26));
    return first + second;
  }

  /**
   * Load split metadata for a given URL.
   * Returns parsed JSON or null if not split.
   */
  async function loadSplitMeta(url) {
    try {
      var metaUrl = url + '.split-meta';
      var response = await originalFetch(metaUrl);
      if (!response.ok) return null;
      return await response.json();
    } catch (e) {
      return null;
    }
  }

  /**
   * Download all chunks in parallel and combine.
   */
  async function loadChunks(baseUrl, chunkCount) {
    var urls = [];
    for (var i = 0; i < chunkCount; i++) {
      urls.push(baseUrl + '.part' + chunkSuffix(i));
    }

    console.log(LOG_PREFIX, 'Loading', chunkCount, 'chunks for:', baseUrl);

    var responses = await Promise.all(
      urls.map(function(partUrl) {
        return originalFetch(partUrl).then(function(resp) {
          if (!resp.ok) {
            throw new Error('Failed to load chunk: ' + partUrl + ' (' + resp.status + ')');
          }
          return resp.arrayBuffer();
        });
      })
    );

    // Calculate total size and combine
    var totalLength = responses.reduce(function(sum, buf) { return sum + buf.byteLength; }, 0);
    var combined = new Uint8Array(totalLength);
    var offset = 0;

    for (var j = 0; j < responses.length; j++) {
      combined.set(new Uint8Array(responses[j]), offset);
      offset += responses[j].byteLength;
    }

    console.log(LOG_PREFIX, 'Combined', totalLength, 'bytes',
      '(' + (totalLength / 1024 / 1024).toFixed(2) + ' MB)');

    return combined;
  }

  /**
   * Determine the Content-Type based on file extension.
   */
  function guessContentType(url) {
    if (url.endsWith('.gz')) return 'application/gzip';
    if (url.endsWith('.br')) return 'application/x-br';
    if (url.endsWith('.wasm')) return 'application/wasm';
    if (url.endsWith('.data')) return 'application/octet-stream';
    return 'application/octet-stream';
  }

  // Override fetch
  window.fetch = async function(input, init) {
    var url = typeof input === 'string' ? input : (input && input.url ? input.url : '');

    // Skip if this is already a chunk or meta request
    if (url.indexOf('.part') !== -1 || url.indexOf('.split-meta') !== -1) {
      return originalFetch.apply(this, arguments);
    }

    // Check for split metadata
    var meta = await loadSplitMeta(url);
    if (!meta) {
      return originalFetch.apply(this, arguments);
    }

    console.log(LOG_PREFIX, 'Intercepted request for split file:', url);

    try {
      var combined = await loadChunks(url, meta.chunks);

      return new Response(combined, {
        status: 200,
        statusText: 'OK',
        headers: {
          'Content-Type': guessContentType(url),
          'Content-Length': combined.byteLength.toString()
        }
      });
    } catch (e) {
      console.error(LOG_PREFIX, 'Error loading chunks, falling back to original:', e);
      return originalFetch.apply(this, arguments);
    }
  };

  // Override XMLHttpRequest for legacy Unity loader compatibility
  var originalXHROpen = XMLHttpRequest.prototype.open;
  var originalXHRSend = XMLHttpRequest.prototype.send;

  XMLHttpRequest.prototype.open = function(method, url) {
    this._splitLoaderUrl = url;
    this._splitLoaderMethod = method;
    return originalXHROpen.apply(this, arguments);
  };

  XMLHttpRequest.prototype.send = function() {
    var url = this._splitLoaderUrl;
    var xhr = this;
    var args = arguments;

    // Skip non-split requests
    if (!url || url.indexOf('.part') !== -1 || url.indexOf('.split-meta') !== -1) {
      return originalXHRSend.apply(xhr, args);
    }

    // Check for split metadata asynchronously
    loadSplitMeta(url).then(function(meta) {
      if (!meta) {
        originalXHRSend.apply(xhr, args);
        return;
      }

      console.log(LOG_PREFIX, 'XHR intercepted for split file:', url);

      loadChunks(url, meta.chunks).then(function(combined) {
        // Simulate successful XHR response
        Object.defineProperty(xhr, 'status', { value: 200, writable: false });
        Object.defineProperty(xhr, 'statusText', { value: 'OK', writable: false });
        Object.defineProperty(xhr, 'readyState', { value: 4, writable: false });
        Object.defineProperty(xhr, 'response', { value: combined.buffer, writable: false });

        if (xhr.onreadystatechange) xhr.onreadystatechange();
        if (xhr.onload) xhr.onload({ target: xhr });
      }).catch(function(e) {
        console.error(LOG_PREFIX, 'XHR chunk load error, falling back:', e);
        originalXHRSend.apply(xhr, args);
      });
    }).catch(function() {
      originalXHRSend.apply(xhr, args);
    });
  };

  console.log(LOG_PREFIX, 'Fetch and XHR interceptors installed');
})();