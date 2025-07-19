# TextMeshPro 日本語フォント設定ガイド

## 問題
TextMeshProのデフォルトフォントは日本語文字をサポートしていないため、日本語テキストが文字化けします。

## 解決方法

### 方法1: TMP Settingsでデフォルトフォントを設定（推奨）

1. **日本語フォントの準備**
   - NotoSansJP、M PLUS、源ノ角ゴシックなどの日本語フォントをプロジェクトにインポート
   - Google Fontsから無料でダウンロード可能

2. **Font Assetの作成**
   - Window > TextMeshPro > Font Asset Creator を開く
   - Source Font File に日本語フォントを指定
   - Character Set を「Unicode Range (Hex)」に設定
   - Character Sequence に以下を入力：
     ```
     20-7E,3000-303F,3040-309F,30A0-30FF,4E00-9FAF
     ```
   - Generate Font Atlas をクリック

3. **TMP Settingsの設定**
   - Edit > Project Settings > TextMeshPro > Settings
   - Default Font Asset に作成した日本語Font Assetを設定

### 方法2: フォールバックフォントを設定

1. TMP Settingsで、Fallback Font Assets リストに日本語Font Assetを追加
2. デフォルトフォントに含まれない文字は自動的にフォールバックフォントから表示される

### 方法3: Dynamic Font Assetを使用

1. Font AssetのAtlas Population ModeをDynamicに設定
2. 実行時に必要な文字が自動的にアトラスに追加される

## uPiperでの対応

uPiperライブラリ内では英語テキストのみを使用し、日本語表示はプロジェクト側のTMP設定に依存します。これにより：

- ライブラリに大きなフォントファイルを含める必要がない
- プロジェクトごとに適切なフォントを選択できる
- ライセンスの問題を回避できる

## 注意事項

- 日本語フォントファイルは大きい（10MB以上）ため、必要な文字範囲のみをFont Assetに含めることを推奨
- CJK文字全体を含める場合、Atlas解像度を4096x4096以上に設定する必要がある場合がある