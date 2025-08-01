# iOS実装チェックリスト

このドキュメントは、iOS対応の完了状況を確認するためのチェックリストです。

## 1. ネイティブライブラリ

### ビルド環境
- [x] ios-cmake toolchainのダウンロードと設定
- [x] build_ios.shスクリプトの作成
- [x] build_dependencies_ios.shスクリプトの作成
- [x] CMakeLists.txtのiOS対応（静的ライブラリビルド）
- [x] Xcodeビルド環境の初期設定（xcodebuild -runFirstLaunch）

### ビルド成果物
- [x] libopenjtalk_wrapper.a（静的ライブラリ）の生成
- [x] HTSEngine APIのiOSビルド
- [x] OpenJTalkライブラリのiOSビルド
- [x] Assets/uPiper/Plugins/iOSへの配置
- [x] 適切な.metaファイルの設定（ARM64、静的リンク）

## 2. Unity側のコード対応

### P/Invoke設定
- [x] ENABLE_PINVOKEマクロにUNITY_IOSを追加
- [x] DllImportで"__Internal"を使用（iOS向け）
- [x] IsNativeLibraryAvailableメソッドのiOS対応
- [x] プラットフォーム判定（PlatformHelper.IsIOS）の実装

### ファイルパス解決
- [x] IOSPathResolverクラスの実装
- [x] StreamingAssetsのiOSパス（Application.dataPath + "/Raw"）対応
- [x] GetDefaultDictionaryPathメソッドのiOS対応
- [x] 辞書ファイルアクセスの検証機能

### エラーハンドリング
- [x] iOS固有のエラーメッセージ
- [x] メモリ不足時の対応
- [x] ファイルアクセスエラーの適切な処理

## 3. リソースファイル

### 辞書ファイル
- [x] フル辞書（mecab-naist-jdic）のStreamingAssets配置
- [x] 必要な8つのファイルの確認（sys.dic, unk.dic等）
- [x] ファイルサイズの検証（sys.dic: 98MB）
- [x] .metaファイルの生成

### モデルファイル
- [x] ONNXモデルファイルのStreamingAssets配置
- [ ] iOSでのモデルファイルアクセステスト（実機確認待ち）

## 4. テスト

### 単体テスト
- [x] OpenJTalkPhonemizerIOSTest
- [x] IOSPlatformTest
- [x] IOSPathResolverTest
- [x] IOSBuildValidationTest（エディタテスト）

### 統合テスト
- [x] IOSIntegrationTest
- [x] メモリ使用量テスト
- [x] スレッドセーフティテスト
- [x] パフォーマンステスト

### サンプル/デモ
- [x] IOSTestControllerの実装
- [x] 辞書アクセステスト機能
- [x] Phonemizerテスト機能
- [x] TTSパイプラインテスト機能
- [ ] 実機でのUIシーン作成（Unity Editor作業）

## 5. ビルド設定

### PlayerSettings
- [ ] iOS最小バージョン設定（11.0以上）
- [ ] アーキテクチャ設定（ARM64）
- [ ] グラフィックスAPI設定（Metal優先）
- [ ] その他のiOS固有設定

### ビルドパイプライン
- [ ] Xcodeプロジェクトの自動設定スクリプト（オプション）
- [ ] CI/CDパイプラインへのiOSビルド追加（オプション）

## 6. ドキュメント

### 技術ドキュメント
- [x] iOS実装計画ドキュメント
- [x] 実装記録と技術的課題の文書化
- [x] IOSデモのREADME
- [x] このチェックリスト

### ユーザー向けドキュメント
- [ ] iOSビルド手順書
- [ ] トラブルシューティングガイド
- [ ] パフォーマンスガイドライン

## 7. 最適化（オプション）

### パフォーマンス
- [ ] メモリ使用量の最適化
- [ ] 起動時間の最適化
- [ ] バッテリー消費の測定と最適化

### 追加機能
- [ ] iOSシミュレータサポート（x86_64）
- [ ] ユニバーサルバイナリの作成
- [ ] Bitcodeサポート（廃止予定のため低優先度）

## 実機テスト前の必須項目

以下の項目が完了していれば、実機テストに進めます：

1. **ネイティブライブラリ** ✅
   - libopenjtalk_wrapper.aが正しく配置されている
   - .metaファイルが適切に設定されている

2. **コード対応** ✅
   - P/Invoke設定が完了している
   - ファイルパス解決が実装されている

3. **リソース** ✅
   - 辞書ファイルがStreamingAssetsに配置されている
   - テスト用のONNXモデルがある

4. **テストコード** ✅
   - 基本的な動作確認用のテストが実装されている
   - IOSTestControllerで実機テストができる

5. **Unity Editor設定** ⚠️
   - PlayerSettingsのiOS設定が必要
   - ビルド設定の確認が必要

## 結論

**実機テストに必要な実装はほぼ完了しています。**

残っている作業：
1. Unity EditorでのPlayerSettings設定
2. 実際のビルドとデプロイ
3. 実機での動作確認

これらはUnity Editorでの作業となるため、コード実装としては完了状態です。