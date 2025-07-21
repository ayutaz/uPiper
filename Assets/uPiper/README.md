# uPiper - Unity Piper TTS Plugin

高品質な音声合成を Unity で実現する Piper TTS プラグインです。

## 特徴

- 🎌 **日本語対応**: OpenJTalk による高精度な日本語音素化
- 🚀 **高速処理**: Unity AI Interface による最適化された推論
- 🎮 **マルチプラットフォーム**: Windows, Linux, macOS に対応
- 🔧 **簡単な統合**: シンプルな API とエディタ拡張

## インストール

### Unity Package Manager 経由

1. Unity のメニューから `Window > Package Manager` を開く
2. `+` ボタンをクリックし、`Add package from git URL...` を選択
3. 以下の URL を入力:
   ```
   https://github.com/ayutaz/uPiper.git?path=/Assets/uPiper
   ```

### 手動インストール

1. [Releases](https://github.com/ayutaz/uPiper/releases) から最新版をダウンロード
2. Unity プロジェクトにインポート

## 基本的な使い方

```csharp
using UnityEngine;
using uPiper.Core;

public class TTSExample : MonoBehaviour
{
    private IPiperTTS piperTTS;
    
    async void Start()
    {
        // 初期化
        piperTTS = new PiperTTS();
        await piperTTS.InitializeAsync();
        
        // 音声生成
        AudioClip clip = await piperTTS.GenerateAudioAsync("こんにちは、世界！");
        
        // 再生
        var audioSource = GetComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.Play();
    }
}
```

## サンプル

Unity Package Manager からサンプルをインポートできます：

1. Package Manager で uPiper を選択
2. "Samples" タブを開く
3. "Basic TTS Demo" の "Import" をクリック

### Basic TTS Demo
- 日本語テキストの音声合成デモ
- シンプルな UI で TTS を体験
- OpenJTalk による高精度な日本語処理

## アーキテクチャ

```
uPiper/
├── Runtime/          # ランタイムコード
│   ├── Core/        # コア API
│   ├── Phonemizers/ # 音素化システム
│   ├── Synthesis/   # 音声合成エンジン
│   ├── Models/      # モデル管理
│   ├── Native/      # ネイティブバインディング
│   └── Utils/       # ユーティリティ
├── Editor/          # エディタ拡張
├── Plugins/         # プラットフォーム別ネイティブライブラリ
├── Models/          # TTS モデルファイル
└── Samples~/        # サンプルプロジェクト
```

## 必要要件

- Unity 6000.0.35f1 以降
- Unity AI Interface (Inference Engine) 2.2.1
- 各プラットフォームの要件:
  - Windows: Windows 10 以降（x64のみ）
  - Linux: Ubuntu 20.04 以降（x86_64, aarch64）
  - macOS: macOS 10.15 以降（Universal）

## ライセンス

MIT License - 詳細は [LICENSE](../../LICENSE) を参照

## 貢献

貢献を歓迎します！[Contributing Guidelines](../../CONTRIBUTING.md) を参照してください。

## サポート

- 📖 [ドキュメント](https://github.com/ayutaz/uPiper/wiki)
- 🐛 [Issue Tracker](https://github.com/ayutaz/uPiper/issues)
- 💬 [Discussions](https://github.com/ayutaz/uPiper/discussions)