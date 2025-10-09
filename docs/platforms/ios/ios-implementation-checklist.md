# iOS実装チェックリスト

> **最終更新**: 2025-10-09
> **ステータス**: コード実装完了、Unity Editorでのビルド設定待ち

このドキュメントは、iOS対応の完了状況を確認するためのチェックリストです。

## 1. ネイティブライブラリ ✅ 完了

### ビルド環境
- [x] ios-cmake toolchainのダウンロードと設定
- [x] build_ios.shスクリプトの作成（3881バイト、実装済み）
- [x] build_dependencies_ios.shスクリプトの作成
- [x] CMakeLists.txtのiOS対応（静的ライブラリビルド）
- [x] Xcodeビルド環境の初期設定（xcodebuild -runFirstLaunch）

### ビルド成果物
- [x] libopenjtalk_wrapper.a（静的ライブラリ）の生成（1.46MB）
- [x] HTSEngine APIのiOSビルド
- [x] OpenJTalkライブラリのiOSビルド
- [x] Assets/uPiper/Plugins/iOSへの配置（確認済み）
- [x] 適切な.metaファイルの設定（ARM64、静的リンク）

## 2. Unity側のコード対応 ✅ 完了

### P/Invoke設定
- [x] ENABLE_PINVOKEマクロにUNITY_IOSを追加（OpenJTalkPhonemizer.cs:4-6）
- [x] DllImportで"__Internal"を使用（iOS向け）（OpenJTalkPhonemizer.cs:49-50）
- [x] IsNativeLibraryAvailableメソッドのiOS対応（OpenJTalkPhonemizer.cs:639-642）
- [x] プラットフォーム判定（PlatformHelper.IsIOS）の実装

### ファイルパス解決
- [x] IOSPathResolverクラスの実装（IOSPathResolver.cs実装済み）
- [x] StreamingAssetsのiOSパス（Application.dataPath + "/Raw"）対応（IOSPathResolver.cs:22）
- [x] GetDefaultDictionaryPathメソッドのiOS対応（OpenJTalkPhonemizer.cs:529-531）
- [x] 辞書ファイルアクセスの検証機能（IOSPathResolver.cs:29-47）

### エラーハンドリング
- [x] iOS固有のエラーメッセージ（IOSPathResolver.cs:41, 101, etc）
- [x] メモリ不足時の対応（テストコードで実装）
- [x] ファイルアクセスエラーの適切な処理（IOSPathResolver.cs:98-103）

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

### サンプル/デモ ✅ 実装完了
- [x] IOSTestControllerの実装（IOSTestController.cs: 305行）
- [x] 辞書アクセステスト機能（IOSTestController.cs:87-129）
- [x] Phonemizerテスト機能（IOSTestController.cs:131-164）
- [x] TTSパイプラインテスト機能（IOSTestController.cs:166-281）
- [ ] 実機でのUIシーン作成（Unity Editor作業）

## 5. ビルド設定 ⚠️ Unity Editor作業

### PlayerSettings（未実施）
- [ ] iOS最小バージョン設定（11.0以上）
- [ ] アーキテクチャ設定（ARM64）
- [ ] グラフィックスAPI設定（Metal優先）
- [ ] API Compatibility Level（.NET Standard 2.1）
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

1. **ネイティブライブラリ** ✅ 完了
   - libopenjtalk_wrapper.a（1.46MB）が正しく配置されている
   - .metaファイルが適切に設定されている
   - build_ios.shスクリプト実装済み

2. **コード対応** ✅ 完了
   - P/Invoke設定が完了している（`__Internal`リンク）
   - IOSPathResolverでファイルパス解決が実装されている
   - OpenJTalkPhonemizer.csのiOS対応完了

3. **リソース** ✅ 準備完了
   - 辞書ファイルがStreamingAssetsに配置可能
   - テスト用のONNXモデルの準備完了
   - Application.dataPath + "/Raw"パス対応

4. **テストコード** ✅ 完了
   - 基本的な動作確認用のテストが実装されている
   - IOSTestControllerで実機テストが可能
   - 包括的なテストスイート実装済み

5. **Unity Editor設定** ⚠️ 未実施
   - PlayerSettingsのiOS設定が必要
   - ビルド設定の確認が必要

## 現在のステータス（2025-10-09）

### ✅ 完了項目
- **コード実装**: 100%完了
- **ネイティブライブラリ**: ビルド済み・配置済み
- **テスト環境**: 実装済み
- **ドキュメント**: 整備済み

### ⚠️ 残作業（Unity Editorでの作業）
1. Unity PlayerSettingsの設定
   - iOS最小バージョン: 11.0
   - アーキテクチャ: ARM64
   - API Level: .NET Standard 2.1
2. Xcodeプロジェクトのビルドとエクスポート
3. 実機へのデプロイとテスト
4. パフォーマンスプロファイリング

## 結論

**コード実装は完全に完了しています。実機テストの準備は整っています。**

Unity Editorでのビルド設定を行えば、すぐに実機テストが可能な状態です。