# uPiper 開発環境でのサンプル使用方法

開発プロジェクトでは、Samples~フォルダがUnityエディタから見えないため、以下の方法でサンプルを使用してください。

## 方法1: Package Managerからサンプルをインポート（推奨）

1. Window > Package Manager で uPiper パッケージを選択
2. `Samples` から `BasicTTSDemo` をインポート
3. インポートされた `BasicTTSDemo.unity` シーンを開いて Play
   - シーンには `InferenceEngineDemo` コンポーネントが配置済みです
   - 多言語モデル（multilingual-test-medium）で6言語（ja/en/zh/es/fr/pt）に対応
4. 辞書・モデルデータが未インストールの場合は、`uPiper > Setup > Install from Samples` を実行
   - ONNXモデルは `Assets/uPiper/Resources/Models/multilingual-test-medium.onnx`

## 方法2: 開発リポジトリ内のデモシーンを直接開く

1. `Assets/uPiper/Scenes/InferenceEngineDemo.unity` を開いて Play
   - `Samples~/BasicTTSDemo/BasicTTSDemo.unity` は同一シーンのコピーです
2. デモのスクリプトは `Assets/uPiper/Runtime/Demo/InferenceEngineDemo.cs`

## 開発時の注意事項

- デモUIの実装は `Runtime/Demo/InferenceEngineDemo.cs` です
- 配布用サンプルは `Samples~/BasicTTSDemo/` に配置されます（`InferenceEngineDemo.cs` を使用）
- パッケージとして配布する際は、Samples~フォルダの内容が使用されます

## トラブルシューティング

### メニューが表示されない場合
1. スクリプトのコンパイルエラーがないか確認
2. Unity Editorを再起動

### シーン作成時のエラー
1. TextMeshProがインポートされているか確認
2. Input Systemパッケージがインストールされているか確認