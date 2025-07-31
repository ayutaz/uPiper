# Fliteネイティブライブラリビルドガイド

## 概要

Flite（Festival-Lite）は、CMUで開発された小型で高速なランタイム音声合成エンジンです。BSDスタイルライセンスでライセンスされており、商用利用に適しています。

## なぜFliteか？

- **ライセンス**: BSDスタイル（商用利用可能）
- **サイズ**: 小さなフットプリント（プラットフォームごとに約2-5MB）
- **速度**: 高速な合成
- **品質**: サイズに対して良好な品質
- **言語**: 主に英語だが拡張可能

## プラットフォーム別ビルド手順

### 前提条件

```bash
# 必要な共通ツール
git clone https://github.com/festvox/flite.git
cd flite
```

### Windowsビルド

```batch
# Visual Studio 2019以降を使用

# 1. 前提条件のインストール
# - C++開発ツールを含むVisual Studio
# - CMake（より簡単なビルドのためオプション）

# 2. 提供されているWindows makefileを使用してビルド
cd flite/windows
nmake /f Makefile.msvc

# 3. 出力場所
# - flite.dllはwindows/build/に作成される
# - 以下にコピー: Assets/uPiper/Plugins/Windows/x86_64/flite.dll
```

### macOSビルド

```bash
# ユニバーサルバイナリ（Intel + Apple Silicon）

# 1. ユニバーサルビルド用に設定
./configure --enable-shared \
    CFLAGS="-arch x86_64 -arch arm64" \
    LDFLAGS="-arch x86_64 -arch arm64"

# 2. ビルド
make

# 3. ユニバーサルライブラリを作成
lipo -create \
    build/x86_64/libflite.dylib \
    build/arm64/libflite.dylib \
    -output libflite_unity.dylib

# 4. Unityにコピー
cp libflite_unity.dylib Assets/uPiper/Plugins/macOS/
```

### Linuxビルド

```bash
# 標準的なLinuxビルド

# 1. 設定
./configure --enable-shared --prefix=/usr/local

# 2. ビルド
make -j$(nproc)

# 3. 出力
# libflite.soはbuild/lib/に作成される
cp build/lib/libflite.so Assets/uPiper/Plugins/Linux/x86_64/
```

### Androidビルド

```bash
# Android NDKを使用

# 1. 環境設定
export ANDROID_NDK_HOME=/path/to/android-ndk
export PATH=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin:$PATH

# 2. 各アーキテクチャ用にビルド
# ARM64-v8a
./configure --host=aarch64-linux-android \
    --enable-shared \
    CC=aarch64-linux-android21-clang \
    CXX=aarch64-linux-android21-clang++

make clean && make

# 出力をコピー
cp libflite.so Assets/uPiper/Plugins/Android/ARM64/

# 3. ARMv7用にも繰り返す
./configure --host=armv7a-linux-androideabi \
    --enable-shared \
    CC=armv7a-linux-androideabi21-clang

make clean && make
cp libflite.so Assets/uPiper/Plugins/Android/ARMv7/
```

### iOSビルド

```bash
# iOS用ビルド（macOSが必要）

# 1. デバイス用ビルド
./configure --host=arm-apple-darwin \
    --enable-static \
    CC="xcrun -sdk iphoneos clang -arch arm64" \
    CFLAGS="-isysroot $(xcrun -sdk iphoneos --show-sdk-path)"

make clean && make

# 2. シミュレータ用ビルド
./configure --host=x86_64-apple-darwin \
    --enable-static \
    CC="xcrun -sdk iphonesimulator clang -arch x86_64" \
    CFLAGS="-isysroot $(xcrun -sdk iphonesimulator --show-sdk-path)"

make clean && make

# 3. ユニバーサルライブラリを作成
lipo -create libflite_arm64.a libflite_x86_64.a -output libflite_unity.a
```

## Unity統合

### プラグイン設定

ライブラリをコピーした後、Unityで設定：

1. **Windows (flite.dll)**:
   - プラットフォーム: Windows
   - CPU: x86_64
   - OS: Windows

2. **macOS (libflite_unity.dylib)**:
   - プラットフォーム: macOS
   - CPU: AnyCPU

3. **Linux (libflite.so)**:
   - プラットフォーム: Linux
   - CPU: x86_64

4. **Android (libflite.so)**:
   - プラットフォーム: Android
   - CPU: ARM64またはARMv7

### C#ラッパー実装

```csharp
using System;
using System.Runtime.InteropServices;

namespace uPiper.Phonemizers.Backend.Flite
{
    public static class FliteNative
    {
        #if UNITY_IOS
            const string FLITE_LIB = "__Internal";
        #else
            const string FLITE_LIB = "flite";
        #endif

        [DllImport(FLITE_LIB)]
        public static extern IntPtr flite_init();

        [DllImport(FLITE_LIB)]
        public static extern void flite_cleanup(IntPtr flite);

        [DllImport(FLITE_LIB)]
        public static extern IntPtr flite_text_to_phones(
            IntPtr flite, 
            string text, 
            string language);

        [DllImport(FLITE_LIB)]
        public static extern void flite_free_phones(IntPtr phones);
    }
}
```

## 最小限のFlite実装

音素化のみ（フルTTSではない）が必要な場合、最小版をビルドできます：

```c
// minimal_flite.c - 音素化のみ、音声合成なし
#include "flite.h"

typedef struct {
    cst_voice *voice;
    cst_lexicon *lex;
    cst_lts_rules *lts;
} flite_phonemizer;

flite_phonemizer* flite_phonemizer_init() {
    flite_init();
    flite_phonemizer *fp = malloc(sizeof(flite_phonemizer));
    fp->lex = cmu_lex_init();
    fp->lts = cmu_lts_rules_init();
    return fp;
}

char* flite_phonemizer_process(flite_phonemizer *fp, const char *text) {
    // 辞書とLTSルールを使用してテキストを音素に変換
    // ARPABET文字列を返す
}
```

## サイズ最適化

ライブラリサイズを削減するには：

1. **未使用の音声を削除**: 音素化コードのみを含める
2. **シンボルをストリップ**: `strip -S libflite.so`
3. **機能を無効化**: 最小限のオプションで設定
   ```bash
   ./configure --disable-audio --disable-lang-usenglish \
               --disable-lang-cmulex --enable-lang-cmu_us_kal
   ```

## ビルドのテスト

```csharp
[Test]
public void FliteNative_ShouldLoadSuccessfully()
{
    IntPtr flite = IntPtr.Zero;
    
    try
    {
        flite = FliteNative.flite_init();
        Assert.AreNotEqual(IntPtr.Zero, flite, "Fliteは初期化されるべき");
        
        string testPhones = Marshal.PtrToStringAnsi(
            FliteNative.flite_text_to_phones(flite, "hello world", "en-US")
        );
        
        Assert.IsNotEmpty(testPhones, "音素を返すべき");
    }
    finally
    {
        if (flite != IntPtr.Zero)
            FliteNative.flite_cleanup(flite);
    }
}
```

## 代替案: Flite-Unityパッケージ

事前ビルド済みバイナリを含むUnityパッケージの作成を検討：

```json
{
  "name": "com.upiper.flite",
  "version": "1.0.0",
  "displayName": "Flite for uPiper",
  "description": "全プラットフォーム用の事前ビルド済みFliteライブラリ",
  "unity": "2021.3",
  "dependencies": {},
  "author": {
    "name": "uPiperチーム"
  }
}
```

これによりユーザーはビルドせずに単にパッケージをインポートできます。