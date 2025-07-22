# uPiper 開発環境でのサンプル使用方法

開発プロジェクトでは、Samples~フォルダがUnityエディタから見えないため、以下の方法でサンプルを使用してください。

## 方法1: エディタメニューから自動作成（推奨）

1. Unityメニューから `uPiper > Development > Create Basic TTS Demo Scene` を選択
2. シーン保存先を選択
   - 推奨: `Assets/uPiper/Scenes/BasicTTSDemo.unity`
   - または: `Assets/Scenes/BasicTTSDemo.unity`
3. シーンが自動的に作成され、必要なコンポーネントが配置されます
4. BasicTTSDemoコンポーネントのインスペクターで`Model Asset`フィールドにONNXモデルアセットを設定
   - 通常は自動的に検出・設定されます
   - 手動で設定する場合は、`Assets/uPiper/Resources/Models/ja_JP-test-medium.onnx`を選択

## 方法2: サンプルスクリプトのコピー

1. Unityメニューから `uPiper > Development > Copy Sample Scripts to Project` を選択
2. `Assets/Samples/uPiper/BasicTTSDemo` にファイルがコピーされます
3. コピーされたスクリプトを使用してシーンを手動で構築

## 開発時の注意事項

- BasicTTSDemoDev.csは開発用の簡略版です
- 本番のサンプルコードはSamples~/BasicTTSDemo/BasicTTSDemo.csにあります
- パッケージとして配布する際は、Samples~フォルダの内容が使用されます

## トラブルシューティング

### メニューが表示されない場合
1. スクリプトのコンパイルエラーがないか確認
2. Unity Editorを再起動

### シーン作成時のエラー
1. TextMeshProがインポートされているか確認
2. Input Systemパッケージがインストールされているか確認