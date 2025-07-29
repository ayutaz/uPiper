# Tools Directory

このディレクトリには、uPiper開発用の各種ツールとスクリプトが整理されて含まれています。

## 📁 ディレクトリ構造

```
Tools/
├── FliteLTS/          # Flite LTS関連ツール
├── Unity/             # Unity開発支援ツール
├── Build/             # ビルド関連ツール
└── README.md          # このファイル
```

## 🛠️ ツール一覧

### FliteLTS/
Flite Letter-to-Sound (LTS) システムのC#移植に使用したツール群。

- **FliteLTSRuleParser.py** - Fliteのルールデータを解析してC#形式に変換
- **extract_flite_lts_data.py** - Fliteソースからルールデータを抽出

### Unity/
Unity開発を支援するツール群。

- **CleanUnityCache.bat** - Unityのキャッシュをクリーンアップ
  ```batch
  Tools\Unity\CleanUnityCache.bat
  ```

- **disable_duplicate_menu_items.py** - 重複したメニュー項目を無効化
- **update_menu_items.py** - メニュー項目を更新・整理
- **fix_meta_guids.py** - .metaファイルのGUID重複を修正

### Build/
ビルドプロセスを支援するツール群。

- **create_dict_zip.bat** - OpenJTalk辞書をZIPパッケージ化
  ```batch
  Tools\Build\create_dict_zip.bat
  ```

## 💡 使用方法

各ツールは目的別にフォルダ分けされています。使用する際は該当フォルダに移動してから実行してください。

### Python スクリプトの実行例
```bash
python Tools/FliteLTS/extract_flite_lts_data.py
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
- Flite/音素化関連 → `FliteLTS/`
- Unity開発支援 → `Unity/`
- ビルド/パッケージング → `Build/`