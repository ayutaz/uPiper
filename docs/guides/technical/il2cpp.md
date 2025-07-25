# IL2CPP Support Guide

## 概要

このドキュメントでは、uPiperのIL2CPPサポートに関する技術的な詳細と設定方法を説明します。

## IL2CPPとは

IL2CPP（Intermediate Language To C++）は、UnityのAhead-of-Time（AOT）コンパイラで、C#コードをC++に変換してネイティブコードにコンパイルします。

### 利点
- **パフォーマンス向上**: ネイティブコードによる高速実行
- **セキュリティ向上**: コードの難読化
- **プラットフォーム互換性**: iOS、WebGLでは必須
- **メモリ効率**: より効率的なメモリ管理

### 制約
- **ビルド時間**: Monoより長いビルド時間
- **デバッグ**: より複雑なデバッグプロセス
- **動的コード生成**: 実行時のコード生成は不可

## uPiperでのIL2CPP対応

### 1. P/Invoke設定

uPiperはネイティブライブラリ（OpenJTalk）を使用するため、P/Invokeの設定が重要です。

```csharp
// 正しいP/Invoke宣言の例
[DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
private static extern IntPtr openjtalk_create([MarshalAs(UnmanagedType.LPStr)] string dict_path);
```

重要なポイント：
- `CallingConvention`を明示的に指定
- `CharSet`を明示的に指定
- 文字列パラメータには`MarshalAs`属性を使用

### 2. link.xml設定

ストリッピングによる問題を防ぐため、`Assets/uPiper/link.xml`を作成：

```xml
<linker>
  <!-- uPiper Core -->
  <assembly fullname="uPiper.Runtime">
    <type fullname="uPiper.Core.*" preserve="all"/>
    <type fullname="uPiper.Core.Phonemizers.*" preserve="all"/>
    <type fullname="uPiper.Core.AudioGeneration.*" preserve="all"/>
  </assembly>
  
  <!-- Unity.InferenceEngine -->
  <assembly fullname="Unity.InferenceEngine">
    <type fullname="Unity.InferenceEngine.*" preserve="all"/>
  </assembly>
  
  <!-- System libraries -->
  <assembly fullname="System">
    <type fullname="System.Runtime.InteropServices.*" preserve="all"/>
  </assembly>
</linker>
```

### 3. Player Settings

#### 自動設定

Unityエディタのメニューから以下を実行：
1. `uPiper > Configure IL2CPP Settings` - IL2CPP設定を自動適用
2. `uPiper > Verify IL2CPP Configuration` - 設定の確認

#### 手動設定

**Build Settings**
- Scripting Backend: `IL2CPP`
- Api Compatibility Level: `.NET Standard 2.0`
- C++ Compiler Configuration: `Release`

**Optimization**
- Managed Stripping Level: `Low`（初期テスト用）→ `Medium`（プロダクション）
- Engine Code Stripping: `Disabled`（初期テスト用）
- Script Call Optimization: `Fast but no exceptions`

### 4. AOT対応

ジェネリック型の具体化が必要な場合：

```csharp
// AOTヒントの追加
[Preserve]
public static class AOTHelper
{
    static AOTHelper()
    {
        // ジェネリック型の具体化を強制
        var dummy1 = new List<PhonemeResult>();
        var dummy2 = new Dictionary<string, AudioClip>();
        var dummy3 = new Task<AudioChunk>();
    }
}
```

### 5. プラットフォーム別の設定

#### Windows
- Architecture: `x86_64`
- Target Platform: `Windows 10`
- Visual Studio 2019以降が必要
- Windows SDKの適切なバージョン

#### macOS
- Architecture: `Universal (Intel + Apple Silicon)`
- Minimum macOS Version: `10.13`
- Xcode最新版が必要
- コード署名の設定

#### Linux
- Architecture: `x86_64`
- GCC/Clangツールチェーン
- 必要なシステムライブラリ

#### Android
- Target Architectures: `ARM64 + ARMv7`
- Minimum API Level: `21 (Android 5.0)`
- Target API Level: `Latest`

#### iOS
- Architecture: `Universal`
- Minimum iOS Version: `11.0`
- Enable Bitcode: `No`

#### WebGL
- Linker Target: `WebAssembly`
- Memory Size: `512 MB`
- Enable Exceptions: `Explicitly Thrown Exceptions Only`

## ネイティブライブラリの配置

各プラットフォーム用のネイティブライブラリを正しい場所に配置する必要があります：

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

1. **設定の適用**: `uPiper > Configure IL2CPP Settings`
2. **設定の確認**: `uPiper > Verify IL2CPP Configuration`
3. **ビルド実行**:
   - File > Build Settings
   - Target Platformを選択
   - Player Settingsで追加設定を確認
   - Build

## トラブルシューティング

### よくある問題

1. **DllNotFoundException**
   - ネイティブライブラリのパスを確認
   - プラットフォーム設定を確認

2. **MarshalDirectiveException**
   - P/Invoke宣言のマーシャリング属性を確認
   - 構造体のレイアウトを確認

3. **TypeInitializationException**
   - 静的コンストラクタのエラーを確認
   - AOT制約違反を確認

### デバッグ方法

1. **ログ出力強化**
```csharp
#if ENABLE_IL2CPP
    Debug.Log("[IL2CPP] Loading native library...");
#endif
```

2. **ビルド時の詳細ログ**
   - Player Settings > Publishing Settings > `Development Build`を有効化
   - `Script Debugging`を有効化

### 具体的な問題と解決策

#### "Type or namespace not found" エラー
- link.xmlに該当する型を追加
- Stripping Levelを下げる

#### P/Invokeエラー
- ネイティブライブラリの配置を確認
- プラットフォーム設定を確認
- DllImportのライブラリ名を確認

#### パフォーマンス問題
- C++ Compiler ConfigurationをMaster に変更
- Incremental GCを有効化
- Script Call OptimizationをFast but no exceptionsに

#### メモリ不足（WebGL）
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

## パフォーマンスベンチマーク

### 測定項目
- 起動時間
- 音声生成時間
- メモリ使用量
- バイナリサイズ

### 期待される結果
- **起動時間**: Monoより20-30%遅い
- **実行速度**: Monoより10-20%高速
- **メモリ使用量**: Monoより10-15%少ない
- **バイナリサイズ**: Monoより大きい（プラットフォーム依存）

## 検証チェックリスト

- [ ] IL2CPPバックエンドが選択されている
- [ ] link.xmlが配置されている
- [ ] ネイティブライブラリが正しく配置されている
- [ ] API互換性レベルが.NET Standard 2.0
- [ ] プラットフォーム固有の設定が完了
- [ ] ビルドエラーがない
- [ ] 実機での動作確認

## まとめ

IL2CPPサポートにより、uPiperは：
- より高速な音声生成
- より少ないメモリ使用量
- より安全なコード配布
- 全プラットフォームでの統一されたパフォーマンス

を実現できます。初期設定には時間がかかりますが、プロダクション環境では大きなメリットがあります。