# IL2CPP Build Settings Guide

## 概要

このドキュメントは、uPiperプロジェクトでIL2CPPビルドを行うための設定ガイドです。

## 自動設定

Unityエディタのメニューから以下を実行：
1. `uPiper > Configure IL2CPP Settings` - IL2CPP設定を自動適用
2. `uPiper > Verify IL2CPP Configuration` - 設定の確認

## 手動設定

### 1. Player Settings

#### Build Settings
- **Scripting Backend**: IL2CPP
- **Api Compatibility Level**: .NET Standard 2.0
- **C++ Compiler Configuration**: Release

#### Optimization
- **Managed Stripping Level**: Low（初期テスト用）
- **Engine Code Stripping**: Disabled（初期テスト用）
- **Script Call Optimization**: Fast but no exceptions

### 2. プラットフォーム別設定

#### Windows
```
Architecture: x86_64
Target Platform: Windows 10
```

#### macOS
```
Architecture: Universal (Intel + Apple Silicon)
Minimum macOS Version: 10.13
```

#### Linux
```
Architecture: x86_64
```

#### Android
```
Target Architectures: ARM64 + ARMv7
Minimum API Level: 21 (Android 5.0)
Target API Level: Latest
```

#### iOS
```
Architecture: Universal
Minimum iOS Version: 11.0
Enable Bitcode: No
```

#### WebGL
```
Linker Target: WebAssembly
Memory Size: 512 MB
Enable Exceptions: Explicitly Thrown Exceptions Only
```

## link.xml の役割

`Assets/uPiper/link.xml`は、IL2CPPビルド時のコードストリッピングを制御します：

```xml
<linker>
    <!-- Unity.AI.InferenceEngine の保持 -->
    <assembly fullname="Unity.AI.InferenceEngine" preserve="all"/>
    
    <!-- uPiper ランタイムの保持 -->
    <assembly fullname="uPiper.Runtime" preserve="all"/>
    
    <!-- P/Invoke構造体の保持 -->
    <assembly fullname="uPiper.Runtime">
        <type fullname="uPiper.Core.Phonemizers.Implementations.OpenJTalkPhonemizer/NativePhonemeResult" preserve="all"/>
    </assembly>
</linker>
```

## ネイティブライブラリの配置

### Windows (x64)
```
Assets/uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll
```

### macOS
```
Assets/uPiper/Plugins/macOS/openjtalk_wrapper.dylib
```

### Linux
```
Assets/uPiper/Plugins/Linux/x86_64/libopenjtalk_wrapper.so
```

### Android
```
Assets/uPiper/Plugins/Android/arm64-v8a/libopenjtalk_wrapper.so
Assets/uPiper/Plugins/Android/armeabi-v7a/libopenjtalk_wrapper.so
```

### iOS
```
Assets/uPiper/Plugins/iOS/libopenjtalk_wrapper.a
```

### WebGL
```
Assets/uPiper/Plugins/WebGL/openjtalk_wrapper.bc
```

## ビルド手順

### 1. 設定の適用
```
uPiper > Configure IL2CPP Settings
```

### 2. 設定の確認
```
uPiper > Verify IL2CPP Configuration
```

### 3. ビルド実行
1. File > Build Settings
2. Target Platformを選択
3. Player Settingsで追加設定を確認
4. Build

## トラブルシューティング

### "Type or namespace not found" エラー
- link.xmlに該当する型を追加
- Stripping Levelを下げる

### P/Invokeエラー
- ネイティブライブラリの配置を確認
- プラットフォーム設定を確認
- DllImportのライブラリ名を確認

### パフォーマンス問題
- C++ Compiler ConfigurationをMaster に変更
- Incremental GCを有効化
- Script Call OptimizationをFast but no exceptionsに

### メモリ不足（WebGL）
- Memory Sizeを増やす（768MB or 1024MB）
- ビルドサイズ最適化を適用

## 最適化のヒント

### コードサイズ削減
1. Stripping LevelをMediumまたはHighに
2. 未使用のパッケージを削除
3. Strip Engine Codeを有効化

### 実行速度向上
1. C++ Compiler ConfigurationをMasterに
2. LTOを有効化（Link Time Optimization）
3. プラットフォーム固有の最適化フラグを追加

### 起動時間短縮
1. Lazy型初期化を使用
2. 初期化処理を非同期化
3. リソースのオンデマンドロード

## 検証チェックリスト

- [ ] IL2CPPバックエンドが選択されている
- [ ] link.xmlが配置されている
- [ ] ネイティブライブラリが正しく配置されている
- [ ] API互換性レベルが.NET Standard 2.0
- [ ] プラットフォーム固有の設定が完了
- [ ] ビルドエラーがない
- [ ] 実機での動作確認