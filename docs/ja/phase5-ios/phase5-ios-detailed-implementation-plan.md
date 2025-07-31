# Phase 5: iOSサポート 詳細実装計画

## 1. 概要

本ドキュメントは、uPiperのiOSプラットフォーム対応の具体的な実装手順を示します。macOS環境でのビルドを前提としています。

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
curl -L https://raw.githubusercontent.com/leetal/ios-cmake/master/ios.toolchain.cmake -o ios.toolchain.cmake
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
- [ ] build_ios.sh スクリプト
- [ ] iOS用CMakeLists.txt修正
- [ ] libopenjtalk_wrapper.a ビルド
- [ ] Unity .metaファイル設定
- [ ] C# iOS対応コード
- [ ] 辞書ファイルのStreamingAssets配置
- [ ] テストアプリでの動作確認
- [ ] パフォーマンス最適化
- [ ] ドキュメント作成

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