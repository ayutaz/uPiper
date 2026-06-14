# WebGL 日本語入力対応

> Issue: [#137](https://github.com/ayutaz/uPiper/issues/137)

## 問題

Unity WebGL ビルドでは、標準の `TMP_InputField` で日本語 IME 入力ができない。

### 原因

Unity の WebGL ビルドは Canvas 上でブラウザの `keydown`/`keyup` イベントを直接キャプチャするため、ブラウザの IME composition パイプライン（`compositionstart` → `compositionupdate` → `compositionend`）がバイパスされる。その結果、日本語・中国語・韓国語などの IME を使用する言語の変換入力ができない。

Unity Input System (`com.unity.inputsystem`) にも WebGL IME 対応は存在しない。

### 現状の実装

デモ UI (`InferenceEngineDemo.cs`) は `TMP_InputField`（行54）を使用し、テキスト取得は `_inputField.text`（行622, 632, 755, 761）で直接読み取り。WebGL 固有の IME 対応処理は存在しない。

シーンファイル (`InferenceEngineDemo.unity`) 上の TMP_InputField コンポーネント設定:
- `m_ContentType: 0`（Standard）、`m_InputType: 0`（Standard）
- InputField イベント（`onValueChanged`, `onEndEdit` 等）はすべて未登録
- 全テキスト処理は C# コード側で制御

```
ユーザー入力（日本語 IME）
    ↓
ブラウザの composition イベント
    ↓ ← Unity Canvas がイベントをインターセプト
TMP_InputField（Unity 側）
    ├─ 確定済みテキスト: 一部取得可能
    └─ 変換中テキスト: 取得不可 ← 問題
```

## 解決策: kou-yeung/WebGLInput

### 概要

| 項目 | 詳細 |
|------|------|
| リポジトリ | https://github.com/kou-yeung/WebGLInput |
| Stars | 939 |
| ライセンス | MIT |
| 最新バージョン | v1.4.5 (2025-12-05) |
| パッケージ名 | `com.github.kou-yeung` |
| namespace | `WebGLSupport` |
| パッケージサイズ | 18KB |
| Unity 要件 | 2023.2+（uPiper: 6000.3.11f1 で互換性あり） |
| 依存パッケージ | `com.unity.ugui: 1.0.0~` のみ（uPiper で導入済み） |
| TMP_InputField 対応 | あり（`WrappedTMPInputField`、Unity 2018.2+ で自動有効） |

### 動作原理

ブラウザネイティブの HTML `<input>` / `<textarea>` 要素を Unity Canvas 上にオーバーレイし、ブラウザの IME 機能をそのまま利用する。

```
ユーザー入力（日本語 IME）
    ↓
ブラウザネイティブ HTML <input>（IME composition 処理）
    ↓ oninput / onchange イベント
WebGLInput jslib（DllImport コールバック）
    ↓
TMP_InputField（Unity 側）に確定テキストを反映
```

1. `TMP_InputField` がフォーカスされると、jslib が `document.createElement("input")` で HTML input 要素を動的作成
2. InputField の画面座標に合わせて input 要素を配置
3. ブラウザの IME が HTML input 上でネイティブに動作（composition 対応）
4. テキスト変更を `[DllImport("__Internal")]` コールバック経由で Unity に通知
5. フォーカスが外れると HTML input 要素を削除

### 日本語入力の対応状況

- リポジトリの説明が「IME for Unity WebGL」であり、IME 対応が主目的
- [Issue #88](https://github.com/kou-yeung/WebGLInput/issues/88): 通常版の `WebGLInput` では日本語入力が動作することが報告済み（Mobile 版の 4byte 文字のみ問題があったが解決済み）
- ネイティブ HTML input を使用するため、ブラウザが対応するすべての IME 言語（日本語・中国語・韓国語等）が自動的に利用可能

### jslib 構成

| ファイル | 役割 |
|---------|------|
| `WebGLInput/WebGLInput.jslib` | コア。HTML input/textarea の動的生成、IME 入力、コピペ、選択、フォーカス管理 |
| `WebGLInput/Mobile/WebGLInputMobile.jslib` | モバイルブラウザ対応（touchend/focusout イベント） |
| `WebGLWindow/WebGLWindow.jslib` | ウィンドウフォーカス/ブラー/リサイズ検出、フルスクリーン切替 |

uPiper 既存 jslib（`WebGLAudioContext`, `IndexedDBCache`, `WebGLBrowserLanguage`）との名前空間・関数名の競合なし。

### プラットフォーム影響

WebGL 以外のプラットフォームへの悪影響なし:
- `#if UNITY_WEBGL && !UNITY_EDITOR` ガードで DllImport を分離
- Editor/Standalone では `Awake()` 内で `enabled = false` に設定され、Update ループ停止
- Assembly Definition の `includePlatforms` が空のため全プラットフォームでコンパイル可能だが、実行時は無操作

### 他の候補との比較

| ライブラリ | Stars | 状態 | TMP 対応 | 備考 |
|-----------|-------|------|---------|------|
| **kou-yeung/WebGLInput** | 939 | 活発 | あり | 最有力、MIT |
| unity3d-jp/WebGLNativeInputField | 112 | Archived | なし | WebGLInput の前身、非推奨 |
| dantasulisses/WebMobileInputFix | 56 | 活発 | あり | モバイル向け、機能限定 |

### 既知の制限事項

- リッチテキストフォーマットとの競合（[#183](https://github.com/kou-yeung/WebGLInput/issues/183)）
- ウィンドウリサイズ時の HTML 要素位置ずれ（[#191](https://github.com/kou-yeung/WebGLInput/issues/191)）
- iOS ランドスケープモードの問題（[#182](https://github.com/kou-yeung/WebGLInput/issues/182)）

---

## マイルストーン

> チケット詳細: [tickets/](tickets/) ディレクトリ参照

### M1: パッケージ導入・ビルド検証 → [T-001](tickets/T-001-package-integration.md)

**目標**: WebGLInput を UPM で導入し、全プラットフォームでビルドが通ることを確認する。

| # | タスク | 対象ファイル | 完了条件 |
|---|--------|------------|---------|
| 1-1 | `manifest.json` に WebGLInput パッケージを追加 | `Packages/manifest.json` | `"com.github.kou-yeung": "https://github.com/kou-yeung/WebGLInput.git?path=Assets/WebGLSupport#1.4.5"` が追加されている |
| 1-2 | Unity Editor でパッケージ解決確認 | — | コンパイルエラーなし、`WebGLSupport` namespace が参照可能 |
| 1-3 | 既存テスト通過確認 | — | EditMode テスト全件パス |
| 1-4 | IL2CPP ビルド互換確認 | `Assets/uPiper/link.xml` | WebGLInput に IL2CPP strip 対象の型がないか確認。必要なら preserve エントリ追加 |

### M2: デモシーン統合 → [T-002](tickets/T-002-demo-scene-integration.md)

**目標**: デモシーンの InputField で日本語 IME 入力が動作する状態にする。

| # | タスク | 対象ファイル | 完了条件 |
|---|--------|------------|---------|
| 2-1 | InputField GameObject に `WebGLInput` コンポーネントを追加 | `Assets/uPiper/Scenes/InferenceEngineDemo.unity` | InputField に `WebGLSupport.WebGLInput` コンポーネントが付与されている |
| 2-2 | Editor 上で動作確認（無操作であること） | — | Editor 再生時に WebGLInput が `enabled = false` になり既存動作に影響なし |
| 2-3 | `showHtmlElement` オプションの検討 | — | テキスト選択ドラッグが必要か判断し、必要に応じて `true` に設定 |

### M3: WebGL ビルド・動作検証 → [T-003](tickets/T-003-webgl-build-verification.md)

**目標**: WebGL ビルドで日本語 IME 入力→音声合成の E2E 動作を確認する。

| # | タスク | 完了条件 |
|---|--------|---------|
| 3-1 | WebGL ビルド成功 | エラーなしでビルド完了 |
| 3-2 | 日本語 IME 入力テスト | ブラウザ上で日本語テキストを入力し、`_inputField.text` に正しく反映される |
| 3-3 | 日本語テキスト→音声合成 E2E テスト | 入力した日本語テキストで音声生成が正常に完了する |
| 3-4 | 英語直接入力テスト | 既存の英語入力が引き続き動作する |
| 3-5 | 中国語・韓国語 IME テスト（任意） | CJK 言語の IME 入力が動作する |
| 3-6 | ブラウザ互換テスト | Chrome / Firefox / Edge で動作確認 |
| 3-7 | コピー&ペースト動作確認 | Ctrl+C / Ctrl+V でテキスト貼り付けが動作する |

### M4: CI/CD 対応 → [T-004](tickets/T-004-cicd-pipeline.md)

**目標**: CI パイプラインで WebGLInput 含むビルド・テストが自動実行される。

| # | タスク | 対象ファイル | 完了条件 |
|---|--------|------------|---------|
| 4-1 | CI WebGL ビルド通過確認 | `.github/workflows/unity-build.yml` | WebGL マトリクスビルドが成功 |
| 4-2 | CI テスト通過確認 | `.github/workflows/unity-tests.yml` | EditMode テスト全件パス |
| 4-3 | deploy-webgl.yml 動作確認 | `.github/workflows/deploy-webgl.yml` | GitHub Pages デプロイ成功 |
| 4-4 | IL2CPP ビルド通過確認 | `.github/workflows/unity-il2cpp-build.yml` | IL2CPP 互換性チェックパス |

### M5: ドキュメント・リリース → [T-005](tickets/T-005-documentation-release.md)

**目標**: ユーザー向けドキュメントを整備し、PR をマージする。

| # | タスク | 対象ファイル | 完了条件 |
|---|--------|------------|---------|
| 5-1 | WebGL セットアップガイドに日本語入力セクション追加 | `docs/platforms/webgl/SETUP.md` | IME 対応の注意事項・WebGLInput コンポーネントの説明を追記 |
| 5-2 | PR 作成・レビュー | — | `feature/webgl-japanese-input` → `develop` の PR |
| 5-3 | Issue #137 クローズ | — | PR マージ後に自動クローズ |

---

## 技術的補足

### manifest.json 変更内容

```json
{
  "dependencies": {
    "com.github.kou-yeung": "https://github.com/kou-yeung/WebGLInput.git?path=Assets/WebGLSupport#1.4.5",
    ...
  }
}
```

### asmdef 参照

デモシーンの InputField に `WebGLInput` コンポーネントを追加するだけの場合、`uPiper.Runtime.asmdef` への参照追加は不要。WebGLInput は独立した Assembly として動作し、TMP_InputField の GameObject にアタッチされたコンポーネントとして機能する。

### 既存 WebGL 機能との関係

WebGLInput は入力 UI の IME 補完であり、uPiper の TTS 推論パイプライン（音素化 → エンコード → ONNX 推論 → AudioClip 生成）には一切影響しない。既存の WebGL Phase 1-4 実装（`WebGLInteractionGate`, `WebGLStreamingAssetsLoader`, `IndexedDBCache` 等）とは独立して動作する。
