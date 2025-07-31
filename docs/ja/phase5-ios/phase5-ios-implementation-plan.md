# Phase 5: iOS Support Implementation Plan

## 概要
uPiperのiOSプラットフォームサポートを実装し、iPhoneおよびiPadでの動作を可能にする。

## ゴール
1. macOSでのローカルビルドによるiOS端末へのインストールと動作確認
2. GitHub ActionsでのiPAビルド（オプション）
3. 署名は各開発者が個別に対応

## 技術的要件

### 1. Unity AI Inference Engine
- iOS対応済み（Unity AI Inference Engine 2.2.1）
- パフォーマンスはデバイス性能に依存

### 2. OpenJTalkネイティブライブラリ
- 静的ライブラリ（.a）としてビルド
- arm64アーキテクチャ（実機）
- x86_64アーキテクチャ（シミュレータ、オプション）
- Universal Binary（fat binary）の作成

### 3. iOSプラットフォーム制約
- メモリ使用量の最適化
- バックグラウンド動作制限への対応
- オーディオセッション管理

## 実装計画

### Phase 5.1: iOS用OpenJTalkライブラリビルド (2日)

#### 1. iOS用ビルドスクリプト作成
```bash
# NativePlugins/OpenJTalk/build_ios.sh
```

- Xcodeツールチェーンを使用
- arm64/x86_64両対応
- ビットコード対応（必要な場合）

#### 2. CMakeLists.txt修正
- iOS固有の設定追加
- 静的ライブラリビルド設定
- iOS SDKパス設定

#### 3. 依存ライブラリの対応
- mecab: iOS用ビルド
- dictionary: リソースとしてバンドル

### Phase 5.2: Unity統合 (1日)

#### 1. プラグイン配置
```
Assets/uPiper/Plugins/iOS/
├── libopenjtalk_wrapper.a
├── libopenjtalk_wrapper.a.meta
└── OpenJTalkBridge.mm (必要な場合)
```

#### 2. プラグイン設定
- Platform settings: iOS
- CPU: ARM64
- Settings: 適切なフレームワーク依存

#### 3. P/Invoke設定
```csharp
#if UNITY_IOS
    private const string LIBRARY_NAME = "__Internal";
#endif
```

### Phase 5.3: iOS固有の実装 (1日)

#### 1. リソース管理
- 辞書ファイルのStreamingAssetsからの読み込み
- iOS向けパス処理

#### 2. メモリ最適化
- 辞書の遅延読み込み
- 不要時の解放

#### 3. オーディオ対応
- AVAudioSessionとの統合
- 割り込み処理

### Phase 5.4: テストとデバッグ (1日)

#### 1. ローカルビルドテスト
- Xcodeプロジェクトのエクスポート
- 実機でのテスト
- パフォーマンス測定

#### 2. CI/CD設定（オプション）
- GitHub Actionsでのビルド
- 証明書なしでのアーカイブ作成

## 技術的課題と対策

### 1. 辞書ファイルサイズ
- **課題**: naist_jdic辞書が大きい（約100MB）
- **対策**: 
  - 圧縮して配布、初回起動時に展開
  - 軽量辞書の作成（オプション）

### 2. メモリ使用量
- **課題**: iOSのメモリ制限
- **対策**:
  - 辞書の部分読み込み
  - キャッシュサイズの調整

### 3. App Store審査
- **課題**: 動的ライブラリの制限
- **対策**: 静的ライブラリとして実装

## 実装の順序

1. **iOS用OpenJTalkビルド環境構築**
   - build_ios.shスクリプト作成
   - CMakeLists.txt修正

2. **静的ライブラリビルド**
   - arm64版ビルド
   - 動作確認用簡易プログラム作成

3. **Unity統合**
   - プラグイン配置・設定
   - P/Invoke調整

4. **iOS固有機能実装**
   - リソース管理
   - メモリ最適化

5. **テスト**
   - 実機テスト
   - パフォーマンス検証

## 成果物

1. **ネイティブライブラリ**
   - libopenjtalk_wrapper.a (arm64)
   - libopenjtalk_wrapper.a (x86_64, オプション)

2. **Unityプラグイン**
   - iOS向けプラグイン設定
   - 必要に応じてObjective-C++ブリッジ

3. **ドキュメント**
   - iOSビルド手順
   - 実装時の注意点

4. **ビルドスクリプト**
   - build_ios.sh
   - GitHub Actionsワークフロー（オプション）

## スケジュール

- **Day 1-2**: iOS用OpenJTalkライブラリビルド
- **Day 3**: Unity統合
- **Day 4**: iOS固有実装
- **Day 5**: テストとデバッグ

合計: 5日間

## 注意事項

1. **証明書・プロビジョニング**
   - 開発者各自で設定
   - ドキュメントに手順記載

2. **Xcodeバージョン**
   - Xcode 14以上推奨
   - iOS SDK 15以上

3. **Unity設定**
   - iOS Build Support必須
   - Player Settings適切に設定

4. **テスト端末**
   - iPhone実機推奨
   - iOS 15以上