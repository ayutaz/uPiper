# T-001: WebGLInput パッケージ導入・ビルド検証

- マイルストーン: M1
- Issue: #137
- 前提チケット: なし
- 後続チケット: T-002
- ステータス: [ ] 未着手

---

## タスク目的とゴール

WebGL版でTMP_InputFieldの日本語IME入力ができない問題（Issue #137）を解決するため、[kou-yeung/WebGLInput](https://github.com/kou-yeung/WebGLInput)（MIT, 939 stars, v1.4.5）をUPM Git URL経由で導入する。

**ゴール**: WebGLInputパッケージがUPM経由で正しくインストールされ、全プラットフォームでコンパイル・ビルドが通ることを確認する。このチケットでは機能変更は行わず、パッケージ導入とビルド検証のみをスコープとする。

---

## 実装する内容の詳細

### 1. `Packages/manifest.json` への依存追加

既存のdependenciesセクション（dot-net-g2p Git URL参照が複数ある）の末尾に以下を追加する:

```json
"com.github.kou-yeung": "https://github.com/kou-yeung/WebGLInput.git?path=Assets/WebGLSupport#1.4.5"
```

- scopedRegistriesの変更は不要（OpenUPMスコープに該当しない）
- Git URLのタグ指定 `#1.4.5` でバージョンを固定する

### 2. `Assets/uPiper/link.xml` のIL2CPP strip対応確認

- WebGLInputのasmdefは`includePlatforms`が空で全プラットフォームコンパイル対象
- WebGLInput内にリフレクション使用がないか確認し、必要ならpreserveエントリを追加する
- リフレクション未使用の場合はlink.xmlの変更なし

### 3. asmdef参照について

asmdef参照の追加は**不要**。理由:
- WebGLInputの利用方法はデモシーンのGameObjectにコンポーネントを追加するだけ
- uPiper.Runtime.asmdefからWebGLInputへのコード参照は発生しない
- スクリプトからの直接参照が必要になるのはT-002以降のスコープ

---

## エージェントチームの役割と人数

| 役割 | 人数 | 担当内容 |
|------|------|----------|
| 実装エージェント | 1名 | manifest.json編集、link.xml確認・必要に応じた変更 |
| 検証エージェント | 1名 | パッケージ解決確認、既存テスト影響確認、全プラットフォームコンパイル検証 |
| レビューエージェント | 1名 | 変更内容のクロスレビュー、懸念事項の最終チェック |

---

## 提供範囲とテスト項目

### 提供範囲

- `Packages/manifest.json` の変更のみ（最小変更）
- 必要に応じて `Assets/uPiper/link.xml` の変更

### テスト項目

#### ユニットテスト
- 既存EditModeテスト全件パス（新規テスト追加なし -- パッケージ追加のみのため）

#### E2Eテスト
- なし（このフェーズでは機能変更なし）

#### 手動確認
- [ ] Unity Editor でコンパイルエラーなし
- [ ] `WebGLSupport` namespace がスクリプトから参照可能
- [ ] 既存EditModeテスト全件パス
- [ ] WebGLビルドターゲットでコンパイルエラーなし

---

## 懸念事項とレビュー項目

### 懸念事項

- **バージョン固定**: WebGLInputのバージョン固定（`#1.4.5`）で将来のアップデート追従が必要。メジャーアップデート時にGit URLのタグを手動更新する運用が発生する
- **ビルド時間**: WebGLInput asmdefが全プラットフォームでコンパイルされるため、ビルド時間への微小な影響がある
- **dotnet-format**: WebGLInput内のコードがdotnet-formatのフォーマットチェック対象になるか確認が必要（Packages配下のため通常は対象外だが、CI設定次第）

### レビュー項目

- [ ] Git URL のタグ指定（`#1.4.5`）が正しいか
- [ ] 依存パッケージ（`com.unity.ugui`）が既存manifest.jsonに含まれているか
- [ ] link.xmlにpreserve追加が必要な型がないか
- [ ] CI環境でのGit URL解決が可能か（パブリックリポジトリ）

---

## もし一から作り直すなら

**パッケージ導入戦略**:
選択肢は4つ: (1) Git URL直接参照（現行案）、(2) OpenUPM経由、(3) unitypackage同梱、(4) git submodule。OpenUPMはレジストリ登録が必要でWebGLInputは未登録のため即時採用不可。unitypackage同梱はバージョン管理が不透明になりライセンス表記の責任がプロジェクト側に移るため保守コスト増。git submoduleはUnity Package Managerのキャッシュ機構と相性が悪く、Unity起動時の解決フローから外れる。結果として Git URL + path指定が既存dot-net-g2pと同じ機構で一貫性があり最適。

**バージョン固定方針**:
既存dot-net-g2pは `#v1.8.2` のタグ固定方式を採用。WebGLInputも同様に `#1.4.5` タグ固定とし、一貫性を保つ。コミットハッシュ固定はタグ改変リスク対策として強いが可読性が落ちる。ブランチ追従（`#main`）は再現性が壊れるためCI環境では禁忌。

**設計思想**:
外部依存は「Unity公式ではカバーされない & 自前実装コストが非現実的」な領域のみに限定する。WebGLInputはWebGL固有のブラウザIME連携という非常に狭い領域に特化しており、自前実装する場合jslib + DOM操作の深い知識が必要。依存を受け入れる価値がある。

**やり直すなら変えること**:
1. Git URLを `#commit-hash` 固定にしてタグ改変リスクを排除
2. `WebGLInput` をフォークして自組織のリポジトリにミラーリングし、upstream消滅リスクをヘッジ
3. `link.xml` にpreserveエントリを予防的に追加しIL2CPP stripのedge caseに備える

**今回のアプローチを維持する理由**:
dot-net-g2pと同じ機構で運用一貫性があり、学習コストが発生しない。939 starsの実績あるMITライブラリで、タグ固定により再現性も確保されている。フォークミラーは将来リスクが顕在化してからの対応で十分。

---

## 後続タスクへの連絡事項

- **T-002（デモシーン統合）へ**: パッケージ導入完了後、`WebGLSupport.WebGLInput` コンポーネントが利用可能になる
- WebGLInputのAssembly名とnamespaceの確認結果を共有すること
- IL2CPP strip対応の結果（link.xml変更有無）を共有すること