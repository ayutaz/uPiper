# Tools Directory

このディレクトリには、uPiper開発用の各種ツールとスクリプトが含まれています。

## 📁 ディレクトリ構造

```
Tools/
├── Unity/                          # Unity開発支援ツール
├── Build/                          # ビルド関連ツール
├── ConvertPinyinDictionary.py      # 中国語ピンイン辞書 (txt→JSON) 変換
├── ExpandChineseDictionary.py      # 中国語辞書の拡張・生成
└── README.md                       # このファイル
```

## 🛠️ ツール一覧

### Unity/
Unity開発を支援するツール群。

- **CleanUnityCache.bat** - Unityのキャッシュをクリーンアップ
  ```batch
  Tools\Unity\CleanUnityCache.bat
  ```
  ビルドエラーやアセットインポートの問題が発生した場合に使用します。

### Build/
ビルドプロセスを支援するツール群。

- **create_dict_zip.bat** - OpenJTalk辞書をZIPパッケージ化
  ```batch
  Tools\Build\create_dict_zip.bat
  ```
  Android向けのStreamingAssetsで使用するZIPファイルを作成します。

### Python スクリプト（Tools/ 直下）
中国語G2P用辞書データの変換・生成スクリプト。

- **ConvertPinyinDictionary.py** - タブ区切りのピンイン辞書テキストをJSON形式に変換
  ```bash
  python Tools/ConvertPinyinDictionary.py
  ```
- **ExpandChineseDictionary.py** - pypinyin / CC-CEDICT 等を取得し中国語辞書を拡張・生成
  ```bash
  python Tools/ExpandChineseDictionary.py
  ```

## 💡 使用方法

各ツールは目的別にフォルダ分けされています。使用する際は該当フォルダに移動してから実行してください。

### Python スクリプトの実行例
```bash
python Tools/ConvertPinyinDictionary.py
```

### バッチファイルの実行例
```batch
Tools\Unity\CleanUnityCache.bat
```

## ⚠️ 注意事項

- バッチファイル（.bat）はWindows環境専用です
- Pythonスクリプトの実行にはPython 3.6以上が必要です
- Unity関連ツールは、実行前にUnityエディタを閉じることを推奨します
- 一部のツールは管理者権限が必要な場合があります

## 🔧 開発者向け情報

新しいツールを追加する場合は、適切なカテゴリのフォルダに配置してください：
- 辞書変換/データ生成スクリプト（Python）→ `Tools/` 直下
- Unity開発支援 → `Unity/`
- ビルド/パッケージング → `Build/`