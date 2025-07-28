# Flite統合実装スケジュール

## エグゼクティブサマリー

Flite (Festival Lite) の統合により、英語音素化の品質を大幅に向上させます。技術調査の結果、実装は可能であり、計画通り進められることが確認されました。

### 実装可能性評価：✅ 可能

**主な根拠：**
- ライセンス：BSD-style（商用利用可能）
- 技術的適合性：C APIでP/Invoke容易
- パフォーマンス：軽量・高速（< 1MB）
- プラットフォーム：全Unity対応プラットフォーム

## 詳細実装スケジュール

### 📅 Phase 1: Fliteビルド環境構築（3日間）

#### Day 1: 環境準備とソースコード取得
**作業内容：**
```bash
# 1. Fliteソースコード取得
git clone https://github.com/festvox/flite.git
cd flite
git checkout v2.3  # 最新安定版

# 2. ディレクトリ構造作成
NativePlugins/
└── Flite/
    ├── CMakeLists.txt
    ├── src/
    │   └── flite_unity.c  # Unity向けラッパー
    ├── build/
    └── external/
        └── flite/  # Fliteソース
```

**成果物：**
- ビルド環境の準備完了
- CMakeプロジェクト構成

#### Day 2: Windows/macOS/Linuxビルド
**作業内容：**
1. CMakeLists.txt作成
```cmake
cmake_minimum_required(VERSION 3.10)
project(flite_unity)

# Flite最小構成
set(FLITE_BUILD_SHARED ON)
set(FLITE_LEAN_AND_MEAN ON)  # 軽量化

# Unity向けビルド設定
if(WIN32)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} /MT")
elseif(APPLE)
    set(CMAKE_OSX_ARCHITECTURES "x86_64;arm64")
endif()

# ソースファイル
add_library(flite_unity SHARED
    src/flite_unity.c
    # Fliteコアファイル（最小限）
)
```

2. ビルドスクリプト作成
```bash
# build.sh (macOS/Linux)
#!/bin/bash
mkdir -p build
cd build
cmake .. -DCMAKE_BUILD_TYPE=MinSizeRel
make -j$(nproc)

# build.bat (Windows)
@echo off
mkdir build
cd build
cmake .. -G "Visual Studio 17 2022" -A x64
cmake --build . --config MinSizeRel
```

**成果物：**
- Windows: flite_unity.dll
- macOS: libflite_unity.dylib
- Linux: libflite_unity.so

#### Day 3: Androidビルド（オプション）
**作業内容：**
```bash
# Android NDKビルド
export ANDROID_NDK=/path/to/ndk
./build_android.sh

# 各ABI用ビルド
- arm64-v8a
- armeabi-v7a
- x86_64
- x86
```

**成果物：**
- Android: libflite_unity.so（各ABI）

### 📅 Phase 2: Unity統合（4日間）

#### Day 4-5: P/Invokeインターフェース実装
**作業内容：**

1. ネイティブラッパー実装（flite_unity.c）
```c
#include "flite.h"

typedef struct {
    cst_voice* voice;
    cst_features* config;
} flite_context;

// Unity向けAPI
EXPORT flite_context* flite_unity_init() {
    flite_init();
    flite_context* ctx = malloc(sizeof(flite_context));
    ctx->voice = register_cmu_us_kal16(NULL);
    return ctx;
}

EXPORT char* flite_unity_text_to_phones(
    flite_context* ctx, 
    const char* text) 
{
    // 音素化処理
    cst_utterance* utt = flite_synth_text(text, ctx->voice);
    return extract_phonemes(utt);
}

EXPORT void flite_unity_free_string(char* str) {
    if (str) free(str);
}

EXPORT void flite_unity_cleanup(flite_context* ctx) {
    if (ctx) {
        // クリーンアップ
        free(ctx);
    }
}
```

2. C#バインディング実装
```csharp
namespace uPiper.Core.Phonemizers.Native
{
    public static class FliteNative
    {
        private const string LIBRARY_NAME = 
            #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                "flite_unity"
            #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                "flite_unity"
            #elif UNITY_ANDROID
                "flite_unity"
            #else
                "flite_unity"
            #endif;

        [DllImport(LIBRARY_NAME)]
        public static extern IntPtr flite_unity_init();
        
        [DllImport(LIBRARY_NAME)]
        public static extern IntPtr flite_unity_text_to_phones(
            IntPtr context, 
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text
        );
        
        [DllImport(LIBRARY_NAME)]
        public static extern void flite_unity_free_string(IntPtr str);
        
        [DllImport(LIBRARY_NAME)]
        public static extern void flite_unity_cleanup(IntPtr context);
    }
}
```

#### Day 6-7: FlitePhonemizerBackend実装
**作業内容：**

```csharp
public class FlitePhonemizerBackend : PhonemizerBackendBase
{
    private IntPtr fliteContext;
    private CMUDictionary cmuDictionary;
    private readonly object lockObject = new object();
    
    public override string Name => "Flite";
    public override string Version => "2.3";
    public override string License => "BSD-style (CMU)";
    
    protected override async Task<bool> InitializeInternalAsync(
        PhonemizerBackendOptions options,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Flite初期化
                fliteContext = FliteNative.flite_unity_init();
                if (fliteContext == IntPtr.Zero)
                    return false;
                
                // CMU辞書も併用（高速化）
                cmuDictionary = new CMUDictionary();
                var dictTask = cmuDictionary.LoadAsync(
                    GetDictionaryPath(), 
                    cancellationToken
                );
                dictTask.Wait();
                
                Priority = 150; // 高優先度
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Flite初期化エラー: {ex.Message}");
                return false;
            }
        }, cancellationToken);
    }
    
    public override async Task<PhonemeResult> PhonemizeAsync(
        string text, 
        string language,
        PhonemeOptions options = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            var phonemes = new List<string>();
            
            // テキストを単語に分割
            var words = TokenizeText(text);
            
            foreach (var word in words)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 1. まずCMU辞書で検索（高速）
                if (cmuDictionary?.TryGetPronunciation(
                    word, out var dictPhonemes) == true)
                {
                    phonemes.AddRange(dictPhonemes);
                }
                else
                {
                    // 2. 辞書にない場合はFlite LTSを使用
                    var fliteResult = GetFlitePhonemes(word);
                    if (fliteResult != null)
                    {
                        phonemes.AddRange(fliteResult);
                    }
                }
            }
            
            stopwatch.Stop();
            
            return new PhonemeResult
            {
                OriginalText = text,
                Phonemes = phonemes.ToArray(),
                PhonemeIds = ConvertToPhonemeIds(phonemes),
                Language = language,
                Success = true,
                ProcessingTimeMs = (float)stopwatch.ElapsedMilliseconds,
                Backend = Name
            };
        }, cancellationToken);
    }
    
    private string[] GetFlitePhonemes(string word)
    {
        lock (lockObject)
        {
            if (fliteContext == IntPtr.Zero)
                return null;
            
            var resultPtr = FliteNative.flite_unity_text_to_phones(
                fliteContext, word);
            
            if (resultPtr == IntPtr.Zero)
                return null;
            
            try
            {
                var phonemeString = Marshal.PtrToStringUTF8(resultPtr);
                return phonemeString?.Split(' ') ?? Array.Empty<string>();
            }
            finally
            {
                FliteNative.flite_unity_free_string(resultPtr);
            }
        }
    }
}
```

### 📅 Phase 3: テスト・最適化（2日間）

#### Day 8: 統合テスト
**作業内容：**

1. 基本動作テスト
```csharp
[Test]
public void TestFliteInitialization()
{
    var phonemizer = new FlitePhonemizerBackend();
    var result = await phonemizer.InitializeAsync();
    Assert.IsTrue(result);
}

[Test]
public void TestCommonWords()
{
    var testWords = new[] {
        "hello", "world", "computer", "artificial", "intelligence"
    };
    
    foreach (var word in testWords)
    {
        var result = await phonemizer.PhonemizeAsync(word, "en");
        Assert.IsNotEmpty(result.Phonemes);
        Debug.Log($"{word}: {string.Join(" ", result.Phonemes)}");
    }
}
```

2. 新語・固有名詞テスト
```csharp
[Test]
public void TestNewWords()
{
    var testCases = new Dictionary<string, string[]>
    {
        ["ChatGPT"] = new[] { "CH", "AE", "T", "JH", "IY", "P", "IY", "T", "IY" },
        ["COVID"] = new[] { "K", "OW", "V", "IH", "D" },
        ["cryptocurrency"] = new[] { "K", "R", "IH", "P", "T", "OW", "K", "ER", "AH", "N", "S", "IY" },
        ["iPhone"] = new[] { "AY", "F", "OW", "N" }
    };
    
    foreach (var kvp in testCases)
    {
        var result = await phonemizer.PhonemizeAsync(kvp.Key, "en");
        CollectionAssert.AreEqual(kvp.Value, result.Phonemes);
    }
}
```

#### Day 9: パフォーマンス最適化
**作業内容：**

1. キャッシュ実装
```csharp
private readonly LRUCache<string, string[]> ltsCache = 
    new LRUCache<string, string[]>(1000);

private string[] GetFlitePhonemes(string word)
{
    // キャッシュチェック
    if (ltsCache.TryGetValue(word, out var cached))
        return cached;
    
    // Flite処理
    var result = GetFlitePhonemesInternal(word);
    
    // キャッシュ保存
    ltsCache.Add(word, result);
    return result;
}
```

2. ベンチマークテスト
```csharp
[Test]
public void BenchmarkPerformance()
{
    var text = "The quick brown fox jumps over the lazy dog";
    var iterations = 1000;
    
    var stopwatch = Stopwatch.StartNew();
    for (int i = 0; i < iterations; i++)
    {
        var result = phonemizer.Phonemize(text);
    }
    stopwatch.Stop();
    
    var avgMs = stopwatch.ElapsedMilliseconds / (float)iterations;
    Debug.Log($"平均処理時間: {avgMs:F2}ms");
    Assert.Less(avgMs, 10); // 10ms以下を目標
}
```

## リスクと対策

### 技術的リスク

| リスク | 影響度 | 対策 |
|--------|--------|------|
| iOS向けビルドの複雑さ | 中 | 初期はiOSを除外、段階的対応 |
| メモリ使用量 | 低 | Fliteは軽量（< 1MB） |
| 音素精度 | 中 | CMU辞書と併用で精度向上 |

### スケジュールリスク

| リスク | 影響度 | 対策 |
|--------|--------|------|
| ビルドエラー | 中 | 十分なバッファ期間を確保 |
| Unity統合の問題 | 低 | 既存のOpenJTalk実装を参考 |

## 成果物

### 最終成果物
1. **ネイティブライブラリ**
   - Windows: flite_unity.dll
   - macOS: libflite_unity.dylib  
   - Linux: libflite_unity.so
   - Android: libflite_unity.so（各ABI）

2. **Unityコード**
   - FliteNative.cs（P/Invokeバインディング）
   - FlitePhonemizerBackend.cs（音素化実装）
   - テストスイート

3. **ドキュメント**
   - 統合ガイド
   - APIリファレンス
   - ライセンス表示

## 次のステップ

1. プロジェクトの承認を得る
2. Fliteソースコードの取得とライセンス確認
3. Day 1の作業開始

総工数：**9日間**（バッファ込み）