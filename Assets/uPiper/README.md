# uPiper - Unity Piper TTS Plugin

高品質な音声合成を Unity で実現する Piper TTS プラグインです。

## 特徴

- 🎌 **日本語対応**: OpenJTalk による高精度な日本語音素化
- 🚀 **高速処理**: Unity AI Interface による最適化された推論
- 🎮 **マルチプラットフォーム**: Windows, Linux, macOS に対応
- 🔧 **簡単な統合**: シンプルな API とエディタ拡張

## インストール

### Unity Package Manager 経由（推奨）

#### ステップ1: パッケージのインストール
1. Unity のメニューから `Window > Package Manager` を開く
2. `+` ボタンをクリックし、`Add package from git URL...` を選択
3. 以下の URL を入力:
   ```
   https://github.com/ayutaz/uPiper.git?path=/Assets/uPiper
   ```

#### ステップ2: 必要なデータのインポート

Package Managerからインストール後、**必ず以下の手順でデータをインポートしてください**：

1. **Package Managerで「In Project」を選択**
2. **「uPiper」パッケージを選択**
3. **「Samples」セクションを展開**
4. **以下のサンプルをインポート**：
   - 📚 **OpenJTalk Dictionary Data** (必須) - 日本語音声合成用辞書
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

1. [Releases](https://github.com/ayutaz/uPiper/releases) から最新版をダウンロード
2. Unity プロジェクトにインポート

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

Package Managerからサンプルをインポートできます：

1. **Unity Package Manager で uPiper を選択**
2. **"Samples" タブを開く**
3. **利用可能なサンプル**：
   - **Basic TTS Demo** - 日本語・英語テキストの音声合成デモ
   - **OpenJTalk Dictionary Data** - 日本語音声合成用辞書（必須）
   - **CMU Pronouncing Dictionary** - 英語音声合成用辞書（必須）
   - **Voice Models** - 高品質音声モデル（推奨）

### Basic TTS Demo
- 日本語・英語テキストの音声合成デモ
- シンプルな UI で TTS を体験
- OpenJTalk による高精度な日本語処理
- リアルタイム音声生成

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

- Unity 6000.0.58f2 以降
- Unity AI Interface (Inference Engine) 2.2.2
- 各プラットフォームの要件:
  - Windows: Windows 10 以降（x64のみ）
  - Linux: Ubuntu 20.04 以降（x86_64, aarch64）
  - macOS: macOS 10.15 以降（Universal）
  - Android: Android 5.0 (API 21) 以降（arm64-v8a, armeabi-v7a, x86, x86_64）
  - iOS: iOS 11.0 以降（ARM64）

## ライセンス

MIT License - 詳細は [LICENSE](../../LICENSE) を参照

## 貢献

貢献を歓迎します！[Contributing Guidelines](../../CONTRIBUTING.md) を参照してください。

## サポート

- 📖 [ドキュメント](https://github.com/ayutaz/uPiper/wiki)
- 🐛 [Issue Tracker](https://github.com/ayutaz/uPiper/issues)
- 💬 [Discussions](https://github.com/ayutaz/uPiper/discussions)