# IL2CPP Compatibility Report for uPiper

## 概要

このドキュメントは、uPiperプロジェクトのIL2CPP互換性検証結果をまとめたものです。

検証日: 2025年1月22日
Unity バージョン: 6000.0.35f1
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
- **プラットフォーム別ランナー**: GitHub ActionsのWindows/macOSランナーを使用（ライセンス追加必要）