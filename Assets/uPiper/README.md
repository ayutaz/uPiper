# uPiper - Unity Piper TTS Plugin

高品質な音声合成を Unity で実現する Piper TTS プラグインです。

## 特徴

- 🎌 **日本語対応**: dot-net-g2p による高精度な日本語音素化（純粋C#実装）
- 🚀 **高速処理**: Unity AI Inference Engine による最適化された推論
- 🎮 **マルチプラットフォーム**: Windows, Linux, macOS, Android, iOS, WebGL に対応
- 🔧 **簡単な統合**: シンプルな API とエディタ拡張

## インストール

### Unity Package Manager 経由（推奨）

#### ステップ1: パッケージのインストール
1. Unity のメニューから `Window > Package Manager` を開く
2. `+` ボタンをクリックし、`Add package from git URL...` を選択
3. 以下の URL を入力:
   ```
   https://github.com/ayutaz/uPiper.git?path=Assets/uPiper
   ```

#### ステップ2: 必要なデータのインポート

Package Managerからインストール後、**必ず以下の手順でデータをインポートしてください**：

1. **Package Managerで「In Project」を選択**
2. **「uPiper」パッケージを選択**
3. **「Samples」セクションを展開**
4. **以下のサンプルをインポート**：
   - 📚 **MeCab Dictionary Data** (必須) - 日本語音声合成用MeCab辞書
   - 📚 **CMU Pronouncing Dictionary** (必須) - 英語音声合成用辞書
   - 🎤 **Voice Models** (推奨) - 高品質音声モデル
   - 🎮 **Basic TTS Demo** (オプション) - デモシーン

#### ステップ3: データのセットアップ

サンプルをインポートした後：

1. **メニューから `uPiper > Setup > Install from Samples` を実行**
2. インストールダイアログで「Install」をクリック
3. セットアップが完了するまで待つ

#### ステップ4: 動作確認

1. **メニューから `uPiper > Setup > Check Setup Status` を実行**
2. すべての項目が「✓ Installed」になっていることを確認
3. Basic TTS Demoシーンを開いて動作確認

> ⚠️ **重要**: 辞書データをインポートしないとTTS機能は動作しません

### 手動インストール

#### .unitypackage を使用する場合

1. [Releases](https://github.com/ayutaz/uPiper/releases) ページから最新の `.unitypackage` ファイルをダウンロード
2. Unity のメニューから `Assets > Import Package > Custom Package...` を選択
3. ダウンロードした `.unitypackage` ファイルを選択してインポート

> **注意**: `.unitypackage` には DotNetG2P パッケージおよび一部のUnityパッケージが含まれていません。`Packages/manifest.json` の `"dependencies"` に以下のエントリを追加してください（既存のエントリは削除しないでください）：

```jsonc
// Packages/manifest.json の "dependencies" 内に以下を追加:
"com.unity.ai.inference": "2.5.0",
"com.unity.nuget.newtonsoft-json": "3.2.1",
"com.dotnetg2p.core": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Core#v1.8.2",
"com.dotnetg2p.mecab": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.MeCab#v1.8.2",
"com.dotnetg2p.english": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.English#v1.8.2",
"com.dotnetg2p.chinese": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Chinese#v1.8.2",
"com.dotnetg2p.korean": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Korean#v1.8.2",
"com.dotnetg2p.spanish": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Spanish#v1.8.2",
"com.dotnetg2p.french": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.French#v1.8.2",
"com.dotnetg2p.portuguese": "https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Portuguese#v1.8.2"
```

### トラブルシューティング

#### Samplesが1つしか表示されない場合
- Unity Editorを再起動
- Package Managerで「Refresh」ボタンをクリック

#### 辞書ファイルが見つからないエラー
- `uPiper > Setup > Install from Samples` を実行したか確認
- `uPiper > Setup > Check Setup Status` で状態を確認

#### 日本語が文字化けする場合
- Basic TTS Demoに含まれるNotoSansJP-Regular SDFフォントを使用

## 基本的な使い方

```csharp
using UnityEngine;
using uPiper.Core;

public class TTSExample : MonoBehaviour
{
    private IPiperTTS _piperTTS;

    async void Start()
    {
        // 初期化（CreateAsync でモデル自動ロードまで完了）
        _piperTTS = await PiperTTS.CreateAsync();

        // 音声生成
        AudioClip clip = await _piperTTS.GenerateAudioAsync("こんにちは、世界！");

        // 再生
        var audioSource = GetComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.Play();
    }

    void OnDestroy()
    {
        _piperTTS?.Dispose();
    }
}
```

### PhonemizeAsync / SynthesizeAsync API

`PhonemizeAsync` で音素・Prosody情報を取得し、`SynthesizeAsync` で音声を生成する2段階APIです。
音素の加工やProsodyパラメータの調整など、より細かい制御が必要な場合に使用します。

```csharp
// 音素化（Prosody情報付き）
PhonemizeResult result = await _piperTTS.PhonemizeAsync("こんにちは");
// result.Phonemes:        音素配列
// result.ProsodyFlat:     stride=3 フラット配列 [a1_0,a2_0,a3_0, a1_1,a2_1,a3_1, ...]
// result.DetectedLanguage: "ja"

// Prosody付きリクエストを構築して合成
var request = SynthesisRequest.FromPhonemesWithProsody(
    result.Phonemes, result.ProsodyFlat, lengthScale: 0.8f);
AudioClip clip = await _piperTTS.SynthesizeAsync(request);

// 音素直接入力（Prosodyなし）
var request2 = SynthesisRequest.FromPhonemes(
    new[] { "k", "o", "N_uvular", "n", "i", "ch", "w", "a" });
AudioClip clip2 = await _piperTTS.SynthesizeAsync(request2);
```

## Prosody（韻律情報）

Prosody対応モデルでは、dot-net-g2p（MeCab辞書）から取得したアクセント情報を使用してより自然なイントネーションの音声を生成できます。
Prosodyデータは **ProsodyFlat** (stride=3) 形式で管理されます。

```csharp
// PhonemizeAsync で Prosody 付き音素を取得
var result = await _piperTTS.PhonemizeAsync("今日は良い天気です");

// ProsodyFlat: [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...]
// Length = Phonemes.Length * 3
Debug.Log($"音素数: {result.Phonemes.Length}");
Debug.Log($"Prosody有無: {result.HasProsody}");

// Prosody付きリクエストを構築して合成
var request = SynthesisRequest.FromPhonemesWithProsody(
    result.Phonemes, result.ProsodyFlat, lengthScale: 0.8f);
AudioClip clip = await _piperTTS.SynthesizeAsync(request);
```

### Prosodyパラメータ（stride=3）

| パラメータ | 日本語 | 英語 | 中国語 |
|-----------|--------|------|--------|
| **A1** | モーラ位置 | 0 | tone(1-5) |
| **A2** | アクセント核位置 | 0 | 音節位置 |
| **A3** | アクセント句位置 | 0 | 単語長 |

これらの値は自然なイントネーション生成に使用されます。対応言語: ja/en/zh/es/fr/pt/ko

## サンプル

Package Managerからサンプルをインポートできます：

1. **Unity Package Manager で uPiper を選択**
2. **"Samples" タブを開く**
3. **利用可能なサンプル**：
   - **Basic TTS Demo** - 日本語・英語テキストの音声合成デモ
   - **MeCab Dictionary Data** - 日本語音声合成用MeCab辞書（必須）
   - **CMU Pronouncing Dictionary** - 英語音声合成用辞書（必須）
   - **Voice Models** - 高品質音声モデル（推奨）

### Basic TTS Demo
- 日本語・英語テキストの音声合成デモ
- シンプルな UI で TTS を体験
- dot-net-g2p による高精度な日本語処理
- リアルタイム音声生成

## アーキテクチャ

```
uPiper/
├── Runtime/
│   ├── Core/                    # コア API
│   │   ├── AudioGeneration/     # AudioClip生成、ONNX推論、PhonemeEncoder
│   │   ├── Phonemizers/         # 音素化システム
│   │   │   ├── Backend/         # 共有型（PhonemeOptions）
│   │   │   ├── Implementations/ # G2P実装（DotNetG2PPhonemizer）
│   │   │   └── Multilingual/    # 多言語対応（PuaTokenMapper、言語検出）
│   │   │       └── Handlers/    # 7言語G2Pハンドラ
│   │   ├── IL2CPP/              # IL2CPP互換レイヤー
│   │   └── Platform/            # プラットフォーム固有コード
│   └── Demo/                    # デモUI
├── Editor/                      # エディタ拡張
├── Resources/Models/            # ONNXモデルファイル
├── Plugins/                     # プラットフォーム固有プラグイン
├── Tests/                       # テスト（Editor/Runtime）
└── Samples~/                    # サンプルデータ
```

## 必要要件

- Unity 6000.0.58f2 以降
- Unity AI Inference Engine (com.unity.ai.inference) 2.5.0
- 各プラットフォームの要件:
  - Windows: Windows 10 以降（x64のみ）
  - Linux: Ubuntu 20.04 以降（x86_64, aarch64）
  - macOS: macOS 10.15 以降（Universal）
  - Android: Android 5.0 (API 21) 以降（arm64-v8a, armeabi-v7a, x86, x86_64）
  - iOS: iOS 11.0 以降（ARM64）
  - WebGL: WebGPU / WebGL2 対応ブラウザ

## ライセンス

Apache License 2.0 - 詳細は [LICENSE](../../LICENSE) を参照

## 貢献

貢献を歓迎します！[Issue Tracker](https://github.com/ayutaz/uPiper/issues) でバグ報告や機能提案を受け付けています。

## サポート

- 📖 [ドキュメント](https://github.com/ayutaz/uPiper/wiki)
- 🐛 [Issue Tracker](https://github.com/ayutaz/uPiper/issues)
- 💬 [Discussions](https://github.com/ayutaz/uPiper/discussions)