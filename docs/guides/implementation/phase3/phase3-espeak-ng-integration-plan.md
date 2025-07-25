# Phase 3: eSpeak-NG統合実装計画

## 概要

Phase 3では、eSpeak-NGを統合して英語音素化の品質向上と多言語音素化基盤の構築を行います。

## 目標（10人日）

1. **eSpeak-NGのクロスプラットフォームビルド（4人日）**
   - Windows/macOS/Linux/Android対応
   - Unity Native Plugin形式での配布

2. **英語音素化の品質向上（3人日）**
   - ESpeakPhonemizerクラスの実装
   - IPhonemizer互換インターフェース
   - 英語専用の音素マッピング

3. **多言語音素化基盤の構築（3人日）**
   - 言語自動検出システム
   - 音素化エンジン選択ロジック
   - 統合テストスイート

## 技術詳細

### 1. eSpeak-NGライブラリの統合

#### ディレクトリ構造
```
NativePlugins/
├── OpenJTalk/          # 既存（日本語用）
└── ESpeakNG/           # 新規（英語・多言語用）
    ├── CMakeLists.txt
    ├── src/
    │   └── espeak_wrapper.c
    ├── include/
    │   └── espeak_wrapper.h
    ├── build.bat
    ├── build.sh
    └── external/
        └── espeak-ng/  # サブモジュール
```

#### ネイティブラッパー仕様
```c
// espeak_wrapper.h
typedef struct {
    char* phonemes;
    int* phoneme_ids;
    int phoneme_count;
    char* error_message;
} ESpeakResult;

// 初期化
int espeak_wrapper_initialize(const char* data_path);

// 音素化
ESpeakResult* espeak_wrapper_phonemize(
    const char* text,
    const char* language,
    int voice_variant
);

// クリーンアップ
void espeak_wrapper_free_result(ESpeakResult* result);
void espeak_wrapper_terminate();
```

### 2. C#側の実装

#### ESpeakPhonemizer クラス
```csharp
namespace uPiper.Core.Phonemizers
{
    public class ESpeakPhonemizer : BasePhonemizer
    {
        // eSpeak-NG対応言語
        private static readonly string[] SupportedLanguages = {
            "en", "es", "fr", "de", "it", "pt", "ru", "zh", "ar", "hi"
        };

        // 音素マッピングテーブル
        private readonly Dictionary<string, PhonemeMapping> languageMappings;

        protected override PhonemeResult PhonemizeInternal(
            string text, 
            string language)
        {
            // eSpeak-NGネイティブ呼び出し
            var result = ESpeakNative.Phonemize(text, language);
            
            // Piper形式への変換
            return ConvertToPiperFormat(result, language);
        }
    }
}
```

### 3. 多言語対応アーキテクチャ

#### PhonemizerFactory
```csharp
public static class PhonemizerFactory
{
    public static IPhonemizer CreatePhonemizerForLanguage(string language)
    {
        return language switch
        {
            "ja" => new OpenJTalkPhonemizer(),
            "en" or "es" or "fr" or "de" => new ESpeakPhonemizer(),
            _ => new DefaultPhonemizer()
        };
    }
}
```

#### 言語検出システム
```csharp
public class LanguageDetector
{
    // 文字範囲による基本的な言語検出
    public string DetectLanguage(string text)
    {
        if (ContainsJapanese(text)) return "ja";
        if (ContainsChinese(text)) return "zh";
        if (ContainsKorean(text)) return "ko";
        if (ContainsArabic(text)) return "ar";
        
        // デフォルトは英語
        return "en";
    }
}
```

## 実装ステップ

### Step 1: eSpeak-NGビルド環境構築（1日目）
- [ ] eSpeak-NGソースコードの取得
- [ ] CMakeLists.txt作成
- [ ] Windows用ビルドスクリプト
- [ ] macOS/Linux用ビルドスクリプト

### Step 2: Androidビルド対応（2日目）
- [ ] Android NDK対応CMake設定
- [ ] Docker環境でのビルド
- [ ] 全ABIサポート

### Step 3: ネイティブラッパー実装（3-4日目）
- [ ] C言語ラッパー実装
- [ ] メモリ管理とエラーハンドリング
- [ ] 音素データ構造の設計

### Step 4: C#バインディング（5-6日目）
- [ ] P/Invokeインターフェース
- [ ] ESpeakPhonemizerクラス
- [ ] 音素マッピングロジック

### Step 5: 統合とテスト（7-8日目）
- [ ] PhonemizerFactory実装
- [ ] 言語検出システム
- [ ] 統合テストスイート

### Step 6: 最適化と品質保証（9-10日目）
- [ ] パフォーマンステスト
- [ ] メモリリーク検証
- [ ] ドキュメント作成

## 成功基準

1. **ビルド成功**
   - 全プラットフォームでeSpeak-NGライブラリがビルド可能
   - Unity Pluginとして正常に動作

2. **音素化品質**
   - 英語テキストの音素化精度95%以上
   - 基本的な多言語テキストの処理が可能

3. **パフォーマンス**
   - 100文字の英語テキスト処理が50ms以内
   - メモリ使用量が既存実装の2倍以内

4. **統合性**
   - 既存のIPhonemizer インターフェースと完全互換
   - OpenJTalkとの共存が可能

## リスクと対策

### リスク1: ライブラリサイズ
- **問題**: eSpeak-NGデータファイルが大きい（約10MB）
- **対策**: 必要な言語データのみを含める選択的ビルド

### リスク2: ライセンス
- **問題**: eSpeak-NGはGPL v3ライセンス
- **対策**: 動的リンクによる分離、またはeSpeak-NG代替の検討

### リスク3: 音素形式の違い
- **問題**: eSpeak-NGとPiperの音素体系が異なる
- **対策**: 詳細なマッピングテーブルの作成と検証

## 次のステップ

1. eSpeak-NGのソースコード調査とビルド可能性の検証
2. ライセンス問題の最終確認
3. プロトタイプ実装による技術検証