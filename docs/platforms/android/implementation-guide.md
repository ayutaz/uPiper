# Phase 2: Android実装ガイド（15人日）

## 概要

Phase 2では、uPiperのAndroidプラットフォーム対応を実装します。dot-net-g2p（純粋C#実装）による音素化、Unity統合、モバイル向け最適化を含みます。

> **注**: 現在のuPiperはdot-net-g2p（純粋C#実装、NuGetパッケージ）を使用しており、ネイティブライブラリ（.so/.dll/.dylib）は不要です。以下の記載は当初のOpenJTalkネイティブ実装時の履歴を含みます。

**実装状況**: ✅ 完了（2025年1月）

## 前提条件

- Phase 1（Windows/Linux/macOS）が完了していること ✅
- Unity 6000.0.35f1以上 ✅
- Docker環境（CI/CD統合用）✅

## 実装状況（完了）

### Phase 2.1: 技術検証とビルド環境構築 ✅

> **注**: 以下のネイティブビルド関連の記載は、当初のOpenJTalk C++実装時のものです。現在はdot-net-g2p（純粋C#実装）に移行済みのため、ネイティブライブラリのビルドは不要です。

#### 2.1.1 環境検証 ✅
- **現在の実装**:
  - dot-net-g2p（純粋C#実装）を使用
  - ネイティブライブラリ（.so/.dll/.dylib）は不要
  - MeCab辞書（NAIST Japanese Dictionary）のみ必要

### Phase 2.2: 音素化ライブラリの統合 ✅

- **現在の実装**:
  - dot-net-g2pをNuGetパッケージとして参照
  - 純粋C#実装のため、ABI別のネイティブビルドは不要
  - IL2CPPとの互換性確認済み

### Phase 2.3: Unity Android統合 ✅

#### 2.3.1 Android向け統合実装 ✅
- **実装内容**:
  - dot-net-g2p（純粋C#）による音素化処理
  - Android固有のパス処理実装
  - StreamingAssetsからPersistentDataPathへの辞書展開
- **成果物**: 
  - `Assets/uPiper/Runtime/Core/Platform/AndroidPathResolver.cs` ✅
  - `Assets/uPiper/Editor/Build/AndroidLibraryValidator.cs` ✅

#### 2.3.2 辞書データ配置 ✅
- **実装内容**:
  - MeCab辞書（NAIST Japanese Dictionary）のStreamingAssets配置
  - ネイティブプラグイン不要（純粋C#実装）
- **成果物**:
  - StreamingAssets/uPiper/配下の辞書ファイル ✅

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

dot-net-g2p（純粋C#実装）を使用しているため、ABI別のネイティブライブラリは不要です。
Unity IL2CPPビルドで全ABI（arm64-v8a, armeabi-v7a, x86, x86_64）に対応しています。

### 既知の問題と対応

#### 文字エンコーディング問題 ✅
- **問題**: Androidログでの日本語文字化け
- **原因**: Androidのログシステムの文字エンコーディング
- **対応**: 内部処理は正常、UTF-8バイト配列を使用して回避
- **状態**: 音声合成は正常に動作

### ファイルサイズ実績
| コンポーネント | サイズ |
|----------------|--------|
| dot-net-g2p（純粋C#、DLL） | 数百KB |
| 辞書データ（MeCab NAIST Japanese Dictionary） | 約50MB |
| 合計APKサイズ増加 | 約50MB |

## 成功基準（達成状況）

- ✅ dot-net-g2p（純粋C#）による音素化動作
- ✅ Unity Editorからの Android ビルド成功
- ✅ 実機での音素化処理動作
- ⚠️ メモリ使用量が100MB以下（辞書展開後は約50MB）
- ⚠️ 初期化時間が3秒以内（初回は辞書展開で時間がかかる）
- ✅ CI/CDでの自動ビルド
- ✅ 主要Androidデバイスでの動作確認

## Phase 2完了後の次ステップ

Phase 2が完了し、以下が実現されました：
- ✅ dot-net-g2p（純粋C#実装）によるAndroid音素化対応
- ✅ Unity統合とリソース管理
- ✅ 実機での日本語TTS動作確認
- ✅ CI/CD統合

推奨される次のステップ：
1. **Androidパフォーマンス最適化**（現在進行中）
2. **Phase 3: eSpeak-NG統合**（英語音声品質向上）
3. **Phase 4: 多言語対応**（中国語・韓国語）

## 実装時の教訓

1. **純粋C#実装の利点**: dot-net-g2pにより、ネイティブビルド不要でクロスプラットフォーム対応
2. **文字エンコーディング**: UTF-8バイト配列での処理が確実
3. **リソース管理**: StreamingAssetsからの自動展開が必須
4. **デバッグ**: 実機ログとUnity Editorでの挙動の違いに注意