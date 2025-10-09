# Phase 5: iOSサポート 詳細実装計画

> **最終更新**: 2025年10月9日
> **現在のステータス**: コード実装完了、Unity Editorでのビルド設定待ち

## 1. 概要

本ドキュメントは、uPiperのiOSプラットフォーム対応の具体的な実装手順を示します。macOS環境でのビルドを前提としています。

### 1.1 現在の進捗状況（2025-10-09時点）
- ✅ **ネイティブライブラリビルド環境構築**: 完了
- ✅ **OpenJTalk iOSライブラリビルド**: 成功（libopenjtalk_wrapper.a: 1.46MB）
- ✅ **Unity側iOS対応実装**: 完了（P/Invoke、パス解決、エラーハンドリング）
- ✅ **テストコード実装**: 完了（単体テスト、統合テスト、デモアプリ）
- ⚠️ **Unity Editorでのビルド設定**: 未実施
- ⚠️ **実機テスト**: 未実施

## 2. 開発環境要件

### 2.1 必須ソフトウェア
- **macOS**: 12.0 (Monterey) 以降
- **Xcode**: 14.0 以降
- **CMake**: 3.20 以降
- **Unity**: 2021.3 LTS 以降（iOS Build Supportインストール済み）
- **Python**: 3.8 以降（ビルドスクリプト用）

### 2.2 ディレクトリ構成
```
uPiper/
├── NativePlugins/
│   └── OpenJTalk/
│       ├── CMakeLists.txt
│       ├── build_ios.sh          # 新規作成
│       ├── ios.toolchain.cmake   # 新規ダウンロード
│       └── src/
└── Assets/
    └── uPiper/
        └── Plugins/
            └── iOS/
                ├── libopenjtalk_wrapper.a
                └── libopenjtalk_wrapper.a.meta
```

## 3. 実装ステップ

### 3.1 Step 1: iOSツールチェーンセットアップ

#### ios-cmakeツールチェーンのダウンロード
```bash
cd NativePlugins/OpenJTalk
curl -L https://raw.githubusercontent.com/leetal/ios-cmake/v4.3.0/ios.toolchain.cmake -o ios.toolchain.cmake
```

### 3.2 Step 2: ビルドスクリプト作成

#### build_ios.sh
```bash
#!/bin/bash

# iOS Build Script for OpenJTalk Unity Wrapper
# Supports: arm64 (device) and x86_64 (simulator, optional)

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== Building OpenJTalk for iOS ===${NC}"

# Configuration
CURRENT_DIR=$(pwd)
BUILD_DIR="${CURRENT_DIR}/build_ios"
INSTALL_DIR="${CURRENT_DIR}/install_ios"
OUTPUT_DIR="${CURRENT_DIR}/../../Assets/uPiper/Plugins/iOS"

# Clean previous builds
if [ -d "${BUILD_DIR}" ]; then
    echo -e "${YELLOW}Cleaning previous build...${NC}"
    rm -rf "${BUILD_DIR}"
fi

if [ -d "${INSTALL_DIR}" ]; then
    rm -rf "${INSTALL_DIR}"
fi

# Create directories
mkdir -p "${BUILD_DIR}"
mkdir -p "${INSTALL_DIR}"
mkdir -p "${OUTPUT_DIR}"

# Build function
build_ios() {
    local PLATFORM=$1
    local ARCH=$2
    local BUILD_TYPE="Release"
    
    echo -e "${GREEN}Building for ${PLATFORM} (${ARCH})...${NC}"
    
    local BUILD_PATH="${BUILD_DIR}/${PLATFORM}"
    mkdir -p "${BUILD_PATH}"
    cd "${BUILD_PATH}"
    
    # Configure with CMake
    cmake "${CURRENT_DIR}" \
        -G Xcode \
        -DCMAKE_TOOLCHAIN_FILE="${CURRENT_DIR}/ios.toolchain.cmake" \
        -DPLATFORM="${PLATFORM}" \
        -DCMAKE_BUILD_TYPE="${BUILD_TYPE}" \
        -DCMAKE_INSTALL_PREFIX="${INSTALL_DIR}/${PLATFORM}" \
        -DBUILD_SHARED_LIBS=OFF \
        -DENABLE_BITCODE=OFF \
        -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0 \
        -DCMAKE_XCODE_ATTRIBUTE_ONLY_ACTIVE_ARCH=NO \
        -DCMAKE_IOS_INSTALL_COMBINED=YES
    
    # Build
    cmake --build . --config "${BUILD_TYPE}" --target install
    
    cd "${CURRENT_DIR}"
}

# Build for device (arm64)
build_ios "OS64" "arm64"

# Optional: Build for simulator (x86_64)
# Uncomment if needed for Unity Editor iOS simulator testing
# build_ios "SIMULATOR64" "x86_64"

# Copy the library to Unity Plugins folder
echo -e "${GREEN}Copying library to Unity...${NC}"
cp "${INSTALL_DIR}/OS64/lib/libopenjtalk_wrapper.a" "${OUTPUT_DIR}/"

# Create .meta file for Unity
echo -e "${GREEN}Creating Unity meta file...${NC}"
cat > "${OUTPUT_DIR}/libopenjtalk_wrapper.a.meta" << EOF
fileFormatVersion: 2
guid: $(uuidgen | tr '[:upper:]' '[:lower:]' | tr -d '-')
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any: 
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      iPhone: iOS
    second:
      enabled: 1
      settings:
        AddToEmbeddedBinaries: false
        CPU: ARM64
        CompileFlags: 
        FrameworkDependencies: 
  userData: 
  assetBundleName: 
  assetBundleVariant: 
EOF

# Copy dictionaries to StreamingAssets
DICT_SOURCE="${CURRENT_DIR}/external/open_jtalk-1.11/mecab-naist-jdic"
DICT_DEST="${CURRENT_DIR}/../../Assets/StreamingAssets/uPiper/Dictionaries/naist_jdic"

if [ -d "${DICT_SOURCE}" ]; then
    echo -e "${GREEN}Copying dictionary files...${NC}"
    mkdir -p "${DICT_DEST}"
    cp -r "${DICT_SOURCE}/"* "${DICT_DEST}/"
else
    echo -e "${YELLOW}Warning: Dictionary files not found. Please build OpenJTalk first.${NC}"
fi

echo -e "${GREEN}=== iOS Build Complete ===${NC}"
echo -e "Library location: ${OUTPUT_DIR}/libopenjtalk_wrapper.a"
```

### 3.3 Step 3: CMakeLists.txtの修正

#### iOS対応の追加部分
```cmake
# iOS固有の設定を追加
if(IOS)
    set(PLATFORM_NAME "ios")
    set(CMAKE_POSITION_INDEPENDENT_CODE ON)
    
    # iOS用のビルド設定
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -fembed-bitcode")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fembed-bitcode")
    
    # 静的ライブラリとしてビルド
    add_library(openjtalk_wrapper STATIC ${SOURCES})
    
    # iOS固有のフレームワーク
    target_link_libraries(openjtalk_wrapper
        "-framework Foundation"
        "-framework CoreFoundation"
    )
    
    # アーキテクチャ設定
    set_target_properties(openjtalk_wrapper PROPERTIES
        XCODE_ATTRIBUTE_VALID_ARCHS "arm64"
        XCODE_ATTRIBUTE_ARCHS "arm64"
    )
endif()
```

### 3.4 Step 4: Unity C#コードの修正

#### OpenJTalkPhonemizer.csのiOS対応
```csharp
// P/Invoke宣言の修正
#if UNITY_IOS
    private const string LIBRARY_NAME = "__Internal";
#else
    private const string LIBRARY_NAME = "openjtalk_wrapper";
#endif

// iOS用の辞書パス取得
private static string GetDictionaryPath()
{
#if UNITY_IOS && !UNITY_EDITOR
    // iOSではStreamingAssetsから一時ディレクトリにコピー
    string sourcePath = Path.Combine(Application.streamingAssetsPath, "uPiper/Dictionaries/naist_jdic");
    string destPath = Path.Combine(Application.persistentDataPath, "naist_jdic");
    
    if (!Directory.Exists(destPath))
    {
        CopyDictionaryFromStreamingAssets(sourcePath, destPath);
    }
    
    return destPath;
#else
    return GetDefaultDictionaryPath();
#endif
}

#if UNITY_IOS && !UNITY_EDITOR
private static void CopyDictionaryFromStreamingAssets(string source, string dest)
{
    // StreamingAssetsからのコピー処理
    // UnityWebRequestを使用して非同期で実行
    UnityEngine.Debug.Log($"[OpenJTalk] Copying dictionary from {source} to {dest}");
    
    // 実装は非同期処理として別途実装
}
#endif
```

### 3.5 Step 5: iOS固有の最適化

#### メモリ管理クラス
```csharp
using System;
using UnityEngine;

namespace uPiper.iOS
{
    public class IOSMemoryManager : MonoBehaviour
    {
        private static IOSMemoryManager _instance;
        
        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                
#if UNITY_IOS && !UNITY_EDITOR
                // iOSメモリ警告の登録
                Application.lowMemory += OnLowMemory;
#endif
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void OnLowMemory()
        {
            Debug.LogWarning("[uPiper] iOS Low Memory Warning - Clearing caches");
            
            // キャッシュのクリア
            PiperTTS.Instance?.ClearCaches();
            
            // ガベージコレクションの強制実行
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }
        
        void OnDestroy()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Application.lowMemory -= OnLowMemory;
#endif
        }
    }
}
```

## 4. ビルド手順

### 4.1 コマンドラインからのビルド
```bash
# 1. OpenJTalkソースの取得（未実施の場合）
cd NativePlugins/OpenJTalk
./download_dependencies.sh

# 2. iOSライブラリのビルド
chmod +x build_ios.sh
./build_ios.sh

# 3. Unityプロジェクトを開く
# Unity Editorでプロジェクトを開き、iOSビルド設定を確認
```

### 4.2 Unityからのビルド

1. **Build Settings**
   - Platform: iOS
   - Architecture: ARM64
   - Target iOS Version: 11.0

2. **Player Settings**
   - Bundle Identifier: com.yousan.upiper
   - Minimum iOS Version: 11.0
   - Architecture: ARM64
   - Api Compatibility Level: .NET Standard 2.1

3. **Build**
   - Buildボタンをクリック
   - Xcodeプロジェクトが生成される

### 4.3 Xcodeでの最終ビルド

1. 生成されたXcodeプロジェクトを開く
2. Signing & Capabilitiesで署名設定
3. 実機またはシミュレータで実行

## 5. テスト手順

### 5.1 単体テスト
```csharp
// iOSテストスクリプト
[TestFixture]
public class IOSOpenJTalkTests
{
    [Test]
    public void TestLibraryLoading()
    {
#if UNITY_IOS
        var version = OpenJTalkPhonemizer.GetVersion();
        Assert.IsNotNull(version);
        Assert.That(version, Does.Contain("2.0.0"));
#endif
    }
    
    [Test]
    public void TestDictionaryPath()
    {
#if UNITY_IOS && !UNITY_EDITOR
        var dictPath = OpenJTalkPhonemizer.GetDictionaryPath();
        Assert.IsTrue(Directory.Exists(dictPath));
#endif
    }
}
```

### 5.2 統合テスト
```csharp
public class IOSIntegrationTest : MonoBehaviour
{
    async void Start()
    {
        try
        {
            // PiperTTSの初期化
            var config = PiperConfig.LoadDefault();
            var tts = new PiperTTS(config);
            
            // 音声合成テスト
            var audioClip = await tts.GenerateAudioAsync(
                "こんにちは、iOSでのテストです。"
            );
            
            // 再生
            var audioSource = GetComponent<AudioSource>();
            audioSource.clip = audioClip;
            audioSource.Play();
            
            Debug.Log("[iOS Test] Success!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[iOS Test] Failed: {e}");
        }
    }
}
```

## 6. トラブルシューティング

### 6.1 よくある問題と解決策

#### ライブラリが見つからない
```
DllNotFoundException: Unable to load DLL '__Internal'
```
**解決策**: 
- libopenjtalk_wrapper.aが`Assets/Plugins/iOS/`に存在するか確認
- .metaファイルのiOS設定が正しいか確認

#### 辞書ファイルが読み込めない
```
OpenJTalkError: Dictionary not found
```
**解決策**:
- StreamingAssetsに辞書ファイルが含まれているか確認
- iOSでのUnityWebRequestを使った読み込み実装を確認

#### メモリ不足
```
ReceivedMemoryWarning
```
**解決策**:
- IOSMemoryManagerを実装
- 辞書の部分読み込みを実装
- キャッシュサイズを制限

### 6.2 パフォーマンス最適化

1. **辞書の圧縮**
   - gzip圧縮で約30%に縮小
   - 初回起動時に展開

2. **メモリマップドファイル**
   - mmapを使用して必要な部分のみメモリにロード

3. **キャッシュ戦略**
   - LRUキャッシュで頻用音素を保持
   - メモリ圧迫時に自動クリア

## 7. GitHub Actionsでの自動ビルド（オプション）

### 7.1 ワークフロー設定
```yaml
name: iOS Library Build

on:
  push:
    paths:
      - 'NativePlugins/OpenJTalk/**'
      - '.github/workflows/ios-build.yml'

jobs:
  build-ios:
    runs-on: macos-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup Xcode
      uses: maxim-lobanov/setup-xcode@v1
      with:
        xcode-version: latest-stable
    
    - name: Install CMake
      run: brew install cmake
    
    - name: Build iOS Library
      working-directory: NativePlugins/OpenJTalk
      run: |
        chmod +x build_ios.sh
        ./build_ios.sh
    
    - name: Upload Artifacts
      uses: actions/upload-artifact@v3
      with:
        name: ios-library
        path: Assets/uPiper/Plugins/iOS/libopenjtalk_wrapper.a
```

## 8. まとめ

### 8.1 成果物チェックリスト
- [x] build_ios.sh スクリプト
- [x] iOS用CMakeLists.txt修正
- [x] libopenjtalk_wrapper.a ビルド
- [x] Unity .metaファイル設定
- [x] C# iOS対応コード
- [x] 辞書ファイルのStreamingAssets配置
- [x] テストアプリの作成（IOSTestController）
- [ ] 実機での動作確認
- [ ] パフォーマンス最適化
- [x] ドキュメント作成

### 8.1.1 実装済み項目の詳細
- **build_ios.sh**: arm64向けビルドスクリプト作成済み
- **CMakeLists.txt**: iOS条件分岐と静的ライブラリビルド設定を追加
- **libopenjtalk_wrapper.a**: 静的ライブラリビルド成功（約1.5MB）
- **Unity .metaファイル**: iOS設定（ARM64、静的リンク）完了
- **C# iOS対応**: 
  - P/Invoke設定（`__Internal`）
  - IsNativeLibraryAvailableメソッドのiOS対応
  - GetDefaultDictionaryPathメソッドのiOS StreamingAssetsパス対応

### 8.1.2 次のステップ
1. **辞書ファイルの配置と確認**
   - StreamingAssetsへの辞書ファイル配置
   - iOSでのファイルパス解決の検証
   
2. **テストアプリの作成**
   - 簡単なiOSテストシーンの作成
   - 実機でのビルドとテスト
   
3. **最適化と改善**
   - シミュレータサポートの追加（x86_64）
   - ユニバーサルバイナリの作成
   - メモリ使用量の最適化

### 8.2 予想所要時間
- 環境構築: 0.5日
- ライブラリビルド: 1日
- Unity統合: 1日
- テスト・デバッグ: 1.5日
- 最適化: 1日

**合計: 5日**

### 8.3 リスクと対策
1. **ビルド失敗**: プリビルドバイナリの提供
2. **メモリ不足**: 軽量辞書の作成
3. **パフォーマンス**: GPUアクセラレーションの活用

## 9. 実装記録

### 2025年8月1日 - Phase 1完了

### 9.1 実装内容
1. **iOS用ビルド環境の構築**
   - ios-cmake toolchainを使用したクロスコンパイル環境を構築
   - `build_ios.sh`スクリプトを作成し、arm64向けビルドを実装

2. **依存ライブラリのiOSビルド**
   - `build_dependencies_ios.sh`を作成
   - HTSEngine APIとOpenJTalkをiOS向けに最適化してビルド
   - 静的ライブラリとしてビルド（`.a`ファイル）

3. **Unity側の対応**
   - OpenJTalkPhonemizer.csを更新
   - iOS向けP/Invoke設定（`__Internal`）を追加
   - ライブラリ存在チェックとパス解決の最適化

### 9.2 技術的課題と解決策
1. **Xcodeプラグインエラー**
   - 問題: DVTDownloads.frameworkが見つからない
   - 解決: `xcodebuild -runFirstLaunch`で初期設定を実行

2. **CMake設定の調整**
   - 問題: 動的ライブラリが生成される
   - 解決: `IOS`フラグを追加し、静的ライブラリビルドを強制

3. **ファイル拡張子の問題**
   - 問題: `.dylib`拡張子で静的ライブラリが生成される
   - 解決: ビルドスクリプトでリネーム処理を追加

### 9.3 成果物
- `libopenjtalk_wrapper.a`: iOS向け静的ライブラリ（約1.5MB）
- 対応する`.meta`ファイル: Unity向けプラグイン設定
- 更新されたビルドスクリプト群

### 9.4 今後の作業
1. **品質向上**
   - エラーハンドリングの強化
   - ログ出力の改善
   - ビルドスクリプトの堅牢性向上

2. **機能拡張**
   - iOSシミュレータサポート（x86_64）
   - ユニバーサルバイナリの作成
   - Bitcodeサポートの検討

3. **テストとドキュメント**
   - 実機でのテスト
   - パフォーマンス測定
   - 使用方法ドキュメントの作成

### 2025年8月1日 - Phase 2完了

### 9.5 Phase 2実装内容
1. **IOSPathResolver実装**
   - iOS固有のStreamingAssetsパス解決
   - Application.dataPath + "/Raw"の使用
   - ファイル存在確認とサイズ計算機能
   - エラーハンドリングとログ機能

2. **辞書ファイル配置の確認**
   - フル辞書（98MB）のStreamingAssets配置完了
   - 全8ファイルの存在確認
   - 他プラットフォームと同一の辞書品質

3. **テスト環境の構築**
   - IOSTestController実装
   - 辞書アクセステスト
   - Phonemizerテスト
   - TTSパイプライン全体のテスト
   - システム情報表示機能

4. **包括的なテストスイート**
   - IOSPathResolverTest追加
   - エラーケースのテスト
   - パフォーマンス監視

### 9.6 現在の状況

**実機テスト準備完了** - すべての必要な実装が完了し、実機でのテストが可能な状態です。

#### 完了項目
- ✅ ネイティブライブラリのビルドと配置
- ✅ Unity側のiOS対応コード
- ✅ ファイルパス解決の実装
- ✅ 辞書ファイルの配置（フル辞書）
- ✅ テストコードとサンプルの作成
- ✅ ドキュメントの整備

#### 残作業（Unity Editor作業）
- Unity PlayerSettingsのiOS設定
- Xcodeプロジェクトのビルド
- 実機へのデプロイとテスト
- パフォーマンスプロファイリング

### 2025年10月9日 - 実装状況確認

### 9.7 最新の実装状況

#### 確認済み成果物
1. **ネイティブライブラリ**
   - `libopenjtalk_wrapper.a` (1.46MB) - 正常にビルド済み
   - `build_ios.sh` (3881バイト) - 実装済み
   - Assets/uPiper/Plugins/iOS/に配置済み

2. **Unity統合コード**
   - `IOSPathResolver.cs` (202行) - 完全実装
   - `OpenJTalkPhonemizer.cs` - iOS対応済み（`__Internal`リンク）
   - `IOSTestController.cs` (305行) - デモアプリ実装済み

3. **テストコード**
   - `OpenJTalkPhonemizerIOSTest.cs` (197行)
   - `IOSPathResolverTest.cs`
   - `IOSIntegrationTest.cs`
   - `IOSBuildValidationTest.cs`

4. **主要な実装内容**
   - P/Invoke設定（`UNITY_IOS && !UNITY_EDITOR`条件）
   - StreamingAssetsパス解決（`Application.dataPath + "/Raw"`）
   - エラーハンドリングとログ機能
   - メモリ最適化対応
   - スレッドセーフティ確保

### 9.8 次のステップ（Unity Editor作業）

1. **Unity PlayerSettings設定**
   ```
   - Platform: iOS
   - Minimum iOS Version: 11.0
   - Architecture: ARM64
   - API Compatibility Level: .NET Standard 2.1
   - Graphics APIs: Metal
   ```

2. **ビルドとテスト手順**
   - File > Build Settings > iOS
   - Player Settingsの設定
   - Buildボタンでcodeプロジェクト生成
   - Xcodeで開いて署名設定
   - 実機またはシミュレータでテスト

3. **パフォーマンス検証項目**
   - 起動時間
   - 音声生成速度
   - メモリ使用量
   - バッテリー消費

### 9.9 リスクと対策

1. **技術的リスク**: すべて解決済み
   - ビルドスクリプト: 実装済み
   - 静的ライブラリ: 正常生成
   - P/Invoke設定: 対応済み

2. **残存リスク**: 実機テストで判明する可能性
   - パフォーマンス問題
   - メモリ制限
   - App Store審査要件