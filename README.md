# uPiper

[![Unity Tests](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml)
[![Unity Build](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml)
[![codecov](https://codecov.io/github/ayutaz/uPiper/graph/badge.svg?token=348eb741-4320-4368-89fa-3eee5188bd3f)](https://codecov.io/github/ayutaz/uPiper)

[piper-plus](https://github.com/ayutaz/piper-plus)の高性能Unityプラグイン実装

## 機能

### コア機能 (Phase 1完了)
- 🎤 高品質な音声合成（Piper TTSベース）
- 🌍 多言語対応（日本語、英語など）
- 🚀 Unity.InferenceEngine (Sentis v2.2.1) による高速推論
- 📱 マルチプラットフォーム対応（Windows/macOS/Linux）
- 🔧 OpenJTalkによる高精度な日本語音素化
- 🎯 複数音声モデルの動的読み込みサポート
- 💾 音声キャッシュシステム（LRU方式）
- 🧪 モックモードによるテスト環境対応

### アーキテクチャ特徴
- モジュラー設計（音素化器、音声生成器の分離）
- 非同期/同期APIの両方をサポート
- Unity ModelAssetとONNXファイルパスの両方に対応
- 包括的なエラーハンドリングとロギング

## Requirements
* Unity 6000.0.35f1以上
* [Unity.InferenceEngine](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.2/manual/index.html) 2.2.1
* Burst 1.8.20以上（推奨）

## インストール

### Package Manager経由
1. Unity Package Managerを開く
2. `+`ボタンから「Add package from git URL...」を選択
3. 以下のURLを入力：
   ```
   https://github.com/ayutaz/uPiper.git?path=/Assets/uPiper
   ```

### 手動インストール
1. リポジトリをクローン
2. `Assets/uPiper`フォルダをプロジェクトにコピー

## ビルド

### 自動ビルド（GitHub Actions）
- mainブランチへのプッシュ時に自動的に全プラットフォーム向けのビルドが実行されます
- WebGLビルドはGitHub Pagesに自動デプロイされます
- リリースタグ（v*）をプッシュすると、自動的にリリースが作成されます

### 手動ビルド
1. Unity Editorで `uPiper/Build/Configure Build Settings` を実行
2. `uPiper/Build/Build All Platforms` で全プラットフォームをビルド

### サポートプラットフォーム
- ✅ Windows (x64)
- ✅ macOS (Intel/Apple Silicon)
- ✅ Linux (x64)
- ⚠️ WebGL（制限付き - [Issue #17](https://github.com/ayutaz/uPiper/issues/17)参照）

## 使用方法

### 基本的な使い方

```csharp
using uPiper.Core;
using UnityEngine;

public class TTSExample : MonoBehaviour
{
    private PiperTTS piperTTS;
    
    async void Start()
    {
        // 初期化
        var config = new PiperConfig
        {
            DefaultLanguage = "ja",
            SampleRate = 22050,
            EnablePhonemeCache = true
        };
        
        piperTTS = new PiperTTS(config);
        await piperTTS.InitializeAsync();
        
        // 音声モデルの読み込み
        var voice = new PiperVoiceConfig
        {
            VoiceId = "ja-JP-kokoro",
            Language = "ja",
            ModelPath = "path/to/model.onnx"
        };
        await piperTTS.LoadVoiceAsync(voice);
        
        // 音声生成
        var audioClip = await piperTTS.GenerateAudioAsync("こんにちは、世界！");
        
        // 再生
        var audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.Play();
    }
    
    void OnDestroy()
    {
        piperTTS?.Dispose();
    }
}
```

### ONNXモデルの使用

ONNXモデルは`.sentis`形式に変換する必要があります。詳細は[ONNX統合ガイド](Assets/uPiper/Docs/ONNX_INTEGRATION_GUIDE.md)を参照してください。

## 開発状況

### Phase 1 (完了) ✅
- [x] Phase 1.1-1.3: 基本アーキテクチャとコアAPI
- [x] Phase 1.4-1.6: OpenJTalk統合
- [x] Phase 1.7: 音声後処理
- [x] Phase 1.8: エラーハンドリング
- [x] Phase 1.9-1.11: Unity.InferenceEngine統合と機能完成

### Phase 2 (計画中)
- [ ] ストリーミング音声生成
- [ ] 感情表現パラメータ
- [ ] 高度な音声エフェクト
- [ ] パフォーマンス最適化

## ドキュメント

- [ONNX統合ガイド](Assets/uPiper/Docs/ONNX_INTEGRATION_GUIDE.md)
- [Phase 1完了サマリー](Assets/uPiper/Docs/phase1-9-to-11-summary.md)
- [API リファレンス](Assets/uPiper/Docs/API.md) (準備中)

### モデルロードエラー
- ONNXファイルは必ず`.sentis`形式に変換してください
- モデルファイルのパスが正しいことを確認してください
- `StreamingAssets`フォルダに配置することを推奨します

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細は[LICENSE](LICENSE)ファイルを参照してください。

## 謝辞

- [piper-plus](https://github.com/ayutaz/piper-plus) - 日本語改善版Piper TTS
- [Piper TTS](https://github.com/rhasspy/piper) - オリジナルのTTSエンジン
- [OpenJTalk](http://open-jtalk.sourceforge.net/) - 日本語音素化エンジン
- Unity.InferenceEngine チーム - 高性能な推論エンジン
