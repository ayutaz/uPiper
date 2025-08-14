# Fonts for BasicTTSDemo

このフォルダにはBasicTTSDemoサンプルで日本語・英語テキストを表示するためのフォントが含まれています。

## Liberation Sans (英語用)

- **フォント名**: Liberation Sans
- **ファイル**: LiberationSans.ttf
- **ライセンス**: SIL Open Font License, Version 1.1
- **著作権**: Copyright (c) 2012 Red Hat, Inc.

## Noto Sans Japanese (日本語用)

- **フォント名**: Noto Sans Japanese
- **ファイル**: NotoSansJP-Regular.ttf
- **ライセンス**: SIL Open Font License, Version 1.1
- **著作権**: Copyright 2014-2021 Adobe (http://www.adobe.com/)

## ライセンス

両フォントともSIL Open Font License (OFL) の下で配布されています。
- Liberation Sans: `LiberationSans-LICENSE.txt`
- Noto Sans Japanese: `LICENSE.txt`

### Unity設定

このサンプルをインポートすると、日本語フォントも一緒にインポートされます。
TextMeshProコンポーネントで日本語が自動的に表示されるように設定されています。

もし日本語が表示されない場合は、以下を確認してください：
1. TextMeshProコンポーネントのFont AssetがNotoSansJP-Regular SDFに設定されているか
2. Window > TextMeshPro > Font Asset Creator でSDFアセットを再生成する必要があるか