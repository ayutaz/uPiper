# Phase 3: eSpeak-NG統合 詳細実装計画

## エグゼクティブサマリー

Phase 3では、uPiperにeSpeak-NGを統合し、英語および多言語の音素化機能を実装します。piper-plusの実装を参考にしつつ、Unity環境に最適化された設計を行います。

## 1. 実装スコープと目標

### 1.1 主要目標
- 英語テキストの高品質音素化（精度95%以上）
- 10言語以上の多言語サポート
- 既存のIPhonemizer インターフェースとの完全互換性
- クロスプラットフォーム対応（Windows/macOS/Linux/Android）

### 1.2 成果物
- ESpeakPhonemizerクラス（C#）
- espeak_wrapper ネイティブライブラリ
- 言語検出・選択システム
- 包括的テストスイート
- 技術ドキュメント

## 2. 技術アーキテクチャ

### 2.1 システム構成図

```
┌─────────────────────────────────────────────┐
│             Unity Application               │
├─────────────────────────────────────────────┤
│         PiperTTS (Core API)                 │
├─────────────────────────────────────────────┤
│    PhonemizerFactory (言語選択)             │
├────────────────┬────────────────────────────┤
│ OpenJTalkPhonemizer │   ESpeakPhonemizer    │
│    (日本語)         │   (英語・多言語)       │
├────────────────┼────────────────────────────┤
│ openjtalk_unity.dll │ espeak_wrapper.dll    │
└────────────────┴────────────────────────────┘
```

### 2.2 データフロー

```
入力テキスト → 言語検出 → 音素化エンジン選択
                              ↓
                        ESpeakPhonemizer
                              ↓
                     espeak_wrapper (Native)
                              ↓
                        eSpeak-NG API
                              ↓
                      IPA音素列 + 韻律情報
                              ↓
                    Piper音素ID変換
                              ↓
                        音声合成へ
```

## 3. 実装タスク詳細（10人日）

### Day 1-2: ビルド環境構築とプロトタイプ

#### タスク1.1: eSpeak-NGソース取得とビルド検証
```bash
# ディレクトリ構造
NativePlugins/ESpeakNG/
├── external/
│   └── espeak-ng/     # git submodule
├── src/
│   └── espeak_wrapper.c
├── CMakeLists.txt
└── build.sh/build.bat
```

#### タスク1.2: 最小限のラッパー実装
```c
// espeak_wrapper.c - 基本実装
int espeak_wrapper_initialize(const char* data_path) {
    return espeak_Initialize(AUDIO_OUTPUT_SYNCHRONOUS, 
                           0, data_path, 0);
}

ESpeakResult* espeak_wrapper_phonemize(const char* text, 
                                      const char* language) {
    // 実装詳細
}
```

### Day 3-4: C#バインディングとインターフェース実装

#### タスク2.1: P/Invokeラッパー
```csharp
// ESpeakNative.cs
public static class ESpeakNative
{
    [DllImport("espeak_wrapper")]
    private static extern int espeak_wrapper_initialize(
        string dataPath);
    
    [DllImport("espeak_wrapper")]
    private static extern IntPtr espeak_wrapper_phonemize(
        string text, string language, int voiceVariant);
}
```

#### タスク2.2: ESpeakPhonemizerクラス実装
```csharp
public class ESpeakPhonemizer : BasePhonemizer
{
    // IPA to Piper phoneme mapping
    private readonly Dictionary<string, string> ipaToPiper = new()
    {
        // 英語音素マッピング
        ["aɪ"] = "ai", ["aʊ"] = "au", ["eɪ"] = "ei",
        ["oʊ"] = "ou", ["ɔɪ"] = "oi",
        // 子音マッピング
        ["θ"] = "th", ["ð"] = "dh", ["ʃ"] = "sh",
        ["ʒ"] = "zh", ["tʃ"] = "ch", ["dʒ"] = "jh"
    };
}
```

### Day 5-6: Android対応とマルチプラットフォーム

#### タスク3.1: Androidビルドスクリプト
```bash
#!/bin/bash
# build_android.sh
ABIS="arm64-v8a armeabi-v7a x86 x86_64"
for ABI in $ABIS; do
    cmake -DANDROID_ABI=$ABI \
          -DANDROID_NDK=$ANDROID_NDK_HOME \
          -DCMAKE_TOOLCHAIN_FILE=$NDK/build/cmake/android.toolchain.cmake
    make
done
```

#### タスク3.2: データファイル管理
```csharp
public class ESpeakDataManager
{
    // StreamingAssetsからの展開
    public async Task<string> EnsureDataFiles()
    {
        var targetPath = GetPlatformDataPath();
        if (!Directory.Exists(targetPath))
        {
            await ExtractDataFiles(targetPath);
        }
        return targetPath;
    }
}
```

### Day 7-8: 言語検出と統合

#### タスク4.1: 言語検出システム
```csharp
public class LanguageDetector
{
    // Unicode範囲による言語推定
    public string DetectLanguage(string text)
    {
        var scripts = GetUnicodeScripts(text);
        return scripts switch
        {
            _ when scripts.Contains(UnicodeScript.Han) => "zh",
            _ when scripts.Contains(UnicodeScript.Arabic) => "ar",
            _ when scripts.Contains(UnicodeScript.Cyrillic) => "ru",
            _ when scripts.Contains(UnicodeScript.Devanagari) => "hi",
            _ => "en" // デフォルト
        };
    }
}
```

#### タスク4.2: PhonemizerFactory拡張
```csharp
public class PhonemizerFactory
{
    private readonly Dictionary<string, IPhonemizer> phonemizers;
    
    public IPhonemizer GetPhonemizer(string language)
    {
        return language switch
        {
            "ja" => GetOrCreate<OpenJTalkPhonemizer>(),
            "en" or "es" or "fr" or "de" => GetOrCreate<ESpeakPhonemizer>(),
            _ => GetOrCreate<DefaultPhonemizer>()
        };
    }
}
```

### Day 9-10: テストと品質保証

#### タスク5.1: 単体テストスイート
```csharp
[TestFixture]
public class ESpeakPhonemizerTests
{
    [Test]
    [TestCase("Hello world", "en", "hɛloʊ wɜrld")]
    [TestCase("Bonjour", "fr", "bɔ̃ʒuʁ")]
    public void TestPhonemization(string text, string lang, string expected)
    {
        // テスト実装
    }
}
```

#### タスク5.2: パフォーマンステスト
```csharp
[Test]
public void BenchmarkPhonemization()
{
    var texts = LoadTestTexts();
    var stopwatch = Stopwatch.StartNew();
    
    foreach (var text in texts)
    {
        phonemizer.Phonemize(text, "en");
    }
    
    Assert.Less(stopwatch.ElapsedMilliseconds / texts.Count, 50);
}
```

## 4. データファイル管理戦略

### 4.1 選択的言語サポート
```json
// espeak_languages.json
{
  "core": ["en", "es", "fr", "de"],  // 必須言語（5MB）
  "extended": ["it", "pt", "ru", "pl"], // 拡張言語（+3MB）
  "asian": ["zh", "hi", "ar"],       // アジア言語（+5MB）
}
```

### 4.2 ダウンロード可能コンテンツ
```csharp
public class ESpeakLanguagePack
{
    public async Task DownloadLanguage(string languageCode)
    {
        var url = $"{CDN_BASE}/espeak-data-{languageCode}.zip";
        await DownloadAndExtract(url);
    }
}
```

## 5. 品質保証計画

### 5.1 テストカバレッジ目標
- 単体テスト: 90%以上
- 統合テスト: 主要10言語
- パフォーマンステスト: 全プラットフォーム

### 5.2 品質メトリクス
| メトリクス | 目標値 | 測定方法 |
|-----------|--------|----------|
| 音素化精度 | 95%+ | 参照データセットとの比較 |
| 処理速度 | <50ms/100文字 | ベンチマークテスト |
| メモリ使用量 | <20MB | Unity Profiler |
| 初期化時間 | <200ms | 起動時測定 |

## 6. リスク管理

### 6.1 技術的リスクと対策

| リスク | 対策 |
|--------|------|
| GPL v3ライセンス | 動的リンク、別プロセス化オプション |
| データファイルサイズ | 段階的ダウンロード、圧縮 |
| プラットフォーム差異 | 十分なCI/CDテスト |
| 音素マッピング精度 | 専門家レビュー、A/Bテスト |

### 6.2 スケジュールリスク
- バッファ: 各タスクに20%の余裕を確保
- 優先順位: 英語サポートを最優先
- 段階的リリース: MVP → 拡張機能

## 7. 成果物とドキュメント

### 7.1 コード成果物
- [ ] ESpeakPhonemizer.cs
- [ ] ESpeakNative.cs
- [ ] espeak_wrapper.{c,h}
- [ ] CMakeLists.txt
- [ ] ビルドスクリプト（全プラットフォーム）

### 7.2 ドキュメント
- [ ] API リファレンス
- [ ] 統合ガイド
- [ ] 言語サポート一覧
- [ ] パフォーマンスガイド

### 7.3 テスト成果物
- [ ] 単体テストスイート
- [ ] 統合テストスイート
- [ ] パフォーマンステスト
- [ ] 言語別テストデータ

## 8. 次のフェーズへの準備

### Phase 4への橋渡し
- 言語検出システムの拡張基盤
- 音素化エンジンプラグインアーキテクチャ
- 多言語テストフレームワーク

### 将来の拡張性
- カスタム言語ルールのサポート
- ユーザー辞書機能
- 音素編集API

## 9. まとめ

Phase 3の実装により、uPiperは真の多言語TTSプラットフォームへと進化します。段階的な実装アプローチにより、リスクを最小化しながら高品質な成果を確実に提供します。