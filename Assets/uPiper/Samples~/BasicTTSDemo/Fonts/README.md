# Fonts for BasicTTSDemo

このフォルダにはBasicTTSDemoサンプルで日本語テキストを表示するためのフォントが含まれています。

## Noto Sans Japanese

- **フォント名**: Noto Sans Japanese
- **ファイル**: NotoSansJP-Regular.ttf
- **ライセンス**: SIL Open Font License, Version 1.1
- **著作権**: Copyright 2014-2021 Adobe (http://www.adobe.com/)

### ライセンス

Noto Sans JapaneseはSIL Open Font License (OFL) の下で配布されています。
詳細は同梱の `LICENSE.txt` を参照してください。

### Unity設定

このサンプルをインポートすると、日本語フォントも一緒にインポートされます。
TextMeshProコンポーネントで日本語が自動的に表示されるように設定されています。

もし日本語が表示されない場合は、以下を確認してください：
1. TextMeshProコンポーネントのFont AssetがNotoSansJP-Regular SDFに設定されているか
2. Window > TextMeshPro > Font Asset Creator でSDFアセットを再生成する必要があるか