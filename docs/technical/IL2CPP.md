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

Unity Editorでの設定：

1. **File > Build Settings > Player Settings**
2. **Configuration**:
   - Scripting Backend: `IL2CPP`
   - Api Compatibility Level: `.NET Standard 2.1`
3. **Optimization**:
   - Managed Stripping Level: `Medium`（推奨）
   - C++ Compiler Configuration: `Master`（リリース時）

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

### 5. プラットフォーム別の考慮事項

#### Windows
- Visual Studio 2019以降が必要
- Windows SDKの適切なバージョン

#### macOS
- Xcode最新版が必要
- コード署名の設定

#### Linux
- GCC/Clangツールチェーン
- 必要なシステムライブラリ

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

## まとめ

IL2CPPサポートにより、uPiperは：
- より高速な音声生成
- より少ないメモリ使用量
- より安全なコード配布
- 全プラットフォームでの統一されたパフォーマンス

を実現できます。初期設定には時間がかかりますが、プロダクション環境では大きなメリットがあります。