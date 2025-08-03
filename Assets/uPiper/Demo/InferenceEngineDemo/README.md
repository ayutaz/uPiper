# InferenceEngineDemo

Unity AI Inference Engine（旧Sentis）を使用したuPiperのデモシーンです。

## 機能

- 日本語、英語、中国語の音声合成
- リアルタイムフォネーム表示
- GPU推論サポート（Windows/macOS）
- 複数の音声モデル切り替え
- 言語別フォント自動切り替え

## 多言語フォント設定

InferenceEngineDemoで日本語・英語・中国語を正しく表示するための設定方法です。

### 自動設定（推奨）

`uPiper/Demo/Create Inference Demo Scene`メニューを使用してシーンを作成すると、フォントが自動的に設定されます：

- **Default Font Asset**: LiberationSansなどのデフォルトフォント（自動検出）
- **Japanese Font Asset**: NotoSansJP（自動検出）
- **Chinese Font Asset**: NotoSansSC（自動検出）

### 手動設定

自動設定がうまくいかない場合や、カスタムフォントを使用したい場合：

1. **InferenceEngineDemoシーンを開く**
   - `Assets/uPiper/Scenes/InferenceEngineDemo.unity`を開きます

2. **InferenceEngineDemoスクリプトを選択**
   - HierarchyでCanvasオブジェクトを選択
   - InspectorでInferenceEngineDemoコンポーネントを確認

3. **フォントを設定**
   Font Settingsセクションに以下のフォントを設定します：
   - **Default Font Asset**: デフォルトフォント（英語用）
   - **Japanese Font Asset**: `NotoSansJP-Regular SDF`（日本語用）
   - **Chinese Font Asset**: `NotoSansSC-Regular SDF`（中国語用）
   
   ※フォントアセットは`Assets/Fonts/`フォルダにあります

4. **動作確認**
   - Playモードで実行
   - モデルドロップダウンで言語を切り替え
   - 各言語のテキストが正しく表示されることを確認

### 自動フォント切り替え機能

InferenceEngineDemoには以下の自動フォント管理機能があります：

1. **言語別フォント自動切り替え**
   - モデル選択時に適切なフォントが自動的に適用されます
   - 日本語モデル → 日本語フォント
   - 中国語モデル → 中国語フォント
   - 英語モデル → デフォルトフォント

2. **フォールバックチェーン**
   - デフォルト → 日本語 → 中国語の順でフォールバック
   - 混在テキストでも正しく表示されます

3. **動的フォント管理**
   - 実行時にフォントの追加・変更が可能
   - フォントが未設定の場合は自動的にフォールバック

### 注意事項

- 各言語のフォントが設定されていない場合、該当言語のテキストは□（missing glyph）として表示されます
- 日本語フォント：ひらがな、カタカナ、常用漢字を含む
- 中国語フォント：約3,500文字の常用漢字（GB2312ベース）
- より多くの文字が必要な場合は、`Assets/uPiper/Documentation/ChineseFontSetup.md`を参照してください

## サポートされているモデル

- **ja_JP-test-medium**: 日本語音声合成（OpenJTalk使用）
- **en_US-ljspeech-medium**: 英語音声合成（Flite LTS使用）
- **zh_CN-huayan-medium**: 中国語音声合成（拡張辞書使用）

## 必要な環境

- Unity 6000.0.35f1以上
- Unity AI Inference Engine 2.2.x
- TextMeshPro（中国語フォント表示用）