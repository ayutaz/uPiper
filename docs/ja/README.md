# uPiper ドキュメント

[🇯🇵 **日本語**](../ja/README.md) | [🇬🇧 English](../en/README.md)

## 📋 概要

uPiperは、Unity環境でPiper TTSエンジンを使用するための高品質音声合成プラグインです。ニューラルネットワークベース（VITS）の音声合成技術により、自然で表現力豊かな音声を生成します。

## 🚀 主な特徴

- **高品質音声合成**: Piper TTSベースの自然な音声生成
- **多言語対応**: 日本語、英語、中国語、韓国語など
- **高速推論**: Unity AI Inference Engine（旧Sentis）による効率的な処理
- **マルチプラットフォーム**: Windows、macOS、Linux、Android対応（iOS対応準備中）
- **OpenJTalk統合**: 高精度な日本語音素化

## 📚 ドキュメント構成

### アーキテクチャ
- [ARCHITECTURE.md](./ARCHITECTURE.md) - システム設計と全体構成

### ガイド

#### 📦 セットアップ
- [追加言語サポート](./guides/setup/additional-language-support.md)
- [CMU辞書セットアップ](./guides/setup/cmu-dictionary-setup.md)
- [Fliteビルドガイド](./guides/setup/flite-build-guide.md)

#### 🎮 使用方法
- [音素化ガイド](./guides/usage/phonemization-guide.md)
- [テスト実行ガイド](./guides/usage/test-execution-guide.md)

#### 🔧 実装
- [Android実装ガイド](./guides/implementation/android/)
  - [実装ガイド](./guides/implementation/android/implementation-guide.md)
  - [ビルド設定](./guides/implementation/android/build-settings.md)
  - [パフォーマンス最適化](./guides/implementation/android/performance-optimization.md)
  - [技術レポート](./guides/implementation/android/technical-report.md)
- [音素化システム](./guides/implementation/phonemization-system/)
  - [実装サマリー](./guides/implementation/phonemization-system/implementation-summary.md)
  - [技術仕様](./guides/implementation/phonemization-system/technical-specification.md)
  - [ライセンス評価](./guides/implementation/phonemization-system/license-evaluation-report.md)
- [アジア言語サポート](./guides/implementation/asian-language-support.md)

#### ⚙️ 技術詳細
- [技術ドキュメント概要](./guides/technical/README.md)
- [IL2CPP互換性](./guides/technical/il2cpp-compatibility.md)
- [IL2CPPガイド](./guides/technical/il2cpp.md)
- [GPU推論](./guides/technical/gpu-inference.md)

#### 🔄 CI/CD
- [CI/CD概要](./guides/ci-cd/README.md)
- [IL2CPPソリューション](./guides/ci-cd/il2cpp-solutions.md)

### Phase 5: iOS対応（進行中）
- [iOS技術調査](./phase5-ios/phase5-ios-technical-research.md)
- [iOS実装計画](./phase5-ios/phase5-ios-implementation-plan.md)
- [iOS詳細実装計画](./phase5-ios/phase5-ios-detailed-implementation-plan.md)

## 🎯 クイックスタート

### 1. インストール
1. Unity Package Managerを開く
2. 「Add package from git URL」を選択
3. 以下のURLを入力:
   ```
   https://github.com/ayutaz/uPiper.git
   ```

### 2. 基本的な使い方

```csharp
using uPiper;

// 初期化
var config = PiperConfig.LoadDefault();
var tts = new PiperTTS(config);

// 音声生成
var audioClip = await tts.GenerateAudioAsync("こんにちは、世界！");

// 再生
audioSource.clip = audioClip;
audioSource.Play();
```

### 3. 日本語音声モデルの設定
1. [Piper公式サイト](https://github.com/rhasspy/piper)から日本語モデルをダウンロード
2. `Assets/StreamingAssets/uPiper/Models/`に配置
3. `PiperConfig`でモデルパスを指定

## 🛠️ 開発者向け情報

### 環境構築
1. Unity 6000.0.35f1以降
2. Unity AI Inference Engine 2.2.x
3. Visual Studio 2022 / Rider

### ビルド要件
- **Windows**: Visual Studio 2019以降
- **macOS**: Xcode 14以降
- **Linux**: GCC 9以降
- **Android**: NDK r21以降

### テスト実行
```bash
# Unity Test Runnerで実行
Window > General > Test Runner

# コマンドラインから
Unity.exe -runTests -projectPath . -testResults results.xml
```

## 📝 ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細は[LICENSE](../../../LICENSE)ファイルを参照してください。

## 🤝 貢献

貢献を歓迎します！以下の方法で参加できます：

1. バグ報告や機能提案は[Issues](https://github.com/ayutaz/uPiper/issues)へ
2. プルリクエストの作成
3. ドキュメントの改善
4. サンプルコードの追加

## 📞 サポート

- **Issues**: [GitHub Issues](https://github.com/ayutaz/uPiper/issues)
- **Discussions**: [GitHub Discussions](https://github.com/ayutaz/uPiper/discussions)
- **Wiki**: [プロジェクトWiki](https://github.com/ayutaz/uPiper/wiki)

## 🔗 関連リンク

- [Piper TTS](https://github.com/rhasspy/piper) - オリジナルのPiper TTSプロジェクト
- [Unity AI Inference Engine](https://docs.unity3d.com/Packages/com.unity.sentis@latest) - Unity公式ドキュメント
- [OpenJTalk](http://open-jtalk.sourceforge.net/) - 日本語音素化エンジン