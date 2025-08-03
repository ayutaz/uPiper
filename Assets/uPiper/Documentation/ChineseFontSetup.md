# 中国語フォント設定ガイド

## 問題
Unity EditorやビルドでInferenceEngineDemoの中国語テキストが文字化けする。

## 原因
プロジェクトに中国語フォントが含まれていないため。

## 解決方法

### 方法1: Noto Sans CJKを使用（推奨）

1. **フォントのダウンロード**
   - [Google Fonts - Noto Sans CJK](https://fonts.google.com/noto/specimen/Noto+Sans+SC) から「Noto Sans SC」（簡体字中国語）をダウンロード
   - または「Noto Sans CJK SC」（中日韓統合版）を使用

2. **Unityにインポート**
   - ダウンロードしたフォントファイル（.otfまたは.ttf）を`Assets/Fonts/`フォルダにドラッグ＆ドロップ

3. **TextMeshProフォントアセットの作成**
   - メニューから `Window > TextMeshPro > Font Asset Creator` を開く
   - 設定：
     - Source Font File: インポートしたNotoSansCJKフォントを選択
     - Sampling Point Size: 36（または必要に応じて調整）
     - Padding: 5
     - Packing Method: Fast
     - Atlas Resolution: 2048x2048（文字数に応じて調整）
     - Character Set: Custom Characters
     - Custom Character List: 以下の文字を貼り付け
       ```
       你好世界谢欢迎使用推理引擎今天气真这是一个语音合成测试中文效果如何我们正在已经实现了早上也要加油啊自定义输入
       ```
   - `Generate Font Atlas`をクリック
   - `Save`で`Assets/Fonts/NotoSansCJK-Regular SDF.asset`として保存

4. **フォントの適用**
   - InferenceEngineDemoシーンを開く
   - 中国語を表示するTextMeshProコンポーネントを選択
   - Font Assetに作成したNotoSansCJK SDFを設定

### 方法2: フォールバックフォントとして設定

1. **TMP Settingsを開く**
   - `Assets/TextMesh Pro/Resources/TMP Settings.asset`を選択

2. **Fallback Font Assets List**に追加
   - 作成した中国語フォントアセットを追加
   - これにより、既存のフォントで表示できない文字が自動的に中国語フォントで表示される

### 方法3: Dynamic SDF（Unity 2023.1以降）

Unity 2023.1以降では、Dynamic SDFを使用して実行時に必要な文字を動的に生成できます：

1. フォントアセットのインスペクターで「Atlas Population Mode」を「Dynamic」に設定
2. Multi Atlas Textureを有効化

## テスト方法

1. InferenceEngineDemoシーンを開く
2. Playモードで実行
3. モデルドロップダウンから「zh_CN-huayan-medium」を選択
4. 中国語フレーズが正しく表示されることを確認

## 注意事項

- **重要**: Unicode範囲 19968-40895 (4E00-9FBF) は約21,000文字の漢字を含むため、Unity Editorがフリーズします
- 中国語の全文字（約20,000文字）を含めるとフォントアセットが非常に大きくなるため、実際に使用する文字のみを含めることを推奨
- ビルドサイズを最小限に抑えるため、使用する中国語フレーズに含まれる文字のみをフォントアセットに含める
- Androidビルドの場合、フォントファイルサイズに注意（APKサイズ制限）

## Unity Editorフリーズの回避方法

### 大量の文字が必要な場合

1. **段階的生成**
   ```
   第1段階: 基本漢字 4E00-4FFF (約4,000文字)
   第2段階: 常用漢字 5000-5FFF (約4,000文字)
   第3段階: 必要に応じて追加
   ```

2. **最適化設定**
   ```
   Atlas Resolution: 2048x2048 → 4096x4096
   Sampling Point Size: 36 → 24
   Padding: 5 → 3
   Render Mode: SDFAA → SDF
   ```

3. **事前生成された文字リストを使用**
   - `Assets/uPiper/Documentation/ChineseCharacterList.txt` - デモ専用の最小限の文字セット（約100文字）
   - `Assets/uPiper/Documentation/ChineseCommonCharacters.txt` - 常用漢字セット（約3,500文字、自由入力対応）
   - これをCustom Character Listにコピー＆ペースト

### 推奨設定（3,500文字の場合）

```
Font Asset Creator設定:
- Atlas Resolution: 4096x4096
- Sampling Point Size: 28-32
- Padding: 4
- Packing Method: Fast
- Render Mode: SDF（SDFAAより軽い）
```

これにより、一般的な中国語入力の99%以上をカバーできます。

## トラブルシューティング

### まだ文字化けする場合
1. TextMeshProコンポーネントのMaterialが正しく設定されているか確認
2. フォントアセットに必要な文字が含まれているか確認（Font Asset CreatorのCharacter Tableで確認）
3. Canvas Scalerの設定を確認（Reference PixelsやDynamic Pixels Per Unitの値）

### パフォーマンスの問題
- フォントアセットのAtlas解像度を下げる（1024x1024など）
- 使用する文字を最小限に絞る
- Dynamic SDFの使用を検討（Unity 2023.1以降）