# Phase 2: Android実装ガイド（15人日）

## 概要

Phase 2では、uPiperのAndroidプラットフォーム対応を実装します。OpenJTalkのAndroid NDKビルド、Unity統合、モバイル向け最適化を含みます。

**実装状況**: ✅ 完了（2025年1月）

## 前提条件

- Phase 1（Windows/Linux/macOS）が完了していること ✅
- Android NDK r23b以上 ✅
- Unity 6000.0.35f1以上 ✅
- Docker環境（CI/CD統合用）✅

## 実装状況（完了）

### Phase 2.1: 技術検証とビルド環境構築 ✅

#### 2.1.1 Android NDK環境検証 ✅
- **実装内容**:
  - Android NDK r23bでの検証完了
  - CMakeツールチェーンの動作確認
  - 全ABI（arm64-v8a, armeabi-v7a, x86, x86_64）でのビルド成功
- **成果物**: 
  - `NativePlugins/OpenJTalk/toolchain-android.cmake` ✅
  - `NativePlugins/OpenJTalk/build_android.sh` ✅

#### 2.1.2 Dockerビルド環境構築 ✅
- **実装内容**:
  - Dockerfile.androidの作成（Ubuntu 20.04ベース）
  - NDK r23bとCMake 3.22.1のインストール
  - Java環境の追加（Android SDK要件）
- **成果物**: 
  - `NativePlugins/OpenJTalk/Dockerfile.android` ✅
  - `NativePlugins/OpenJTalk/test_android_build.sh` ✅
  - `NativePlugins/OpenJTalk/test_android_build.bat` ✅

#### 2.1.3 依存ライブラリのAndroid移植調査 ✅
- **実装内容**:
  - OpenJTalk依存ライブラリの移植性確認
  - C++標準ライブラリ（c++_shared）の選択
  - 静的リンクによるAPKサイズ最適化
- **解決した課題**:
  - std::__ndk1名前空間のリンクエラー → c++_sharedに変更
  - x86ビルドのシンボル未定義 → ABIリストに追加

### Phase 2.2: OpenJTalkネイティブライブラリのAndroidビルド ✅

#### 2.2.1 CMakeツールチェーン設定 ✅
- **実装内容**:
  - Android向けCMakeLists.txt修正
  - ANDROID_ABI変数を使用したライブラリパス設定
  - C++標準ライブラリの適切な選択
- **成果物**: 
  - 更新された`CMakeLists.txt`（Android対応）✅

#### 2.2.2 OpenJTalk本体のAndroidビルド ✅
- **実装内容**:
  - 全ABIでのビルド成功
  - 静的ライブラリとしてリンク
  - Docker環境での再現可能なビルド
- **ビルドサイズ**:
  - arm64-v8a: 約5.3MB
  - armeabi-v7a: 約4.9MB
  - x86_64: 約5.5MB
  - x86: 約5.2MB

#### 2.2.3 ラッパーライブラリのAndroidビルド ✅
- **実装内容**:
  - libopenjtalk_wrapper.soの生成成功
  - JNI/P/Invoke互換性の確保
  - 全ABIでのビルド完了

### Phase 2.3: Unity Android統合 ✅

#### 2.3.1 Android向けバインディング実装 ✅
- **実装内容**:
  - P/Invoke経由でのネイティブライブラリ呼び出し
  - Android固有のパス処理実装
  - StreamingAssetsからPersistentDataPathへの辞書展開
- **成果物**: 
  - `Assets/uPiper/Runtime/Core/Platform/AndroidPathResolver.cs` ✅
  - `Assets/uPiper/Editor/Build/AndroidLibraryValidator.cs` ✅

#### 2.3.2 Androidプラグイン設定 ✅
- **実装内容**:
  - Unity Plugin Importerの自動設定
  - ABI別のライブラリ配置（Plugins/Android/libs/）
  - ビルド時の自動インクルード
- **成果物**: 
  - プラグイン設定済みのメタファイル ✅
  - Android向けディレクトリ構造 ✅

#### 2.3.3 Android向けリソース管理 ✅
- **実装内容**:
  - 辞書ファイルのStreamingAssets配置
  - 初回起動時の自動展開機能
  - UnityWebRequestを使用したAPK内リソース読み込み
- **成果物**: 
  - 辞書の自動展開機能（AndroidPathResolver内）✅
  - 実機での動作確認済み ✅

### Phase 2.4: モバイル最適化（部分的に実装）

#### 2.4.1 メモリ使用量最適化 ⚠️
- **実装状況**:
  - 基本的な最適化は実装済み
  - 辞書データは展開後約50MB
  - 更なる最適化は今後の課題

#### 2.4.2 パフォーマンス最適化 ⚠️
- **実装状況**:
  - 基本的な動作は確認済み
  - ARM NEON最適化は未実装
  - キャッシュ戦略は基本実装のみ

### Phase 2.5: テストとCI/CD統合 ✅

#### 2.5.1 Android自動テスト ✅
- **実装内容**:
  - InferenceEngineDemoでの自動テスト機能
  - 起動2秒後に自動的にTTS生成テスト
  - 実機での動作確認完了
- **成果物**: 
  - AutoTestTTSGenerationコルーチン ✅

#### 2.5.2 CI/CDパイプライン統合 ✅
- **実装内容**:
  - GitHub ActionsへのAndroidビルド追加
  - Dockerを使用した再現可能なビルド
  - 全ABIのライブラリ自動生成
- **成果物**: 
  - `.github/workflows/build-native-libraries.yml`（Android対応）✅
  - Androidビルドアーティファクト ✅

## 技術的詳細

### サポートするAndroid バージョン
- 最小API Level: 28 (Android 9.0) ✅
- ターゲットAPI Level: 自動 (最新)
- テスト済みバージョン: Android 9.0以上

### ABI対応状況
| ABI | 状況 | ファイルサイズ |
|-----|------|---------------|
| arm64-v8a | ✅ | 5.3MB |
| armeabi-v7a | ✅ | 4.9MB |
| x86_64 | ✅ | 5.5MB |
| x86 | ✅ | 5.2MB |

### 既知の問題と対応

#### 文字エンコーディング問題 ✅
- **問題**: Androidログでの日本語文字化け
- **原因**: Androidのログシステムの文字エンコーディング
- **対応**: 内部処理は正常、UTF-8バイト配列を使用して回避
- **状態**: 音声合成は正常に動作

### ファイルサイズ実績
| コンポーネント | サイズ |
|----------------|--------|
| libopenjtalk_wrapper.so (全ABI合計) | 約20MB |
| 辞書データ（圧縮前） | 約50MB |
| 合計APKサイズ増加 | 約70MB |

## 成功基準（達成状況）

- ✅ 全ターゲットABIでのビルド成功
- ✅ Unity Editorからの Android ビルド成功
- ✅ 実機での音素化処理動作
- ⚠️ メモリ使用量が100MB以下（辞書展開後は約50MB）
- ⚠️ 初期化時間が3秒以内（初回は辞書展開で時間がかかる）
- ✅ CI/CDでの自動ビルド
- ✅ 主要Androidデバイスでの動作確認

## Phase 2完了後の次ステップ

Phase 2が完了し、以下が実現されました：
- ✅ Android全ABI対応のネイティブライブラリ
- ✅ Unity統合とリソース管理
- ✅ 実機での日本語TTS動作確認
- ✅ CI/CD統合

推奨される次のステップ：
1. **Androidパフォーマンス最適化**（現在進行中）
2. **Phase 3: eSpeak-NG統合**（英語音声品質向上）
3. **Phase 4: 多言語対応**（中国語・韓国語）

## 実装時の教訓

1. **Docker環境の重要性**: ローカルとCI/CDで同一環境を確保
2. **C++標準ライブラリの選択**: c++_staticではなくc++_sharedを使用
3. **文字エンコーディング**: UTF-8バイト配列での処理が確実
4. **リソース管理**: StreamingAssetsからの自動展開が必須
5. **デバッグ**: 実機ログとUnity Editorでの挙動の違いに注意