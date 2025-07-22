# Phase 2: Android実装ガイド（15人日）

## 概要

Phase 2では、uPiperのAndroidプラットフォーム対応を実装します。OpenJTalkのAndroid NDKビルド、Unity統合、モバイル向け最適化を含みます。

## 前提条件

- Phase 1（Windows/Linux/macOS）が完了していること
- Android NDK r25c以上
- Unity 6000.0.35f1以上
- Docker環境（CI/CD統合用）

## 実装計画（15人日）

### Phase 2.1: 技術検証とビルド環境構築（3人日）

#### 2.1.1 Android NDK環境検証（1人日）
- **作業内容**:
  - Android NDKのバージョン確認と互換性検証
  - 必要なツールチェーンの確認
  - クロスコンパイル環境の構築
- **成果物**: 
  - Android環境検証レポート
  - ビルド要件ドキュメント
- **完了条件**: 
  - NDKでのサンプルC++プログラムがビルド可能
  - 全てのターゲットABI（arm64-v8a, armeabi-v7a, x86, x86_64）でビルド成功

#### 2.1.2 Dockerビルド環境構築（1人日）
- **作業内容**:
  - Dockerfile.androidの作成と最適化
  - ビルドスクリプトのDocker対応
  - ローカル・CI/CD共通環境の確立
- **成果物**: 
  - Dockerfile.android
  - docker-compose.yml
  - build_android_docker.sh/bat
- **完了条件**: 
  - Dockerコンテナ内でビルド成功
  - ホストOSに依存しないビルド環境

#### 2.1.3 依存ライブラリのAndroid移植調査（1人日）
- **作業内容**:
  - OpenJTalk依存ライブラリの移植性確認
  - Android固有の制限事項の調査
  - メモリ・ストレージ要件の確認
- **成果物**: 
  - 依存関係分析レポート
  - Android制限事項リスト
- **完了条件**: 
  - 全依存ライブラリの移植可能性確認
  - 回避策の策定完了

### Phase 2.2: OpenJTalkネイティブライブラリのAndroidビルド（4人日）

#### 2.2.1 CMakeツールチェーン設定（1人日）
- **作業内容**:
  - toolchain-android.cmakeの作成
  - Android向けCMakeLists.txt修正
  - プラットフォーム固有の設定追加
- **成果物**: 
  - toolchain-android.cmake
  - 更新されたCMakeLists.txt
- **完了条件**: 
  - CMakeがAndroid NDKを正しく認識
  - クロスコンパイル設定が動作

#### 2.2.2 OpenJTalk本体のAndroidビルド（2人日）
- **作業内容**:
  - HTSEngineのAndroidビルド
  - OpenJTalk本体のAndroidビルド
  - 静的ライブラリの生成
- **成果物**: 
  - build_dependencies_android.sh
  - 各ABIのlibファイル
- **完了条件**: 
  - 全ABIでビルド成功
  - 静的ライブラリのサイズが適切

#### 2.2.3 ラッパーライブラリのAndroidビルド（1人日）
- **作業内容**:
  - openjtalk_wrapper.soの生成
  - JNI互換性の確保
  - シンボルエクスポート設定
- **成果物**: 
  - libopenjtalk_wrapper.so（各ABI）
  - build_android.sh
- **完了条件**: 
  - 共有ライブラリの生成成功
  - nmコマンドでJNIシンボル確認

### Phase 2.3: Unity Android統合（4人日）

#### 2.3.1 JNIバインディング実装（2人日）
- **作業内容**:
  - AndroidOpenJTalkBinding.csの作成
  - P/InvokeからJNIへの移行
  - Android固有のパス処理
- **成果物**: 
  - AndroidOpenJTalkBinding.cs
  - AndroidPathResolver.cs
- **完了条件**: 
  - Unity EditorでのAndroidビルド設定動作
  - JNI呼び出しの成功

#### 2.3.2 Androidプラグイン設定（1人日）
- **作業内容**:
  - Unity Plugin Importerの設定
  - Android.mkの作成（必要な場合）
  - ABI別のライブラリ配置
- **成果物**: 
  - プラグイン設定済みのメタファイル
  - Android向けディレクトリ構造
- **完了条件**: 
  - Unity EditorでAndroidプラットフォーム認識
  - ビルド時の自動インクルード

#### 2.3.3 Android向けリソース管理（1人日）
- **作業内容**:
  - 辞書ファイルのStreamingAssets配置
  - Android向けファイル読み込み実装
  - パーミッション設定
- **成果物**: 
  - AndroidResourceLoader.cs
  - 更新されたAndroidManifest.xml
- **完了条件**: 
  - APK内からの辞書読み込み成功
  - 実機での動作確認

### Phase 2.4: モバイル最適化（2人日）

#### 2.4.1 メモリ使用量最適化（1人日）
- **作業内容**:
  - メモリプロファイリング
  - 辞書データの圧縮
  - メモリプールの実装
- **成果物**: 
  - メモリ最適化レポート
  - 最適化されたコード
- **完了条件**: 
  - メモリ使用量50%削減
  - 低メモリデバイスでの動作

#### 2.4.2 パフォーマンス最適化（1人日）
- **作業内容**:
  - ARM NEON最適化の検討
  - キャッシュ戦略の実装
  - バックグラウンド処理対応
- **成果物**: 
  - パフォーマンス計測結果
  - 最適化されたビルド設定
- **完了条件**: 
  - 初期化時間3秒以内
  - 音素化処理100ms以内

### Phase 2.5: テストとCI/CD統合（2人日）

#### 2.5.1 Android自動テスト（1人日）
- **作業内容**:
  - Unity Test Runnerでのテスト作成
  - 実機テストの自動化
  - デバイスファーム対応
- **成果物**: 
  - AndroidPhonemizerTests.cs
  - テスト実行スクリプト
- **完了条件**: 
  - 全テストケースのパス
  - CI上での自動実行

#### 2.5.2 CI/CDパイプライン統合（1人日）
- **作業内容**:
  - GitHub ActionsへのAndroidビルド追加
  - Dockerを使用した再現可能なビルド
  - アーティファクトの自動生成
- **成果物**: 
  - 更新された.github/workflows/build.yml
  - Androidビルドアーティファクト
- **完了条件**: 
  - PRでのAndroidビルド自動実行
  - ビルド成果物のダウンロード可能

## 技術的詳細

### サポートするAndroid バージョン
- 最小API Level: 21 (Android 5.0)
- ターゲットAPI Level: 33 (Android 13)
- 推奨API Level: 28以上

### ABI対応
| ABI | 優先度 | 備考 |
|-----|--------|------|
| arm64-v8a | 高 | 最新デバイスの主流 |
| armeabi-v7a | 中 | 古いデバイス対応 |
| x86_64 | 低 | エミュレータ用 |
| x86 | 低 | 古いエミュレータ用 |

### メモリ要件
- 最小RAM: 2GB
- 推奨RAM: 4GB以上
- 辞書データ: 約50MB（圧縮時20MB）

### ファイルサイズ予測
| コンポーネント | サイズ |
|----------------|--------|
| libopenjtalk_wrapper.so (各ABI) | 2-3MB |
| 辞書データ（圧縮） | 20MB |
| 合計APKサイズ増加 | 約30MB |

## リスクと対策

### リスク1: メモリ不足
- **対策**: 辞書の段階的ロード実装
- **代替案**: 軽量辞書の作成

### リスク2: JNI呼び出しオーバーヘッド
- **対策**: バッチ処理の実装
- **代替案**: ネイティブスレッドでの処理

### リスク3: デバイス断片化
- **対策**: 主要デバイスでの徹底テスト
- **代替案**: 互換性レイヤーの実装

## 成功基準

- ✅ 全ターゲットABIでのビルド成功
- ✅ Unity Editorからの Android ビルド成功
- ✅ 実機での音素化処理動作
- ✅ メモリ使用量が100MB以下
- ✅ 初期化時間が3秒以内
- ✅ CI/CDでの自動ビルド
- ✅ 主要Androidデバイスでの動作確認

## Phase 2完了後の次ステップ

Phase 2完了後は、以下のいずれかに進みます：
- Phase 3: WebGL実装（ブラウザ対応）
- Phase 4: iOS実装（Apple対応）
- Phase 5: エディタツール（開発体験向上）

推奨はPhase 3（WebGL）です。Dockerベースのビルド環境が確立されたため、Emscriptenビルドも同様のアプローチで実装可能です。