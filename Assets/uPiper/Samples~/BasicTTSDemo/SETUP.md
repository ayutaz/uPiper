# Basic TTS Demo Setup

このサンプルを使用するには、以下の手順に従ってください。

## セットアップ手順

### 開発中のプロジェクトの場合

1. **サンプルの手動インポート**
   - `Assets/uPiper/Samples~/BasicTTSDemo` フォルダを
   - `Assets/Samples/uPiper/BasicTTSDemo` にコピー

2. **シーンを開く**
   - コピーされた `BasicTTSDemo.unity` シーンを開く
   - またはメニューから `uPiper > Demo > Open Inference Demo Scene` を選択

### パッケージとしてインストールした場合

1. **サンプルのインポート**
   - Unity Package Managerを開く（Window > Package Manager）
   - 左上のドロップダウンから "In Project" を選択
   - uPiperパッケージを選択
   - "Samples" タブを開く
   - "Basic TTS Demo" の "Import" ボタンをクリック

2. **シーンを開く**
   - インポートされた `BasicTTSDemo.unity` シーンを開く

3. **実行**
   - 作成されたシーンを開く
   - Playボタンを押して実行
   - 日本語テキストを入力して "Generate Speech" をクリック

## トラブルシューティング

### OpenJTalkライブラリが見つからない場合
- メニューから `uPiper > Debug > OpenJTalk Status` を実行
- ライブラリの状態を確認

### 音声が生成されない場合
- Consoleウィンドウでエラーを確認
- ONNXモデルファイルが正しく配置されているか確認

## カスタマイズ

BasicTTSDemo.csを参考に、独自のTTS実装を作成できます。主な手順：

1. OpenJTalkPhonemizerで日本語を音素化
2. InferenceAudioGeneratorで音声を生成
3. AudioClipBuilderでUnityのAudioClipに変換
4. AudioSourceで再生