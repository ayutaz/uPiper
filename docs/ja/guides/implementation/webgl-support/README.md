# uPiper WebGL対応ガイド

## 概要

uPiperのWebGL対応により、ブラウザ上で高品質な音声合成が可能になりました。WebAssemblyを使用してOpenJTalk（日本語）とeSpeak-ng（英語）の音素化エンジンを統合し、Unity AI Inference Engineによるニューラル音声合成を実現しています。

## 主な機能

- 🇯🇵 **日本語音声合成**: OpenJTalk WebAssemblyによる高精度な音素化
- 🇺🇸 **英語音声合成**: eSpeak-ng WebAssemblyによる音素化
- 🚀 **高速処理**: WebAssemblyとGPUPixelバックエンドによる最適化
- 💾 **キャッシュ機能**: IndexedDBを使用した音素化結果のキャッシュ
- 🌐 **GitHub Pages対応**: 自動デプロイによるデモ公開

## アーキテクチャ

### 音素化パイプライン

```
テキスト入力
    ↓
言語判定
    ↓
音素化エンジン選択
    ├─ 日本語 → WebGLOpenJTalkPhonemizer
    └─ 英語  → WebGLESpeakPhonemizer
    ↓
WebAssembly呼び出し（JavaScript Bridge）
    ↓
音素配列生成
    ↓
InferenceAudioGenerator（GPUPixel）
    ↓
音声出力
```

### ファイル構造

```
Assets/
├── StreamingAssets/
│   ├── openjtalk.js         # OpenJTalk WebAssemblyモジュール
│   ├── openjtalk.wasm       # OpenJTalk WebAssemblyバイナリ
│   ├── dict/                # OpenJTalk辞書ファイル
│   ├── espeak-ng/           # eSpeak-ng WebAssemblyファイル
│   └── voice/               # 音声ファイル（ダミー）
├── uPiper/
│   ├── Plugins/WebGL/
│   │   ├── openjtalk_wrapper.jslib    # OpenJTalk JavaScript Bridge
│   │   ├── espeak_wrapper.jslib       # eSpeak-ng JavaScript Bridge
│   │   └── indexeddb_cache.jslib      # キャッシュ管理
│   └── Runtime/Core/Phonemizers/WebGL/
│       ├── WebGLOpenJTalkPhonemizer.cs
│       ├── WebGLESpeakPhonemizer.cs
│       └── WebGLCacheManager.cs
└── WebGLTemplates/
    └── uPiperTemplate/      # カスタムWebGLテンプレート
```

## ビルド方法

### Unity Editorからのビルド

1. **ビルド設定**
   ```
   Menu: uPiper/Build/Configure Build Settings
   ```

2. **WebGLビルド実行**
   ```
   Menu: uPiper/Build/Build WebGL
   ```

### コマンドラインビルド

```bash
Unity -batchmode -quit \
  -projectPath . \
  -buildTarget WebGL \
  -executeMethod PiperBuildProcessor.BuildWebGL \
  -customBuildPath build \
  -customBuildName uPiperWebGL
```

## CI/CD設定

### GitHub Actions

`.github/workflows/unity-webgl-build.yml`により、以下が自動化されています：

1. **自動ビルド**
   - mainブランチへのプッシュ時
   - Pull Request作成時
   - 手動実行も可能

2. **GitHub Pagesデプロイ**
   - mainブランチのビルドを自動デプロイ
   - https://[username].github.io/uPiper/ で公開

### 必要なSecrets

GitHub リポジトリに以下のSecretsを設定してください：

- `UNITY_LICENSE`: Unity Pro/Plus ライセンス
- `UNITY_EMAIL`: Unity アカウントメール
- `UNITY_PASSWORD`: Unity アカウントパスワード

## 実装詳細

### WebGLOpenJTalkPhonemizer

日本語テキストの音素化を担当：

```csharp
// 非同期初期化
await InitializeInternalAsync(options, cancellationToken);

// 音素化実行
var result = await PhonemizeAsync("こんにちは", "ja");
// result.Phonemes: ["^", "k", "o", "N", "n", "i", "\ue001", "i", "w", "a", "$"]
```

### WebGLESpeakPhonemizer

英語テキストの音素化を担当：

```csharp
// 音素化実行
var result = await PhonemizeAsync("Hello world", "en");
// result.Phonemes: ["^", "h", "ɛ", "l", "oʊ", " ", "w", "ɜː", "r", "l", "d", "$"]
```

### JavaScript Bridge

`openjtalk_wrapper.jslib`の例：

```javascript
InitializeOpenJTalkWeb: function() {
    // WebAssemblyモジュールの動的ロード
    const module = await OpenJTalkModule({
        locateFile: (path) => {
            if (path.endsWith('.wasm')) {
                return 'StreamingAssets/openjtalk.wasm';
            }
            return path;
        }
    });
    
    // 辞書ファイルのロード
    await loadDictionary();
    
    // 初期化完了
    window.uPiperOpenJTalk.initialized = true;
}
```

## パフォーマンス最適化

### メモリ管理

- Unity WebGLヒープサイズ: 1GB（PlayerSettings）
- WebAssemblyモジュール: 約50MB（OpenJTalk）+ 約20MB（eSpeak-ng）

### キャッシュ戦略

- IndexedDBによる音素化結果のキャッシュ
- 同一テキストの再音素化を回避
- ブラウザセッション間でのキャッシュ永続化

### ロード時間短縮

- Gzip圧縮有効化
- WebAssembly Streaming Instantiation
- 遅延ロード（必要時にモジュール初期化）

## トラブルシューティング

### ビルドエラー

1. **WebGLメモリ不足**
   ```
   PlayerSettings.WebGL.memorySize = 1024; // 1GBに増加
   ```

2. **テンプレートが見つからない**
   - `Assets/WebGLTemplates/uPiperTemplate`が存在することを確認

### 実行時エラー

1. **WebAssemblyロード失敗**
   - StreamingAssetsにWASMファイルが含まれているか確認
   - ブラウザコンソールでネットワークエラーを確認

2. **音素化失敗**
   - 辞書ファイルが正しくロードされているか確認
   - ブラウザコンソールでJavaScriptエラーを確認

## 今後の改善点

- [ ] 中国語音素化エンジンの統合
- [ ] Service Workerによるオフライン対応
- [ ] WebGPUバックエンドのサポート
- [ ] ストリーミング音声生成
- [ ] より高度なキャッシュ戦略

## 参考資料

- [WebGL対応調査・実装計画](investigation-and-plan.md)
- [Unity WebGL ビルド設定](https://docs.unity3d.com/Manual/webgl-building.html)
- [Emscripten Documentation](https://emscripten.org/docs/)
- [wasm_open_jtalk](https://github.com/taku910/mecab/tree/master/mecab/wasm)