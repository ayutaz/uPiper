# WebGL セットアップガイド

## 前提条件

- Unity 2022.3 LTS 以降
- Sentis パッケージ（Unity.InferenceEngine）2.5.0 以降
- 対応ブラウザ:
  - Chrome 90+ / Edge 90+（WebGL2）
  - Chrome 113+（WebGPU、`chrome://flags` で有効化が必要な場合あり）
  - Firefox 89+（WebGL2）
  - Safari 15+（WebGL2）

## 推論バックエンド

WebGL 環境では、ブラウザの Graphics API に応じてバックエンドが自動選択されます。

| Graphics API | 自動選択バックエンド | 備考 |
|-------------|-------------------|------|
| WebGL2 | GPUPixel | VITS モデルとの互換性良好 |
| WebGPU | GPUCompute | Compute Shader ネイティブ対応 |

```csharp
// 推奨: Auto で環境に最適なバックエンドが選択される
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto
};
```

## セットアップ手順

### 1. ビルドターゲットの切り替え

1. **File > Build Settings** を開く
2. **WebGL** を選択
3. **Switch Platform** をクリック

### 2. Player Settings の設定

**Edit > Project Settings > Player > WebGL** で以下を設定します。

| 設定項目 | 推奨値 | 備考 |
|---------|--------|------|
| Compression Format | Gzip または Brotli | サーバーが対応している形式を選択 |
| Decompression Fallback | 有効 | サーバーが圧縮ヘッダーを返さない場合の保険 |
| Data Caching | 有効 | ブラウザキャッシュ活用 |
| Memory Size | 512MB 以上 | モデル読み込みに必要 |

### 3. ユーザーインタラクションゲートの設定

ブラウザのセキュリティポリシーにより、ユーザーのクリック/タップなしに AudioContext を開始できません。uPiper は `WebGLInteractionGate` コンポーネントでこの制約に対応します。

```csharp
using uPiper.Core.Platform;

public class WebGLTTSManager : MonoBehaviour
{
    private async void Start()
    {
        // ユーザーのクリック/タップを待機
        // 全画面オーバーレイが表示され、クリックで AudioContext が再開される
        // WebGL 以外のプラットフォームでは即座に完了する
        await WebGLInteractionGate.WaitForInteractionAsync();

        // AudioContext が有効になった後に TTS を初期化
        var config = new PiperConfig { Backend = InferenceBackend.Auto };
        var tts = new PiperTTS(config);
        await tts.InitializeAsync();
    }
}
```

`WebGLInteractionGate` の動作:
- 全画面の半透明オーバーレイを表示（ブラウザ言語に合わせたローカライズメッセージ付き）
- ユーザーのクリック/タップで `WebGL_ResumeAudioContext()` を呼び出し
- AudioContext が既に `running` 状態の場合はスキップ
- 対応言語: ja, en, zh, es, fr, pt, ko

カスタムメッセージを表示したい場合:

```csharp
var gate = gameObject.AddComponent<WebGLInteractionGate>();
gate.OverlayMessage = "音声を再生するにはクリックしてください";
```

### 4. StreamingAssets の非同期読み込み

WebGL では直接ファイルシステムにアクセスできないため、`WebGLStreamingAssetsLoader` を使用して非同期にファイルを読み込みます。uPiper 内部で自動的に使用されますが、カスタム辞書を動的に読み込む場合は直接利用できます。

```csharp
using uPiper.Core.Platform;

// テキストファイルの読み込み
var json = await WebGLStreamingAssetsLoader.LoadTextAsync(
    "uPiper/Dictionaries/user_custom_dict.json");

// バイナリファイルの読み込み（進捗レポート付き）
var data = await WebGLStreamingAssetsLoader.LoadBytesAsync(
    "uPiper/pua.json",
    new Progress<float>(p => Debug.Log($"Loading: {p:P0}")));
```

### 5. IndexedDB キャッシュ

WebGL ビルドでは、辞書データなどをブラウザの IndexedDB に永続キャッシュできます。これによりセッション間でデータを保持し、再読み込みを高速化します。

```csharp
using uPiper.Core.Platform;

// キャッシュにデータを保存
await IndexedDBCache.StoreAsync("sys.dic", dictionaryBytes, "v1.0");

// キャッシュからデータを読み込み（存在しない場合は null）
var cached = await IndexedDBCache.LoadAsync("sys.dic");

// バージョン付きキャッシュの存在確認
bool exists = await IndexedDBCache.HasKeyAsync("sys.dic", "v1.0");

// キャッシュの削除
IndexedDBCache.Delete("sys.dic");
```

IndexedDB キャッシュの特徴:
- DB 名: `uPiper-cache`、ストア名: `dictionaries`
- バージョン文字列によるキャッシュ無効化
- WebGL 以外のプラットフォームでは全操作がノーオペレーション
- WebGL はシングルスレッドのためロック不要

### 6. ローディング UI

初期化中のユーザー向けに `WebGLLoadingPanel` コンポーネントを使用して進捗表示を行えます。

```csharp
// WebGLLoadingPanel をシーンに配置し、Slider / TextMeshProUGUI をアサイン
[SerializeField] private WebGLLoadingPanel _loadingPanel;

private async void Start()
{
    _loadingPanel.Show();

    _loadingPanel.SetProgress(0.1f, "モデルを読み込み中...");
    var config = new PiperConfig { Backend = InferenceBackend.Auto };
    var tts = new PiperTTS(config);
    await tts.InitializeAsync();

    _loadingPanel.SetProgress(1.0f, "準備完了");
    _loadingPanel.Hide();
}
```

## 大容量ファイルの分割（GitHub Pages デプロイ）

GitHub Pages にはファイルサイズ 100MB の制限があります。`WebGLSplitDataProcessor` が 100MB を超えるビルドファイルを 90MB チャンクに自動分割します。

### 仕組み

1. **WebGLSplitDataProcessor**: Build/ ディレクトリ内の大容量ファイルを検出し分割
   - チャンク名: `.partaa`, `.partab`, `.partac`, ...
   - メタデータ: `.split-meta`（チャンク数とサイズ情報）
2. **split-file-loader.js**: ブラウザの `fetch` / `XMLHttpRequest` をインターセプトし、分割チャンクを並列ダウンロード後に結合して返却
3. **github-pages-adapter.js**: `username.github.io/repo-name/` 形式のパスを自動解決し、Unity ローダーの config パスを調整

### Editor からの手動実行

```
uPiper > Development > Prepare WebGL for GitHub Pages
```

> このメニューは `UPIPER_DEVELOPMENT` 定義シンボルが必要です。

### CI 環境

CI 環境（`deploy-webgl.yml`）では bash スクリプトが同等の処理を実行するため、Editor ツールは不要です。

## コード例

```csharp
using uPiper.Core;
using uPiper.Core.Platform;
using UnityEngine;

public class WebGLTTSDemo : MonoBehaviour
{
    private PiperTTS _tts;

    private async void Start()
    {
        // 1. ユーザーインタラクションを待機
        await WebGLInteractionGate.WaitForInteractionAsync();

        // 2. TTS初期化
        var config = new PiperConfig
        {
            Backend = InferenceBackend.Auto
        };
        _tts = new PiperTTS(config);
        await _tts.InitializeAsync();

        // 3. 音声合成
        var result = await _tts.PhonemizeAsync("Hello, world!");
        var request = SynthesisRequest.FromPhonemesWithProsody(
            result.Phonemes, result.ProsodyFlat);
        var clip = await _tts.SynthesizeAsync(request);

        var audioSource = GetComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.Play();
    }

    private void OnDestroy()
    {
        _tts?.Dispose();
    }
}
```

## WebGL 固有の制約

| 制約 | 説明 | 対応 |
|------|------|------|
| `Task.Run` 不可 | WebGL はシングルスレッド | uPiper 内部でメインスレッド非同期処理に自動切替 |
| 同期ファイル I/O 不可 | ファイルシステムアクセスなし | `WebGLStreamingAssetsLoader` で非同期読み込み |
| AudioContext 制限 | ユーザージェスチャーが必要 | `WebGLInteractionGate` で対応 |
| メモリ制限 | ブラウザのメモリ上限あり | Player Settings でメモリサイズを適切に設定 |

## よくある問題

### 音声が再生されない

**原因**: AudioContext がユーザーインタラクション前に作成された。

**解決策**: `WebGLInteractionGate.WaitForInteractionAsync()` を `PiperTTS.InitializeAsync()` の前に呼び出す。

### モデル読み込みエラー

**原因**: StreamingAssets のパスが正しくない、またはサーバーの CORS / MIME タイプ設定。

**解決策**:
1. ブラウザの開発者ツールでネットワークタブを確認
2. `.data`, `.wasm` ファイルの MIME タイプが正しいか確認
3. `Decompression Fallback` を有効にする

### GitHub Pages で 404 エラー

**原因**: リポジトリ名がパスに含まれていないか、ファイルサイズ制限を超えている。

**解決策**:
1. `github-pages-adapter.js` がビルド出力に含まれているか確認
2. 100MB を超えるファイルは `WebGLSplitDataProcessor` で分割
3. `split-file-loader.js` がビルド出力に含まれているか確認

### メモリ不足（Out of Memory）

**原因**: ブラウザのメモリ割り当てが不足。

**解決策**:
1. Player Settings > WebGL > Memory Size を 512MB 以上に設定
2. 不要なアセットを削除してビルドサイズを削減
3. Chrome の場合、64-bit 版を使用

### ローディングが遅い

**解決策**:
1. Brotli 圧縮を使用（サーバーが対応している場合）
2. IndexedDB キャッシュを活用
3. CDN の利用を検討
4. `WebGLLoadingPanel` で進捗表示を行いユーザー体験を改善