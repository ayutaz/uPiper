# WebGL対応 技術調査報告書

## 概要

このドキュメントは、uPiperのWebGL対応に関する技術調査結果をまとめたものです。Unity WebGL環境で高品質な日本語音声合成を実現するための技術的可能性と実装アプローチを詳述します。

## 調査背景

- **調査期間**: 2025年1月
- **目的**: ブラウザ環境でのフル辞書による高精度日本語音声合成の実現
- **関連プロジェクト**: [piper-plus](https://github.com/ayutaz/piper-plus)のWebAssembly実装調査

## 主要な発見

### 1. OpenJTalk WebAssembly実装の存在

npmパッケージ`wasm_open_jtalk`が既に存在し、OpenJTalkのWebAssembly実装を提供しています：

```bash
npm install wasm_open_jtalk
```

- Emscriptenを使用してビルド済み
- 辞書互換性の問題は解決済み
- Node.js環境で動作確認済み

### 2. Unity WebGLの技術的優位性

#### メモリ管理
- Unity WebGLは2-4GBのヒープを提供
- 大容量辞書ファイル（100MB）の処理が可能
- IndexedDBによる自動キャッシュシステム

#### JavaScript連携
Unity WebGLでは`.jslib`ファイルを使用したJavaScript連携が確立されています：

```javascript
// sample.jslib
mergeInto(LibraryManager.library, {
    InitOpenJTalkWasm: function() {
        // OpenJTalk WebAssemblyの初期化
    },
    PhonemizeText: function(textPtr) {
        var text = UTF8ToString(textPtr);
        var phonemes = openjtalk_wasm.phonemize(text);
        return allocateUTF8(phonemes);
    }
});
```

```csharp
// Unity C#側
[DllImport("__Internal")]
private static extern void InitOpenJTalkWasm();

[DllImport("__Internal")]
private static extern string PhonemizeText(string text);
```

## piper-plusプロジェクトとの比較

### piper-plusの課題（PR #118）

piper-plusプロジェクトでは、MeCab辞書フォーマットの非互換性により実装が断念されました：

- **問題**: DARTS（Double Array Trie）実装の非互換性
- **結果**: 音素化精度0%
- **原因**: MeCab独自のバイナリフォーマット

### uPiperでの解決策

1. **既存実装の活用**: `wasm_open_jtalk`パッケージを使用
2. **Unity基盤の活用**: WebGLランタイムによる強力なサポート
3. **段階的実装**: 動作確認済みコンポーネントから構築

## 実装アプローチ

### Phase 1: 基盤整備（1-2週間）

1. **WebGL検出とフォールバック**
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
public class WebGLPhonemizer : IPhonmizer
{
    // WebGL専用実装
}
#endif
```

2. **辞書ファイルの外部配信**
- CDNからの動的読み込み
- UnityWebRequestによる非同期取得
- IndexedDBキャッシュ

### Phase 2: OpenJTalk統合（2-3週間）

1. **wasm_open_jtalkの統合**
- ブラウザ環境への適応
- Unity jslib連携実装

2. **パフォーマンス最適化**
- 初回読み込みの最適化
- メモリ使用量の管理

### Phase 3: 品質保証（1週間）

1. **精度検証**
- デスクトップ版との比較
- テストケースの実行

2. **ユーザビリティ改善**
- 読み込み進捗表示
- エラーハンドリング

## 技術的制約と対策

### 制約

1. **初回読み込み時間**: 辞書ダウンロード（100MB）に10-20秒
2. **メモリ使用量**: 辞書＋モデルで300-400MB
3. **ブラウザ要件**: WebAssembly対応必須

### 対策

1. **プログレッシブ読み込み**: 必要な部分から順次読み込み
2. **キャッシュ戦略**: IndexedDBによる永続化
3. **フォールバック**: 非対応環境での適切な案内

## 推奨事項

### 実装優先度: **高**

WebGL対応により以下が実現可能：

- ✅ インストール不要のデモ環境
- ✅ pyopenjtalkと同等の音素化精度
- ✅ クロスプラットフォーム対応
- ✅ 容易な配布とアクセス

### 次のステップ

1. **piper-plusでの実装**: より軽量な実装から開始
2. **uPiperへの統合**: piper-plusの成果を活用
3. **継続的改善**: パフォーマンスとUXの向上

## 結論

Unity WebGL環境でのフル辞書による高精度日本語音声合成は**技術的に実現可能**です。`wasm_open_jtalk`の活用とUnity WebGLの強力な基盤により、ブラウザ上でもデスクトップ版と同等の品質を提供できます。

## 参考資料

- [wasm_open_jtalk NPMパッケージ](https://www.npmjs.com/package/wasm_open_jtalk)
- [Unity WebGL: Browser Scripting](https://docs.unity3d.com/Manual/webgl-interactingwithbrowserscripting.html)
- [piper-plus PR #118](https://github.com/ayutaz/piper-plus/pull/118)