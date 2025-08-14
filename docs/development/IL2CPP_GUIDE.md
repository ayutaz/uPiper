# IL2CPPサポートガイド

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

を実現できます。初期設定には時間がかかりますが、プロダクション環境では大きなメリットがあります。# IL2CPP Compatibility Report for uPiper

## 概要

このドキュメントは、uPiperプロジェクトのIL2CPP互換性検証結果をまとめたものです。

検証日: 2025年1月22日
Unity バージョン: 6000.0.55f1
対象プラットフォーム: Windows, Linux, macOS, Android, iOS, WebGL

## 1. P/Invoke宣言の検証

### 1.1 OpenJTalkPhonemizerのP/Invoke

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Implementations/OpenJTalkPhonemizer.cs`

```csharp
[DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
private static extern int openjtalk_initialize([MarshalAs(UnmanagedType.LPStr)] string dictionary_path);

[DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
private static extern void openjtalk_synthesize(
    [MarshalAs(UnmanagedType.LPStr)] string text,
    ref NativePhonemeResult result);

[DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
private static extern void openjtalk_free_result(ref NativePhonemeResult result);

[DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
private static extern void openjtalk_finalize();
```

**互換性評価**: ✅ 完全互換
- CallingConvention.Cdeclは適切
- MarshalAsAttributeの使用は正しい
- 構造体の参照渡しも問題なし

### 1.2 NativePhonemeResult構造体

```csharp
[StructLayout(LayoutKind.Sequential)]
private struct NativePhonemeResult
{
    public IntPtr phonemes;
    public int phoneme_count;
    public IntPtr durations;
    public IntPtr pitches;
    public int error_code;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public byte[] error_message;
}
```

**互換性評価**: ✅ 完全互換
- LayoutKind.Sequentialは適切
- ByValArray with SizeConstはIL2CPP対応
- IntPtrの使用は推奨される方法

## 2. マーシャリング属性の検証

### 2.1 文字列マーシャリング

現在の実装:
- `[MarshalAs(UnmanagedType.LPStr)]` - UTF-8文字列として扱われる

**推奨事項**: 
- 日本語対応のため、UTF-8エンコーディングが必要
- 現在の実装で問題なし

### 2.2 配列マーシャリング

現在の実装:
- 固定サイズ配列: `[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]`
- 動的配列: IntPtrとして受け取り、手動でマーシャリング

**互換性評価**: ✅ 完全互換

## 3. AOT制約の確認

### 3.1 ジェネリック型の使用

確認された使用箇所:
- `Dictionary<string, T>` - ✅ 問題なし
- `List<T>` - ✅ 問題なし
- `IAsyncEnumerable<AudioChunk>` - ⚠️ 要確認
- `Task<T>` - ✅ 問題なし

### 3.2 リフレクションの使用

検出された箇所:
- なし（直接的なリフレクション使用は検出されず）

### 3.3 動的コード生成

検出された箇所:
- なし（Expression Trees、DynamicMethodの使用なし）

## 4. Unity AI Inference Engine (旧Sentis) の互換性

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

確認事項:
- Worker APIの使用 - ✅ IL2CPP対応済み
- TensorFloat/TensorIntの使用 - ✅ IL2CPP対応済み
- BackendType.CPU - ✅ 全プラットフォーム対応

## 5. 非互換箇所と対応策

### 5.1 IAsyncEnumerableの使用

**影響箇所**: 
- `StreamAudioAsync`メソッド

**対応策**:
- System.Linq.Asyncパッケージの依存を確認
- AOT用のコード生成が必要な可能性

### 5.2 async/awaitパターン

**現状**: 
- 広範囲で使用されているが、Unity 2023以降では完全サポート
- Unity 6000.0では問題なし

## 6. プラットフォーム固有の考慮事項

### 6.1 iOS
- ネイティブライブラリは静的リンクが必要
- Info.plistへの設定追加が必要な場合あり

### 6.2 Android
- arm64-v8aとarmeabi-v7aの両方のライブラリが必要
- Android.mkまたはCMakeでのビルド設定

### 6.3 WebGL
- ネイティブライブラリはEmscriptenでビルド必要
- P/Invokeは自動的にJavaScript呼び出しに変換される

## 7. 推奨される追加対応

### 7.1 link.xmlの作成
- Unity.AI.InferenceEngineの型保持
- カスタム型の保持設定

### 7.2 AOT属性の追加
- ジェネリックメソッドへの[Preserve]属性
- インターフェース実装への明示的な型保持

### 7.3 ストリッピングレベルの調整
- 初期テストでは"Low"推奨
- 動作確認後に"Medium"または"High"へ

## 結論

uPiperは基本的にIL2CPP互換性が高く、大きな修正は不要です。以下の軽微な対応で完全な互換性を達成できます：

1. link.xmlの作成
2. IAsyncEnumerableの動作確認
3. プラットフォーム固有のネイティブライブラリビルド

推定作業時間: 0.5人日（計画通り）

## CI/CD環境での制限事項

### Dockerベースの制限
Unity BuilderのDockerイメージには以下の制限があります：

1. **Windows IL2CPP**: ❌ サポートされていない
   - Visual StudioとWindows SDKが必要
   - Linuxコンテナでは実行不可

2. **macOS IL2CPP**: ❌ サポートされていない  
   - Xcodeとネイティブツールチェーンが必要
   - macOSランナーでの直接実行が必要

3. **Linux IL2CPP**: ✅ 完全サポート
   - Dockerイメージに必要なツールが含まれている

### 回避策
- **セルフホストランナー**: Windows/macOS環境でUnity+IL2CPPをインストール
- **クラウドビルドサービス**: Unity Cloud Buildなどの利用
- **プラットフォーム別ランナー**: GitHub ActionsのWindows/macOSランナーを使用（ライセンス追加必要）# IL2CPP CI/CDソリューション

## 現状の制限

Unity BuilderのDockerベースのアプローチでは、以下の制限があります：

| プラットフォーム | Mono | IL2CPP | 理由 |
|-----------------|------|--------|------|
| Linux | ✅ | ✅ | Dockerイメージに全ツール含まれる |
| Windows | ✅ | ❌ | Visual Studio必要 |
| macOS | ✅ | ❌ | Xcode必要 |

## 解決策

### 1. プラットフォーム別GitHub Actionsランナー

**メリット**:
- 完全なIL2CPPサポート
- ネイティブツールチェーンへのアクセス

**デメリット**:
- 追加のUnityライセンスが必要（プラットフォームごと）
- ビルド時間が長い
- コストが高い

**実装例**: `.github/workflows/unity-il2cpp-native.yml`参照

### 2. Unity Cloud Build

**メリット**:
- 設定が簡単
- 全プラットフォーム対応
- Unityが管理

**デメリット**:
- 有料サービス
- ビルド時間制限あり

**設定方法**:
1. Unity Dashboardでプロジェクトを作成
2. GitHubリポジトリを連携
3. ビルド設定でIL2CPPを選択

### 3. セルフホストランナー

**メリット**:
- 完全なコントロール
- 高速ビルド（キャッシュ活用）
- コスト効率的（長期的に）

**デメリット**:
- 初期セットアップが複雑
- メンテナンスが必要
- セキュリティ考慮事項

**セットアップ手順**:
```bash
# Windows
1. Unity Hub + IL2CPPモジュールをインストール
2. Visual Studio 2019/2022をインストール
3. GitHub Actions runnerを設定

# macOS  
1. Unity Hub + IL2CPPモジュールをインストール
2. Xcodeをインストール
3. GitHub Actions runnerを設定
```

### 4. ハイブリッドアプローチ（推奨）

開発フェーズに応じて使い分け：

1. **開発中**: 
   - PR: Monoビルドのみ（高速）
   - Linux IL2CPPで基本動作確認

2. **リリース前**:
   - 手動トリガーでネイティブランナー使用
   - 全プラットフォームIL2CPPビルド

3. **本番リリース**:
   - Unity Cloud Buildで最終ビルド
   - または検証済みセルフホストランナー

## コスト比較

| ソリューション | 初期コスト | 運用コスト | ビルド時間 |
|--------------|-----------|-----------|-----------|
| Dockerのみ | 低 | 低 | 速い（Monoのみ） |
| GitHub Actions | 中 | 高 | 遅い |
| Unity Cloud | 低 | 中 | 中 |
| セルフホスト | 高 | 低 | 速い |

## 推奨事項

1. **小規模プロジェクト**: DockerでLinux IL2CPPのみ
2. **中規模プロジェクト**: Unity Cloud Build
3. **大規模プロジェクト**: セルフホストランナー
4. **オープンソース**: ハイブリッドアプローチ

## 実装状況

- ✅ Linux IL2CPP（Docker）- CIで自動実行
- ✅ 全プラットフォームMono - CIで自動実行
- ❌ Windows/macOS IL2CPP - Dockerでは実行不可
- 📝 代替案: Unity Cloud Build、セルフホストランナー（ドキュメント化済み）