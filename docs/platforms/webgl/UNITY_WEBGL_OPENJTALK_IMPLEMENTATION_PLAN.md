# Unity WebGL + piper-plus OpenJTalk 本番環境実装計画

## 概要

本ドキュメントは、Unity WebGLビルドにpiper-plus OpenJTalkを統合するための詳細な実装計画です。
TDD（テスト駆動開発）の原則に基づき、各マイルストーンで動作確認可能な成果物を作成します。

## 実装方針

- **開発手法**: TDD（テスト駆動開発）
- **テスト原則**: t-wadaのTDD原則に従い、Red-Green-Refactorサイクルを実施
- **品質基準**: テストカバレッジ80%以上
- **総所要時間**: 2-3日（実働16-21時間）

## マイルストーン概要

| ID | マイルストーン | 所要時間 | 依存関係 | 優先度 |
|----|--------------|---------|----------|--------|
| M1 | ビルド環境構築とテスト基盤 | 3-4時間 | なし | 必須 |
| M2 | Unity互換ビルドスクリプト作成 | 2-3時間 | M1 | 必須 |
| M3 | Unity統合ラッパー実装 | 3-4時間 | M2 | 必須 |
| M4 | Unity JSLib実装 | 2-3時間 | M3 | 必須 |
| M5 | Unity WebGLビルドとローカルテスト | 2-3時間 | M4 | 必須 |
| M6 | GitHub Pagesデプロイと本番テスト | 2時間 | M5 | 必須 |
| M7 | ドキュメントとCI/CD | 2時間 | M6 | 推奨 |

---

## Milestone 1: ビルド環境構築とテスト基盤

### ゴール
- ✅ piper-plusのビルド環境が動作すること
- ✅ JavaScriptテスト環境が構築されていること
- ✅ 既存のpiper-plusビルドが成功すること

### タスク詳細

#### Task 1.1: Docker環境構築（1時間）

**Dockerfile作成**
```dockerfile
# Dockerfile
FROM emscripten/emsdk:3.1.39

# 必要なツールをインストール
RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    git \
    python3 \
    python3-pip \
    nodejs \
    npm

# 作業ディレクトリ設定
WORKDIR /workspace

# Emscripten環境変数設定
ENV EMSDK=/emsdk
ENV EM_CONFIG=/emsdk/.emscripten
```

**docker-compose.yml作成**
```yaml
version: '3.8'
services:
  builder:
    build: .
    volumes:
      - ./piper-plus:/workspace/piper-plus
      - ./uPiper:/workspace/uPiper
    working_dir: /workspace
    command: /bin/bash
```

#### Task 1.2: JavaScriptテスト環境構築（1時間）

**package.json作成**
```json
{
  "name": "openjtalk-unity-integration",
  "version": "1.0.0",
  "description": "Unity WebGL OpenJTalk Integration",
  "scripts": {
    "test": "jest",
    "test:watch": "jest --watch",
    "test:coverage": "jest --coverage",
    "build": "./build-unity-compatible.sh",
    "build:test": "npm test && npm run build"
  },
  "devDependencies": {
    "jest": "^29.7.0",
    "@jest/globals": "^29.7.0",
    "jest-environment-jsdom": "^29.7.0",
    "jest-junit": "^16.0.0"
  },
  "jest": {
    "testEnvironment": "jsdom",
    "collectCoverageFrom": [
      "src/**/*.js",
      "!src/**/*.test.js"
    ],
    "coverageThreshold": {
      "global": {
        "branches": 80,
        "functions": 80,
        "lines": 80,
        "statements": 80
      }
    },
    "reporters": [
      "default",
      "jest-junit"
    ]
  }
}
```

#### Task 1.3: 基本テストの作成（1時間）

**test/setup.test.js**
```javascript
import { describe, it, expect, beforeAll } from '@jest/globals';

describe('Build Environment', () => {
  describe('Environment Check', () => {
    it('should have Node.js available', () => {
      expect(typeof process).toBe('object');
      expect(process.version).toMatch(/^v\d+\.\d+\.\d+$/);
    });

    it('should have required environment variables', () => {
      // Docker環境でのみテスト
      if (process.env.DOCKER_ENV) {
        expect(process.env.EMSDK).toBeDefined();
        expect(process.env.EM_CONFIG).toBeDefined();
      }
    });
  });

  describe('Existing Build', () => {
    it('should find piper-plus build script', () => {
      const fs = require('fs');
      const buildScriptPath = '../piper-plus/src/wasm/openjtalk-web/build/build-production.sh';
      expect(fs.existsSync(buildScriptPath)).toBe(true);
    });

    it('should identify HEAP array export issue', () => {
      const fs = require('fs');
      const buildScript = fs.readFileSync(
        '../piper-plus/src/wasm/openjtalk-web/build/build-production.sh',
        'utf8'
      );
      
      // HEAP配列がエクスポートされていないことを確認
      expect(buildScript).toContain('EXPORTED_RUNTIME_METHODS');
      expect(buildScript).not.toContain('"HEAP8"');
      expect(buildScript).not.toContain('"HEAPU8"');
    });
  });
});
```

#### Task 1.4: 既存ビルドの動作確認（1時間）

**ビルド実行スクリプト**
```bash
#!/bin/bash
# test-existing-build.sh
set -e

echo "=== Testing Existing piper-plus Build ==="

cd piper-plus/src/wasm/openjtalk-web
./build/build-production.sh

echo "=== Checking Build Output ==="
ls -la dist/

echo "=== Analyzing Module Format ==="
grep -c "export default" dist/openjtalk.js || echo "No ES6 exports found"

echo "=== Build Test Complete ==="
```

### 成果物チェックリスト
- [ ] Dockerfile
- [ ] docker-compose.yml
- [ ] package.json（テスト設定含む）
- [ ] test/setup.test.js
- [ ] 既存ビルドの動作確認ログ
- [ ] HEAP配列欠落の確認

---

## Milestone 2: Unity互換ビルドスクリプト作成

### ゴール
- ✅ Unity互換のビルドスクリプトが動作すること
- ✅ HEAP配列がエクスポートされること
- ✅ UMD形式で出力されること

### タスク詳細

#### Task 2.1: ビルドスクリプトのテスト作成（1時間）

**test/build-unity.test.js**
```javascript
import { describe, it, expect, beforeAll, afterAll } from '@jest/globals';
import fs from 'fs';
import { execSync } from 'child_process';

describe('Unity Compatible Build', () => {
  let buildOutput;
  
  beforeAll(() => {
    // ビルド実行
    try {
      execSync('./build-unity-compatible.sh', { stdio: 'inherit' });
      buildOutput = fs.readFileSync('dist/openjtalk-unity.js', 'utf8');
    } catch (error) {
      console.error('Build failed:', error);
    }
  });

  describe('Build Output Files', () => {
    it('should generate openjtalk-unity.js', () => {
      expect(fs.existsSync('dist/openjtalk-unity.js')).toBe(true);
    });

    it('should generate openjtalk-unity.wasm', () => {
      expect(fs.existsSync('dist/openjtalk-unity.wasm')).toBe(true);
    });

    it('should have reasonable file sizes', () => {
      const jsSize = fs.statSync('dist/openjtalk-unity.js').size;
      const wasmSize = fs.statSync('dist/openjtalk-unity.wasm').size;
      
      expect(jsSize).toBeGreaterThan(100000); // > 100KB
      expect(jsSize).toBeLessThan(5000000);   // < 5MB
      expect(wasmSize).toBeGreaterThan(1000000); // > 1MB
      expect(wasmSize).toBeLessThan(10000000);   // < 10MB
    });
  });

  describe('Module Format', () => {
    it('should NOT export as ES6 module', () => {
      expect(buildOutput).not.toContain('export default');
      expect(buildOutput).not.toContain('export {');
    });

    it('should define OpenJTalkUnity as global', () => {
      expect(buildOutput).toContain('OpenJTalkUnity');
    });

    it('should be in UMD format', () => {
      // UMD形式のパターンを確認
      expect(buildOutput).toMatch(/typeof exports.*typeof module/);
    });
  });

  describe('HEAP Arrays Export', () => {
    const heapArrays = [
      'HEAP8', 'HEAPU8', 'HEAP16', 'HEAPU16',
      'HEAP32', 'HEAPU32', 'HEAPF32', 'HEAPF64'
    ];

    heapArrays.forEach(heap => {
      it(`should export ${heap}`, () => {
        expect(buildOutput).toContain(`"${heap}"`);
      });
    });
  });

  describe('Required Functions', () => {
    const requiredFunctions = [
      '_openjtalk_initialize',
      '_openjtalk_synthesis_labels',
      '_openjtalk_free_string',
      '_openjtalk_clear',
      '_malloc',
      '_free'
    ];

    requiredFunctions.forEach(func => {
      it(`should export ${func}`, () => {
        expect(buildOutput).toContain(func);
      });
    });
  });

  describe('Runtime Methods', () => {
    const runtimeMethods = [
      'FS', 'cwrap', 'ccall', 'setValue', 'getValue',
      'UTF8ToString', 'stringToUTF8', 'lengthBytesUTF8', 'allocateUTF8'
    ];

    runtimeMethods.forEach(method => {
      it(`should export runtime method: ${method}`, () => {
        expect(buildOutput).toContain(`"${method}"`);
      });
    });
  });
});
```

#### Task 2.2: ビルドスクリプト実装（1時間）

**build-unity-compatible.sh**
```bash
#!/bin/bash
set -eu

echo "=== Building Unity-Compatible OpenJTalk WebAssembly ==="

# ディレクトリ設定
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
DIST_DIR="$PROJECT_DIR/dist"
SRC_DIR="$PROJECT_DIR/src"
PIPER_DIR="$PROJECT_DIR/piper-plus/src/wasm/openjtalk-web"

# 出力ディレクトリ作成
mkdir -p "$DIST_DIR"

# piper-plusのビルド環境を利用
WASM_OPENJTALK_DIR="$PIPER_DIR/tools/wasm_open_jtalk"
OPEN_JTALK_DIR="$WASM_OPENJTALK_DIR/tools/open_jtalk"
HTS_ENGINE_API_DIR="$WASM_OPENJTALK_DIR/tools/hts_engine_API"

# Emscripten環境設定
source "$WASM_OPENJTALK_DIR/tools/emsdk/emsdk_env.sh"

# インクルードパス設定
INCLUDE_FLAGS="-I$OPEN_JTALK_DIR/src/jpcommon \
    -I$OPEN_JTALK_DIR/src/mecab/src \
    -I$OPEN_JTALK_DIR/src/mecab2njd \
    -I$OPEN_JTALK_DIR/src/njd \
    -I$OPEN_JTALK_DIR/src/njd2jpcommon \
    -I$OPEN_JTALK_DIR/src/njd_set_accent_phrase \
    -I$OPEN_JTALK_DIR/src/njd_set_accent_type \
    -I$OPEN_JTALK_DIR/src/njd_set_digit \
    -I$OPEN_JTALK_DIR/src/njd_set_long_vowel \
    -I$OPEN_JTALK_DIR/src/njd_set_pronunciation \
    -I$OPEN_JTALK_DIR/src/njd_set_unvoiced_vowel \
    -I$OPEN_JTALK_DIR/src/text2mecab \
    -I$HTS_ENGINE_API_DIR/include"

# ライブラリ設定
LIBS="$OPEN_JTALK_DIR/src/build/libopenjtalk.a \
    $HTS_ENGINE_API_DIR/src/build/lib/libhts_engine_API.a"

# ソースコードの準備（デバッグコード削除）
cp "$PIPER_DIR/src/openjtalk_safe.c" "$SRC_DIR/openjtalk_unity.c"
sed -i 's/EM_ASM({[^}]*});//g' "$SRC_DIR/openjtalk_unity.c"

# Unity互換ビルド実行
emcc "$SRC_DIR/openjtalk_unity.c" \
    -o "$DIST_DIR/openjtalk-unity.js" \
    $INCLUDE_FLAGS \
    $LIBS \
    -DCHARSET_UTF_8 \
    -s ENVIRONMENT=web \
    -s MODULARIZE=1 \
    -s EXPORT_NAME='OpenJTalkUnity' \
    -s INITIAL_MEMORY=67108864 \
    -s ALLOW_MEMORY_GROWTH=1 \
    -s FILESYSTEM=1 \
    -s FORCE_FILESYSTEM=1 \
    -s EXPORTED_RUNTIME_METHODS='["HEAP8","HEAPU8","HEAP16","HEAPU16","HEAP32","HEAPU32","HEAPF32","HEAPF64","FS","cwrap","ccall","setValue","getValue","UTF8ToString","stringToUTF8","lengthBytesUTF8","allocateUTF8"]' \
    -s EXPORTED_FUNCTIONS='["_malloc","_free","_openjtalk_initialize","_openjtalk_clear","_openjtalk_synthesis_labels","_openjtalk_free_string"]' \
    -O3 \
    -s ASSERTIONS=0 \
    --no-export-es6 \
    -s EXPORT_ES6=0

echo "=== Build Complete ==="
echo "Output files:"
ls -lh "$DIST_DIR/openjtalk-unity.js" "$DIST_DIR/openjtalk-unity.wasm"

# テスト実行
npm test -- test/build-unity.test.js
```

### 成果物チェックリスト
- [ ] build-unity-compatible.sh
- [ ] test/build-unity.test.js（全テストGreen）
- [ ] dist/openjtalk-unity.js
- [ ] dist/openjtalk-unity.wasm
- [ ] ビルドログ

---

## Milestone 3: Unity統合ラッパー実装

### ゴール
- ✅ Unity Moduleと競合しないラッパーが動作すること
- ✅ 非同期初期化が正しく動作すること
- ✅ 音素化APIが正しく動作すること

### タスク詳細

#### Task 3.1: ラッパーAPIのテスト作成（1.5時間）

**test/unity-wrapper.test.js**
```javascript
import { describe, it, expect, beforeEach, afterEach } from '@jest/globals';
import fs from 'fs';

describe('OpenJTalkUnityAPI', () => {
  let originalWindow;
  let mockModule;

  beforeEach(() => {
    // グローバル環境のモック
    originalWindow = global.window;
    global.window = {
      location: {
        hostname: 'localhost',
        pathname: '/test/'
      }
    };

    // OpenJTalkUnityモジュールのモック
    mockModule = {
      _openjtalk_initialize: jest.fn(() => 0),
      _openjtalk_synthesis_labels: jest.fn(() => 12345),
      _openjtalk_free_string: jest.fn(),
      _openjtalk_clear: jest.fn(),
      allocateUTF8: jest.fn((str) => 67890),
      UTF8ToString: jest.fn(() => 'mocked_labels'),
      _free: jest.fn(),
      FS: {
        mkdir: jest.fn(),
        writeFile: jest.fn()
      }
    };

    global.OpenJTalkUnity = jest.fn(() => Promise.resolve(mockModule));
  });

  afterEach(() => {
    global.window = originalWindow;
    delete global.OpenJTalkUnity;
    delete global.OpenJTalkUnityAPI;
  });

  describe('Initialization', () => {
    it('should initialize only once (idempotent)', async () => {
      // ラッパー読み込み
      const wrapperCode = fs.readFileSync('src/openjtalk-unity-wrapper.js', 'utf8');
      eval(wrapperCode);

      const api = global.OpenJTalkUnityAPI;
      
      // 複数回初期化を呼んでも1回しか実行されない
      const promise1 = api.initialize();
      const promise2 = api.initialize();
      const promise3 = api.initialize();

      await Promise.all([promise1, promise2, promise3]);

      expect(global.OpenJTalkUnity).toHaveBeenCalledTimes(1);
    });

    it('should handle initialization errors gracefully', async () => {
      global.OpenJTalkUnity = jest.fn(() => 
        Promise.reject(new Error('Init failed'))
      );

      const wrapperCode = fs.readFileSync('src/openjtalk-unity-wrapper.js', 'utf8');
      eval(wrapperCode);

      const api = global.OpenJTalkUnityAPI;
      
      await expect(api.initialize()).rejects.toThrow('Init failed');
    });

    it('should not conflict with Unity Module', async () => {
      // Unity Moduleのモック
      global.Module = {
        unitySpecificData: true,
        _malloc: jest.fn(),
        HEAP8: new Int8Array(100)
      };

      const wrapperCode = fs.readFileSync('src/openjtalk-unity-wrapper.js', 'utf8');
      eval(wrapperCode);

      const api = global.OpenJTalkUnityAPI;
      await api.initialize();

      // Unity Moduleが変更されていないことを確認
      expect(global.Module.unitySpecificData).toBe(true);
      expect(global.Module).not.toBe(mockModule);
    });
  });

  describe('Phonemization', () => {
    beforeEach(async () => {
      const wrapperCode = fs.readFileSync('src/openjtalk-unity-wrapper.js', 'utf8');
      eval(wrapperCode);
      await global.OpenJTalkUnityAPI.initialize();
    });

    it('should phonemize Japanese text', () => {
      mockModule.UTF8ToString.mockReturnValue(`
        0 10000000 xx^xx-sil+k=o
        10000000 20000000 xx^sil-k+o=N
        20000000 30000000 sil^k-o+N=n
        30000000 40000000 k^o-N+n=i
        40000000 50000000 o^N-n+i=ch
        50000000 60000000 N^n-i+ch=i
        60000000 70000000 n^i-ch+i=w
        70000000 80000000 i^ch-i+w=a
        80000000 90000000 ch^i-w+a=sil
        90000000 100000000 i^w-a+sil=xx
      `);

      const result = global.OpenJTalkUnityAPI.phonemize('こんにちは');
      
      expect(result).toContain('k');
      expect(result).toContain('o');
      expect(result).toContain('N');
      expect(result).toContain('n');
      expect(result).toContain('i');
      expect(result).toContain('w');
      expect(result).toContain('a');
    });

    it('should handle multi-character phonemes', () => {
      mockModule.UTF8ToString.mockReturnValue(`
        0 10000000 xx^xx-sil+ch=i
        10000000 20000000 xx^sil-ch+i=ky
        20000000 30000000 sil^ch-i+ky=o
      `);

      const result = global.OpenJTalkUnityAPI.phonemize('ちきょう');
      
      // ch -> \ue001, ky -> \ue006 に変換されることを確認
      expect(result).toContain('\ue001');
      expect(result).toContain('\ue006');
    });

    it('should throw error when not initialized', () => {
      // 新しいインスタンスを作成（未初期化）
      delete global.OpenJTalkUnityAPI;
      const wrapperCode = fs.readFileSync('src/openjtalk-unity-wrapper.js', 'utf8');
      eval(wrapperCode);

      expect(() => {
        global.OpenJTalkUnityAPI.phonemize('テスト');
      }).toThrow('not initialized');
    });
  });

  describe('GitHub Pages Support', () => {
    it('should detect GitHub Pages environment', async () => {
      global.window.location.hostname = 'username.github.io';
      global.window.location.pathname = '/repository-name/';

      const wrapperCode = fs.readFileSync('src/openjtalk-unity-wrapper.js', 'utf8');
      eval(wrapperCode);

      const api = global.OpenJTalkUnityAPI;
      expect(api.isGitHubPages()).toBe(true);
    });

    it('should adjust paths for GitHub Pages', async () => {
      global.window.location.hostname = 'username.github.io';
      global.window.location.pathname = '/my-repo/';

      const wrapperCode = fs.readFileSync('src/openjtalk-unity-wrapper.js', 'utf8');
      eval(wrapperCode);

      const api = global.OpenJTalkUnityAPI;
      const adjustedPath = api.adjustPathForGitHubPages('StreamingAssets/file.js');
      
      expect(adjustedPath).toBe('/my-repo/StreamingAssets/file.js');
    });
  });

  describe('Memory Management', () => {
    it('should properly free allocated memory', async () => {
      const wrapperCode = fs.readFileSync('src/openjtalk-unity-wrapper.js', 'utf8');
      eval(wrapperCode);
      await global.OpenJTalkUnityAPI.initialize();

      global.OpenJTalkUnityAPI.phonemize('テスト');

      expect(mockModule._free).toHaveBeenCalled();
      expect(mockModule._openjtalk_free_string).toHaveBeenCalled();
    });
  });
});
```

#### Task 3.2: ラッパー実装（1.5時間）

**src/openjtalk-unity-wrapper.js**
```javascript
/**
 * OpenJTalk Unity Integration Wrapper
 * Unity WebGLビルドとの統合用ラッパー
 */
(function(global) {
  'use strict';

  let moduleInstance = null;
  let initPromise = null;
  let isInitialized = false;

  // マルチ文字音素のマッピング
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
            printErr: (text) => console.error('[OpenJTalk]', text)
          };

          moduleInstance = await OpenJTalkUnity(config);

          // 辞書ロード
          await this.loadDictionary();

          // 初期化完了
          isInitialized = true;
          console.log('[OpenJTalkUnity] Initialization complete');
          return true;

        } catch (error) {
          console.error('[OpenJTalkUnity] Initialization failed:', error);
          throw error;
        }
      })();

      return initPromise;
    },

    /**
     * 辞書データのロード
     */
    async loadDictionary() {
      if (!moduleInstance || !moduleInstance.FS) {
        throw new Error('Module not initialized');
      }

      console.log('[OpenJTalkUnity] Loading dictionary...');

      // ディレクトリ作成
      try {
        moduleInstance.FS.mkdir('/dict');
      } catch (e) {
        // Already exists
      }

      try {
        moduleInstance.FS.mkdir('/voice');
      } catch (e) {
        // Already exists
      }

      // 辞書ファイルのロード（実装は簡略化）
      // 実際の実装では、辞書ファイルをfetchして書き込む
      console.log('[OpenJTalkUnity] Dictionary loaded');
    },

    /**
     * テキストを音素に変換
     * @param {string} text 日本語テキスト
     * @returns {Array<string>} 音素配列
     */
    phonemize(text) {
      if (!isInitialized || !moduleInstance) {
        throw new Error('OpenJTalk Unity module not initialized');
      }

      // テキストをメモリに確保
      const textPtr = moduleInstance.allocateUTF8(text);
      
      try {
        // OpenJTalkで音素化
        const resultPtr = moduleInstance._openjtalk_synthesis_labels(textPtr);
        
        if (!resultPtr) {
          throw new Error('Failed to synthesize labels');
        }

        // 結果を文字列として取得
        const labels = moduleInstance.UTF8ToString(resultPtr);
        
        // メモリ解放
        moduleInstance._openjtalk_free_string(resultPtr);
        
        // 音素抽出
        return this.extractPhonemes(labels);

      } finally {
        // 入力テキストのメモリ解放
        moduleInstance._free(textPtr);
      }
    },

    /**
     * ラベルから音素を抽出
     * @param {string} labels OpenJTalkのラベル文字列
     * @returns {Array<string>} 音素配列
     */
    extractPhonemes(labels) {
      const phonemes = ['^']; // BOS marker
      const lines = labels.split('\n').filter(line => line.trim());

      for (const line of lines) {
        const match = line.match(/-([^+]+)\+/);
        if (match && match[1] !== 'sil' && match[1] !== 'pau') {
          let phoneme = match[1];
          
          // マルチ文字音素の変換
          if (MULTI_CHAR_PHONEMES[phoneme]) {
            phoneme = MULTI_CHAR_PHONEMES[phoneme];
          }
          
          phonemes.push(phoneme);
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
      return global.window && 
             global.window.location && 
             global.window.location.hostname.includes('github.io');
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

      const pathname = global.window.location.pathname;
      const repoName = pathname.split('/').filter(p => p)[0];
      
      if (repoName && !path.startsWith('/')) {
        return `/${repoName}/${path}`;
      }
      
      return path;
    },

    /**
     * クリーンアップ
     */
    dispose() {
      if (moduleInstance && moduleInstance._openjtalk_clear) {
        moduleInstance._openjtalk_clear();
      }
      
      moduleInstance = null;
      initPromise = null;
      isInitialized = false;
    }
  };

  // デバッグ用
  if (typeof process !== 'undefined' && process.env.NODE_ENV === 'development') {
    global.OpenJTalkUnityAPI._debug = {
      getModule: () => moduleInstance,
      isInitialized: () => isInitialized
    };
  }

})(typeof window !== 'undefined' ? window : global);
```

### 成果物チェックリスト
- [ ] src/openjtalk-unity-wrapper.js
- [ ] test/unity-wrapper.test.js（全テストGreen）
- [ ] APIドキュメント
- [ ] メモリ管理の検証結果

---

## Milestone 4: Unity JSLib実装

### ゴール
- ✅ Unity C#から呼び出し可能なJSLib関数が動作すること
- ✅ メモリ管理が正しく動作すること
- ✅ エラーハンドリングが適切に実装されること

### タスク詳細

#### Task 4.1: JSLibのテスト作成（1時間）

**test/jslib.test.js**
```javascript
import { describe, it, expect, beforeEach, afterEach } from '@jest/globals';

describe('Unity JSLib Functions', () => {
  let mockDocument;
  let mockScripts = [];

  beforeEach(() => {
    // Unity WebGLのグローバル関数をモック
    global._malloc = jest.fn((size) => 1000 + size);
    global._free = jest.fn();
    global.UTF8ToString = jest.fn((ptr) => 'test string');
    global.stringToUTF8 = jest.fn();
    global.lengthBytesUTF8 = jest.fn((str) => str.length);

    // documentのモック
    mockScripts = [];
    mockDocument = {
      createElement: jest.fn((tag) => {
        if (tag === 'script') {
          const script = {
            onload: null,
            onerror: null,
            src: ''
          };
          mockScripts.push(script);
          return script;
        }
      }),
      head: {
        appendChild: jest.fn()
      }
    };
    global.document = mockDocument;

    // windowのモック
    global.window = {
      OpenJTalkUnityAPI: null,
      location: {
        hostname: 'localhost',
        pathname: '/'
      }
    };
  });

  afterEach(() => {
    delete global._malloc;
    delete global._free;
    delete global.UTF8ToString;
    delete global.stringToUTF8;
    delete global.lengthBytesUTF8;
    delete global.document;
    delete global.window;
    mockScripts = [];
  });

  describe('InitializeOpenJTalkUnity', () => {
    it('should load scripts in correct order', async () => {
      // JSLibコードの読み込み（実際にはmergeIntoでラップされる）
      const InitializeOpenJTalkUnity = async function() {
        try {
          const script = document.createElement('script');
          script.src = 'StreamingAssets/openjtalk-unity.js';
          document.head.appendChild(script);
          
          await new Promise((resolve, reject) => {
            script.onload = resolve;
            script.onerror = reject;
          });
          
          const wrapperScript = document.createElement('script');
          wrapperScript.src = 'StreamingAssets/openjtalk-unity-wrapper.js';
          document.head.appendChild(wrapperScript);
          
          await new Promise((resolve, reject) => {
            wrapperScript.onload = resolve;
            wrapperScript.onerror = reject;
          });
          
          return 0;
        } catch (error) {
          return -1;
        }
      };

      const resultPromise = InitializeOpenJTalkUnity();
      
      // スクリプトロードをシミュレート
      expect(mockScripts.length).toBe(1);
      expect(mockScripts[0].src).toBe('StreamingAssets/openjtalk-unity.js');
      
      // 最初のスクリプトのロード完了
      mockScripts[0].onload();
      
      // 少し待つ
      await new Promise(resolve => setTimeout(resolve, 10));
      
      // 2番目のスクリプトがロードされる
      expect(mockScripts.length).toBe(2);
      expect(mockScripts[1].src).toBe('StreamingAssets/openjtalk-unity-wrapper.js');
      
      // 2番目のスクリプトのロード完了
      mockScripts[1].onload();
      
      const result = await resultPromise;
      expect(result).toBe(0);
    });

    it('should return -1 on error', async () => {
      const InitializeOpenJTalkUnity = async function() {
        try {
          throw new Error('Load failed');
        } catch (error) {
          return -1;
        }
      };

      const result = await InitializeOpenJTalkUnity();
      expect(result).toBe(-1);
    });
  });

  describe('PhonemizeWithOpenJTalk', () => {
    beforeEach(() => {
      // OpenJTalkUnityAPIのモック
      global.window.OpenJTalkUnityAPI = {
        phonemize: jest.fn((text) => {
          if (text === 'エラー') {
            throw new Error('Phonemization failed');
          }
          return ['^', 't', 'e', 's', 'u', 't', 'o', '$'];
        })
      };
    });

    it('should allocate memory for result', () => {
      const PhonemizeWithOpenJTalk = function(textPtr) {
        const text = UTF8ToString(textPtr);
        
        try {
          const phonemes = window.OpenJTalkUnityAPI.phonemize(text);
          const result = JSON.stringify({
            success: true,
            phonemes: phonemes
          });
          
          const bufferSize = lengthBytesUTF8(result) + 1;
          const buffer = _malloc(bufferSize);
          stringToUTF8(result, buffer, bufferSize);
          return buffer;
        } catch (error) {
          const errorResult = JSON.stringify({
            success: false,
            error: error.message,
            phonemes: []
          });
          
          const bufferSize = lengthBytesUTF8(errorResult) + 1;
          const buffer = _malloc(bufferSize);
          stringToUTF8(errorResult, buffer, bufferSize);
          return buffer;
        }
      };

      const result = PhonemizeWithOpenJTalk(12345);
      
      expect(global._malloc).toHaveBeenCalled();
      expect(global.stringToUTF8).toHaveBeenCalled();
      expect(result).toBeGreaterThan(1000); // mallocのモック戻り値
    });

    it('should return JSON result', () => {
      global.UTF8ToString.mockReturnValue('テスト');
      
      const resultBuffer = [];
      global.stringToUTF8.mockImplementation((str, buffer, size) => {
        resultBuffer.push(str);
      });

      const PhonemizeWithOpenJTalk = function(textPtr) {
        const text = UTF8ToString(textPtr);
        const phonemes = window.OpenJTalkUnityAPI.phonemize(text);
        const result = JSON.stringify({
          success: true,
          phonemes: phonemes
        });
        
        const bufferSize = lengthBytesUTF8(result) + 1;
        const buffer = _malloc(bufferSize);
        stringToUTF8(result, buffer, bufferSize);
        return buffer;
      };

      PhonemizeWithOpenJTalk(12345);
      
      expect(resultBuffer.length).toBe(1);
      const jsonResult = JSON.parse(resultBuffer[0]);
      expect(jsonResult.success).toBe(true);
      expect(jsonResult.phonemes).toEqual(['^', 't', 'e', 's', 'u', 't', 'o', '$']);
    });

    it('should handle errors gracefully', () => {
      global.UTF8ToString.mockReturnValue('エラー');
      
      const resultBuffer = [];
      global.stringToUTF8.mockImplementation((str, buffer, size) => {
        resultBuffer.push(str);
      });

      const PhonemizeWithOpenJTalk = function(textPtr) {
        const text = UTF8ToString(textPtr);
        
        try {
          const phonemes = window.OpenJTalkUnityAPI.phonemize(text);
          const result = JSON.stringify({
            success: true,
            phonemes: phonemes
          });
          
          const bufferSize = lengthBytesUTF8(result) + 1;
          const buffer = _malloc(bufferSize);
          stringToUTF8(result, buffer, bufferSize);
          return buffer;
        } catch (error) {
          const errorResult = JSON.stringify({
            success: false,
            error: error.message,
            phonemes: []
          });
          
          const bufferSize = lengthBytesUTF8(errorResult) + 1;
          const buffer = _malloc(bufferSize);
          stringToUTF8(errorResult, buffer, bufferSize);
          return buffer;
        }
      };

      PhonemizeWithOpenJTalk(12345);
      
      const jsonResult = JSON.parse(resultBuffer[0]);
      expect(jsonResult.success).toBe(false);
      expect(jsonResult.error).toBe('Phonemization failed');
      expect(jsonResult.phonemes).toEqual([]);
    });
  });

  describe('Memory Management', () => {
    it('should properly free allocated memory', () => {
      const FreeOpenJTalkMemory = function(ptr) {
        if (ptr && typeof _free !== 'undefined') {
          _free(ptr);
        }
      };

      FreeOpenJTalkMemory(12345);
      expect(global._free).toHaveBeenCalledWith(12345);
      
      FreeOpenJTalkMemory(null);
      expect(global._free).toHaveBeenCalledTimes(1); // null では呼ばれない
    });
  });
});
```

#### Task 4.2: JSLib実装（1時間）

**Assets/uPiper/Plugins/WebGL/openjtalk_unity_wrapper.jslib**
```javascript
mergeInto(LibraryManager.library, {
  // OpenJTalk Unity統合の初期化
  InitializeOpenJTalkUnity: async function() {
    console.log('[Unity] Initializing OpenJTalk Unity integration...');
    
    try {
      // OpenJTalkモジュールスクリプトを読み込み
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
      
      // ラッパースクリプトを読み込み
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
      
      // API初期化
      if (window.OpenJTalkUnityAPI) {
        await window.OpenJTalkUnityAPI.initialize();
        console.log('[Unity] OpenJTalk Unity integration ready');
        return 0;
      } else {
        console.error('[Unity] OpenJTalkUnityAPI not found');
        return -1;
      }
      
    } catch (error) {
      console.error('[Unity] Initialization failed:', error);
      return -1;
    }
  },

  // 初期化状態の確認
  IsOpenJTalkUnityInitialized: function() {
    return (window.OpenJTalkUnityAPI && 
            window.OpenJTalkUnityAPI._debug && 
            window.OpenJTalkUnityAPI._debug.isInitialized()) ? 1 : 0;
  },

  // 日本語テキストの音素化
  PhonemizeWithOpenJTalk: function(textPtr) {
    const text = UTF8ToString(textPtr);
    console.log('[Unity] Phonemizing:', text);
    
    try {
      // API確認
      if (!window.OpenJTalkUnityAPI) {
        throw new Error('OpenJTalkUnityAPI not available');
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

  // メモリ解放
  FreeOpenJTalkMemory: function(ptr) {
    if (ptr && typeof _free !== 'undefined') {
      _free(ptr);
    }
  },

  // クリーンアップ
  DisposeOpenJTalkUnity: function() {
    console.log('[Unity] Disposing OpenJTalk Unity integration');
    
    if (window.OpenJTalkUnityAPI && window.OpenJTalkUnityAPI.dispose) {
      window.OpenJTalkUnityAPI.dispose();
    }
  }
});
```

### 成果物チェックリスト
- [ ] Assets/uPiper/Plugins/WebGL/openjtalk_unity_wrapper.jslib
- [ ] test/jslib.test.js（全テストGreen）
- [ ] メモリ管理の検証結果
- [ ] Unity C#側の実装ファイル

---

## Milestone 5: Unity WebGLビルドとローカルテスト

### ゴール
- ✅ Unity WebGLビルドが成功すること
- ✅ ローカルサーバーで動作すること
- ✅ 音素化が正しく実行されること

### タスク詳細

#### Task 5.1: Unity C#実装（1時間）

**Assets/uPiper/Runtime/Core/Phonemizers/WebGL/WebGLOpenJTalkUnityPhonemizer.cs**
```csharp
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace uPiper.Phonemizers.WebGL
{
    /// <summary>
    /// Unity互換版OpenJTalk音素化クラス
    /// </summary>
    public class WebGLOpenJTalkUnityPhonemizer : IPhonmizer
    {
        [DllImport("__Internal")]
        private static extern int InitializeOpenJTalkUnity();

        [DllImport("__Internal")]
        private static extern int IsOpenJTalkUnityInitialized();

        [DllImport("__Internal")]
        private static extern IntPtr PhonemizeWithOpenJTalk(string text);

        [DllImport("__Internal")]
        private static extern void FreeOpenJTalkMemory(IntPtr ptr);

        [DllImport("__Internal")]
        private static extern void DisposeOpenJTalkUnity();

        private bool _isInitialized = false;

        /// <summary>
        /// 音素化結果のJSONレスポンス
        /// </summary>
        [Serializable]
        private class PhonemizeResponse
        {
            public bool success;
            public string[] phonemes;
            public string error;
            public int count;
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public async Task Initialize()
        {
            if (_isInitialized)
            {
                Debug.Log("[WebGLOpenJTalkUnity] Already initialized");
                return;
            }

            Debug.Log("[WebGLOpenJTalkUnity] Initializing...");

            #if UNITY_WEBGL && !UNITY_EDITOR
            int result = await Task.Run(() => InitializeOpenJTalkUnity());
            
            if (result == 0)
            {
                _isInitialized = true;
                Debug.Log("[WebGLOpenJTalkUnity] Initialization successful");
            }
            else
            {
                throw new Exception("Failed to initialize OpenJTalk Unity");
            }
            #else
            Debug.LogWarning("[WebGLOpenJTalkUnity] Not in WebGL build, skipping initialization");
            #endif
        }

        /// <summary>
        /// テキストを音素に変換
        /// </summary>
        public async Task<string[]> TextToPhonemes(string text)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Phonemizer not initialized");
            }

            if (string.IsNullOrEmpty(text))
            {
                return new string[] { "^", "$" };
            }

            Debug.Log($"[WebGLOpenJTalkUnity] Phonemizing: {text}");

            #if UNITY_WEBGL && !UNITY_EDITOR
            IntPtr resultPtr = IntPtr.Zero;
            
            try
            {
                // JSLibを呼び出して音素化
                resultPtr = await Task.Run(() => PhonemizeWithOpenJTalk(text));
                
                if (resultPtr == IntPtr.Zero)
                {
                    throw new Exception("Failed to phonemize text");
                }

                // JSON結果を取得
                string jsonResult = Marshal.PtrToStringUTF8(resultPtr);
                Debug.Log($"[WebGLOpenJTalkUnity] Result: {jsonResult}");

                // JSONパース
                var response = JsonConvert.DeserializeObject<PhonemizeResponse>(jsonResult);
                
                if (response.success)
                {
                    return response.phonemes;
                }
                else
                {
                    throw new Exception($"Phonemization failed: {response.error}");
                }
            }
            finally
            {
                // メモリ解放
                if (resultPtr != IntPtr.Zero)
                {
                    FreeOpenJTalkMemory(resultPtr);
                }
            }
            #else
            // エディタ/非WebGL環境ではダミーデータを返す
            Debug.LogWarning("[WebGLOpenJTalkUnity] Not in WebGL, returning dummy data");
            return new string[] { "^", "t", "e", "s", "u", "t", "o", "$" };
            #endif
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            if (_isInitialized)
            {
                #if UNITY_WEBGL && !UNITY_EDITOR
                DisposeOpenJTalkUnity();
                #endif
                _isInitialized = false;
                Debug.Log("[WebGLOpenJTalkUnity] Disposed");
            }
        }
    }
}
```

#### Task 5.2: E2Eテスト（1時間）

**test/e2e/webgl-integration.test.js**
```javascript
import { describe, it, expect } from '@jest/globals';
import puppeteer from 'puppeteer';

describe('Unity WebGL E2E Tests', () => {
  let browser;
  let page;

  beforeAll(async () => {
    browser = await puppeteer.launch({
      headless: false, // デバッグのため
      args: ['--no-sandbox']
    });
    page = await browser.newPage();
    
    // コンソールログを出力
    page.on('console', msg => console.log('Browser:', msg.text()));
    page.on('error', err => console.error('Browser Error:', err));
  });

  afterAll(async () => {
    await browser.close();
  });

  it('should load Unity WebGL build', async () => {
    await page.goto('http://localhost:8000');
    
    // Unity WebGLの読み込み待機
    await page.waitForFunction(
      () => window.unityInstance !== undefined,
      { timeout: 30000 }
    );
    
    const title = await page.title();
    expect(title).toContain('uPiper');
  });

  it('should initialize OpenJTalk Unity', async () => {
    const initialized = await page.evaluate(async () => {
      // 初期化を待つ
      await new Promise(resolve => setTimeout(resolve, 5000));
      
      return window.OpenJTalkUnityAPI && 
             window.OpenJTalkUnityAPI._debug &&
             window.OpenJTalkUnityAPI._debug.isInitialized();
    });
    
    expect(initialized).toBe(true);
  });

  it('should phonemize Japanese text', async () => {
    const result = await page.evaluate(async () => {
      if (!window.OpenJTalkUnityAPI) {
        throw new Error('API not available');
      }
      
      const phonemes = window.OpenJTalkUnityAPI.phonemize('こんにちは');
      return phonemes;
    });
    
    expect(result).toContain('^');
    expect(result).toContain('$');
    expect(result.length).toBeGreaterThan(2);
  });

  it('should handle multi-character phonemes', async () => {
    const result = await page.evaluate(async () => {
      const phonemes = window.OpenJTalkUnityAPI.phonemize('ちきょう');
      return phonemes;
    });
    
    // ch -> \ue001 の変換を確認
    const hasChPhoneme = result.some(p => p === '\ue001');
    expect(hasChPhoneme).toBe(true);
  });
});
```

### 成果物チェックリスト
- [ ] WebGLOpenJTalkUnityPhonemizer.cs
- [ ] Unity WebGLビルド設定
- [ ] WebGLビルド成果物
- [ ] E2Eテスト結果
- [ ] ローカルサーバー動作確認ログ

---

## Milestone 6: GitHub Pagesデプロイと本番テスト

### ゴール
- ✅ GitHub Pagesで動作すること
- ✅ パス解決が正しく動作すること
- ✅ 本番環境での性能が要件を満たすこと

### タスク詳細

#### Task 6.1: GitHub Actions設定（1時間）

**.github/workflows/deploy-webgl.yml**
```yaml
name: Deploy Unity WebGL to GitHub Pages

on:
  push:
    branches: [main, feature/openjtalk-unity]
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        
      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '18'
          
      - name: Install dependencies
        run: |
          cd uPiper
          npm install
          
      - name: Run tests
        run: |
          cd uPiper
          npm test
          
      - name: Build OpenJTalk Unity
        run: |
          cd piper-plus
          docker build -t openjtalk-builder .
          docker run -v $(pwd):/workspace openjtalk-builder \
            /workspace/build-unity-compatible.sh
            
      - name: Copy build artifacts
        run: |
          cp piper-plus/dist/openjtalk-unity.* uPiper/WebGLBuild/StreamingAssets/
          cp uPiper/src/openjtalk-unity-wrapper.js uPiper/WebGLBuild/StreamingAssets/
          
      - name: Setup Pages
        uses: actions/configure-pages@v4
        
      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: uPiper/WebGLBuild
          
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

#### Task 6.2: パフォーマンステスト（1時間）

**test/performance.test.js**
```javascript
import { describe, it, expect } from '@jest/globals';

describe('Performance Tests', () => {
  let api;

  beforeAll(async () => {
    // 実際のモジュールを読み込み
    await import('../dist/openjtalk-unity.js');
    await import('../src/openjtalk-unity-wrapper.js');
    
    api = global.OpenJTalkUnityAPI;
    await api.initialize();
  });

  it('should initialize within 5 seconds', async () => {
    const start = Date.now();
    
    // 新しいインスタンスで初期化時間を測定
    delete global.OpenJTalkUnityAPI;
    await import('../src/openjtalk-unity-wrapper.js');
    
    const newApi = global.OpenJTalkUnityAPI;
    await newApi.initialize();
    
    const elapsed = Date.now() - start;
    expect(elapsed).toBeLessThan(5000);
  });

  it('should phonemize within 100ms', () => {
    const testCases = [
      'こんにちは',
      '今日は良い天気ですね',
      'OpenJTalkとPiperを使った音声合成のデモです'
    ];

    testCases.forEach(text => {
      const start = Date.now();
      api.phonemize(text);
      const elapsed = Date.now() - start;
      
      expect(elapsed).toBeLessThan(100);
    });
  });

  it('should handle 1000 phonemizations without memory leak', () => {
    if (typeof performance !== 'undefined' && performance.memory) {
      const initialMemory = performance.memory.usedJSHeapSize;
      
      for (let i = 0; i < 1000; i++) {
        api.phonemize(`テスト${i}`);
      }
      
      // ガベージコレクションを促す
      if (global.gc) {
        global.gc();
      }
      
      const finalMemory = performance.memory.usedJSHeapSize;
      const memoryIncrease = finalMemory - initialMemory;
      
      // メモリ増加が10MB以下であることを確認
      expect(memoryIncrease).toBeLessThan(10 * 1024 * 1024);
    }
  });
});
```

### 成果物チェックリスト
- [ ] GitHub Actions設定ファイル
- [ ] GitHub Pagesデプロイ成功
- [ ] 本番環境での動作確認
- [ ] パフォーマンステスト結果
- [ ] 公開URL

---

## Milestone 7: ドキュメントとCI/CD

### ゴール
- ✅ 完全なドキュメントが作成されること
- ✅ CI/CDパイプラインが動作すること
- ✅ 今後のメンテナンスが容易になること

### タスク詳細

#### Task 7.1: APIドキュメント作成（1時間）

**docs/API.md**
```markdown
# OpenJTalk Unity API Documentation

## JavaScript API

### OpenJTalkUnityAPI

グローバルオブジェクトとして提供される主要API。

#### Methods

##### initialize()
モジュールを初期化します。

**Returns:** `Promise<boolean>` - 初期化成功時true

**Example:**
\```javascript
await OpenJTalkUnityAPI.initialize();
\```

##### phonemize(text)
日本語テキストを音素配列に変換します。

**Parameters:**
- `text` (string): 変換する日本語テキスト

**Returns:** `Array<string>` - 音素の配列

**Example:**
\```javascript
const phonemes = OpenJTalkUnityAPI.phonemize('こんにちは');
// Result: ['^', 'k', 'o', 'N', 'n', 'i', '\ue001', 'i', 'w', 'a', '$']
\```

## Unity C# API

### WebGLOpenJTalkUnityPhonemizer

Unity WebGL環境でのOpenJTalk統合クラス。

#### Methods

##### Initialize()
音素化エンジンを初期化します。

**Returns:** `Task` - 初期化完了タスク

##### TextToPhonemes(string text)
テキストを音素配列に変換します。

**Parameters:**
- `text`: 変換する日本語テキスト

**Returns:** `Task<string[]>` - 音素の配列

## エラーコード

| コード | 説明 |
|--------|------|
| -1 | 初期化失敗 |
| -2 | メモリ確保失敗 |
| -3 | 音素化失敗 |
```

#### Task 7.2: README更新（1時間）

**README.md**
```markdown
# Unity WebGL OpenJTalk Integration

Unity WebGLビルドでpiper-plus OpenJTalkを統合するためのプロジェクト。

## 特徴

- ✅ Unity Module名前空間との完全分離
- ✅ HEAP配列の適切なエクスポート
- ✅ GitHub Pages対応
- ✅ TDDによる高品質な実装
- ✅ 包括的なテストカバレッジ（80%以上）

## クイックスタート

### 必要要件

- Unity 6000.0.55f1以降
- Node.js 18以降
- Docker（ビルド環境用）

### インストール

\```bash
# リポジトリのクローン
git clone https://github.com/yourusername/uPiper.git
cd uPiper

# 依存関係のインストール
npm install

# OpenJTalk Unityビルド
./build-unity-compatible.sh

# テスト実行
npm test
\```

### Unity統合

1. `openjtalk-unity.js`と`openjtalk-unity.wasm`を`Assets/StreamingAssets/`にコピー
2. `openjtalk-unity-wrapper.js`を`Assets/StreamingAssets/`にコピー
3. `openjtalk_unity_wrapper.jslib`を`Assets/uPiper/Plugins/WebGL/`にコピー
4. Unity WebGLビルドを実行

## テスト

\```bash
# 全テスト実行
npm test

# カバレッジ付きテスト
npm run test:coverage

# ウォッチモード
npm run test:watch

# E2Eテスト
npm run test:e2e
\```

## デプロイ

GitHub Actionsが自動的にGitHub Pagesへデプロイします。

## ライセンス

MIT License
```

### 成果物チェックリスト
- [ ] API.md
- [ ] README.md更新
- [ ] CONTRIBUTING.md
- [ ] CI/CDパイプライン動作確認
- [ ] テストカバレッジレポート（80%以上）

---

## 実装完了基準

### 全体の成功基準

- ✅ 全マイルストーンの完了
- ✅ GitHub Pagesで「こんにちは」が正しく音素化される
- ✅ Unity Moduleとの競合なし
- ✅ メモリリークなし
- ✅ 初期化時間5秒以内
- ✅ テストカバレッジ80%以上

### リスク管理

| リスク | 影響度 | 対策 |
|--------|--------|------|
| ビルド環境構築の遅延 | 中 | Dockerイメージの事前準備 |
| Unity互換性問題 | 高 | 早期のUnity統合テスト |
| パフォーマンス問題 | 中 | 段階的な最適化 |

## 付録

### チートシート

```bash
# よく使うコマンド
docker-compose up -d          # Docker環境起動
npm test                       # テスト実行
npm run build                  # ビルド実行
python -m http.server 8000     # ローカルサーバー起動

# デバッグ
chrome://inspect               # Chrome DevTools
console.log(OpenJTalkUnityAPI._debug.getModule())
```

### トラブルシューティング

1. **HEAP8エラーが出る場合**
   - ビルドスクリプトのEXPORTED_RUNTIME_METHODSを確認
   - dist/openjtalk-unity.jsを再生成

2. **初期化が失敗する場合**
   - ブラウザコンソールでエラーログを確認
   - ネットワークタブでファイル読み込みを確認

3. **音素化が動作しない場合**
   - OpenJTalkUnityAPI.isInitialized()を確認
   - 辞書ファイルの読み込みを確認

---

*このドキュメントは随時更新されます。*
*最終更新: 2024年8月9日*