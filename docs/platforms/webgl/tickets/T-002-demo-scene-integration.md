# T-002: デモシーン WebGLInput 統合
- マイルストーン: M2
- Issue: #137
- 前提チケット: T-001
- 後続チケット: T-003
- ステータス: [ ] 未着手

---

## タスク目的とゴール

デモシーン（InferenceEngineDemo.unity）のTMP_InputField GameObjectにWebGLInputコンポーネントを追加し、Editor上で既存動作に影響がないことを確認する。

WebGL版でTMP_InputFieldの日本語IME入力ができない問題を、kou-yeung/WebGLInput（MIT, v1.4.5）で解決するためのデモシーン統合タスク。

---

## 実装する内容の詳細

### WebGLInputコンポーネントの追加

- `Assets/uPiper/Scenes/InferenceEngineDemo.unity` のInputField GameObject（行4779付近）に `WebGLSupport.WebGLInput` コンポーネントを追加
  - シーンファイルをUnity Editorで開いて Add Component -> WebGLInput
  - TMP_InputFieldと同じGameObjectにアタッチ

### showHtmlElement オプションの設定検討

| 値 | 動作 | 備考 |
|----|------|------|
| `false`（デフォルト） | HTML input要素は透明。IME変換候補ウィンドウはブラウザ依存の位置に表示 | |
| `true` | HTML input要素が可視化され、テキスト選択のドラッグが可能 | |

- 推奨: まずはデフォルト（`false`）で動作確認

### InferenceEngineDemo.cs への変更は不要

WebGLInputはTMP_InputFieldのtextプロパティを透過的に更新するため、既存の `_inputField.text` 読み取りコード（行622, 632, 755, 761）はそのまま動作する。イベントリスナーの追加も不要。

---

## エージェントチームの役割と人数

| 役割 | 人数 | 担当内容 |
|------|------|----------|
| 実装エージェント | 1名 | シーンファイルの変更（Unity Editor経由、またはMCP uLoop経由） |
| 検証エージェント | 1名 | Editor再生時の動作確認（WebGLInputが enabled=false になること） |
| レビューエージェント | 1名 | シーン差分レビュー、不要な変更が含まれていないか確認 |

---

## 提供範囲とテスト項目

### 提供範囲

- InferenceEngineDemo.unity シーンファイルの変更のみ
- C# コード変更なし

### テスト項目

#### ユニットテスト
- 既存EditModeテスト全件パス（新規テスト追加なし -- シーン変更のみ）

#### E2Eテスト
- なし（WebGLビルドでの検証はT-003）

#### 手動確認
- [ ] Unity Editor でシーン再生時、WebGLInput コンポーネントが `enabled = false` になっている
- [ ] InputFieldへの英語テキスト入力が従来通り動作する
- [ ] 生成ボタン押下 -> 音声合成が従来通り動作する
- [ ] 言語切替ドロップダウンでのデフォルトテキスト設定が動作する
- [ ] InferenceEngineDemo の WebGL 条件コンパイル（行129-131: DotNetG2P非同期初期化）に影響なし

---

## 懸念事項とレビュー項目

### 懸念事項

- シーンファイルのYAML差分が大きくなる可能性（Unity EditorがGameObjectのserializationを再整列する場合）
- WebGLInputコンポーネントのデフォルト設定値が将来のバージョンで変わる可能性

### レビュー項目

- [ ] シーンファイルの差分が最小限か（WebGLInputコンポーネント追加のみ）
- [ ] WebGLInputが正しいGameObject（TMP_InputFieldと同一）にアタッチされているか
- [ ] showHtmlElement の設定値が意図通りか
- [ ] 他のGameObjectに意図しない変更が入っていないか

---

## もし一から作り直すなら

**統合方式**:
シーン直接編集は「シーンファイルに見えないバイナリ差分が残る」「Unity バージョンアップで meta や YAML 構造が壊れるリスク」「Prefab Variant 化しにくい」という欠点がある。一方、コード動的追加（`Awake` 時に `#if UNITY_WEBGL && !UNITY_EDITOR` で `gameObject.AddComponent<WebGLInput>()`）なら、シーン差分ゼロ・プラットフォーム切替が自動・Unit Test でも MonoBehaviour 単体で検証可能。理想は **`WebGLInputAutoAttach` ヘルパー MonoBehaviour** を `Runtime/Platform/WebGL/` に用意し、`InferenceEngineDemo.cs` からも `[SerializeField] TMP_InputField` を介して自動装着する方式。

**ユーザー導線設計**:
デモシーンへの手動 Add Component は「ユーザーが自分のシーンに展開する方法」を暗黙知にしてしまう。代わりに `PiperTTSWebGLHelper.EnableIMEInputFor(TMP_InputField)` のような 1 行 API を公開し、README とサンプルコードで統一導線を提示。さらに `IPiperTTS.CreateAsync` と同系列で `PiperTTS.ConfigureWebGLInput(inputField)` を提供すれば、TTS 初期化と IME 設定が同じファクトリ経由で完結する。

**設計思想**:
透過的統合（ユーザーが意識しない）と明示的制御（`showHtmlElement` など細かい設定を露出）のバランスは、**デフォルトは透過、オプトインで詳細制御** が定石。現状の「手動 Add Component」はその中間で最も認知負荷が高い。

**やり直すなら変えること**:
1. WebGLInput 装着をコード側（`[RequireComponent]` または `AddComponent`）に寄せる
2. `Tests/Editor/WebGL/WebGLInputAutoAttachTests.cs` でプラットフォーム分岐をモック検証
3. `Samples~/WebGLIMEInput/` に最小サンプルシーンを別出しし、ユーザーの参照実装にする
4. `CLAUDE.md` の「WebGL対応」セクションに導線ガイドを追記

**今回のアプローチを維持する理由**:
T-002 の目的は「WebGLInput が動くことの検証」であり、シーン直接編集は最小変更・最短 PR・レビュー容易という利点がある。コード統合は T-003 以降のフェーズで `WebGLInputAutoAttach` 化してリファクタする前提なら、今回の手動 Add Component は妥当な MVP である。C# コード変更ゼロ = 既存テストへの影響ゼロというリスク最小化が、現フェーズでは最重要。

---

## 後続タスクへの連絡事項

- **T-003（WebGLビルド・動作検証）へ**: WebGLInputコンポーネントのアタッチ完了。showHtmlElement設定値を共有
- **T-003へ**: Editor上での動作確認結果（enabled=false確認済み）を共有
- WebGLビルドでのIME動作検証はT-003の責務