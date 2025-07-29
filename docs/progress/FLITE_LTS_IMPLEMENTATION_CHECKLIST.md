# Flite LTS C#移植 実装チェックリスト

## 実装前準備

### 環境セットアップ
- [ ] Fliteソースコード取得（v2.2）
- [ ] Python 3.12環境準備（uv使用）
- [ ] 開発ブランチ作成: `feature/flite-lts-csharp-port`

### 分析ツール準備
- [ ] Fliteビルド環境構築（デバッグ用）
- [ ] データ抽出スクリプト環境
- [ ] 比較検証ツール準備

## Phase 1: データ移植準備（Day 1-2）

### Day 1: データ抽出
- [ ] `cmu_lts_rules.c`の構造分析
  - [ ] 音素テーブル抽出（76音素）
  - [ ] 文字インデックス抽出（27エントリー）
  - [ ] ルールデータ形式の理解
- [ ] Pythonスクリプト作成
  - [ ] `scripts/extract_flite_lts_data.py`
  - [ ] データ検証機能
  - [ ] C#コード生成機能

### Day 2: C#データ構造
- [ ] データクラス設計
  - [ ] `FliteLTSData.cs`
  - [ ] `FliteLTSRuleSet.cs`
  - [ ] `FlitePhoneMapping.cs`
- [ ] 自動生成コード
  - [ ] `FliteLTSData.Generated.cs`
  - [ ] データ圧縮・展開処理
- [ ] 単体テスト作成
  - [ ] データ整合性テスト
  - [ ] 音素マッピングテスト

## Phase 2: コアエンジン実装（Day 3-5）

### Day 3: 基本実装
- [ ] `FliteLTSEngine.cs`実装
  - [ ] WFSTプロセッサー基本構造
  - [ ] ルール適用メカニズム
  - [ ] 状態遷移処理
- [ ] `FliteWFSTProcessor.cs`実装
  - [ ] 有限状態機械の実装
  - [ ] 重み計算ロジック
  - [ ] 最適パス探索

### Day 4: アルゴリズム完成
- [ ] コンテキスト処理
  - [ ] 前後の文字認識
  - [ ] 特殊ケース処理
- [ ] エラーハンドリング
  - [ ] 不正入力対応
  - [ ] フォールバック処理
- [ ] 基本動作テスト
  - [ ] 単純な単語テスト
  - [ ] 複雑な単語テスト

### Day 5: 最適化
- [ ] パフォーマンス最適化
  - [ ] ルックアップテーブル最適化
  - [ ] メモリアクセスパターン改善
  - [ ] 不要なアロケーション削減
- [ ] キャッシュ実装
  - [ ] LRUキャッシュ（10,000エントリー）
  - [ ] スレッドセーフ実装
- [ ] ベンチマーク作成
  - [ ] 処理速度測定
  - [ ] メモリ使用量測定

## Phase 3: システム統合（Day 6-7）

### Day 6: 統合実装
- [ ] `FliteLTSPhonemizer.cs`実装
  - [ ] PhonemizerBackendBase継承
  - [ ] 辞書との連携
  - [ ] 非同期処理対応
- [ ] 設定管理
  - [ ] キャッシュサイズ設定
  - [ ] 優先度設定
  - [ ] フォールバック設定
- [ ] エラー処理
  - [ ] 例外ハンドリング
  - [ ] ログ出力

### Day 7: テストと文書化
- [ ] 統合テスト
  - [ ] 既存システムとの互換性
  - [ ] エンドツーエンドテスト
  - [ ] 回帰テスト
- [ ] パフォーマンステスト
  - [ ] Fliteネイティブとの比較
  - [ ] メモリプロファイリング
  - [ ] 負荷テスト
- [ ] ドキュメント作成
  - [ ] APIドキュメント
  - [ ] 実装ガイド
  - [ ] トラブルシューティング

## 検証項目

### 精度検証
- [ ] CMU辞書収録語の検証（1000語サンプル）
- [ ] 未知語の検証（500語サンプル）
- [ ] Fliteネイティブとの比較（差分1%以内）

### パフォーマンス検証
- [ ] 処理速度：1000単語/秒以上
- [ ] メモリ使用量：10MB以下
- [ ] 起動時間：100ms以下

### 互換性検証
- [ ] Windows 10/11
- [ ] macOS 12+
- [ ] Ubuntu 20.04+
- [ ] Unity 2021.3 LTS
- [ ] Unity 6000.0

## リリース準備

### コード品質
- [ ] コードレビュー完了
- [ ] 静的解析パス（0警告）
- [ ] テストカバレッジ80%以上

### ドキュメント
- [ ] README.md更新
- [ ] CHANGELOG.md更新
- [ ] Migration Guide作成

### 最終確認
- [ ] ライセンス表記確認
- [ ] パッケージサイズ確認
- [ ] 依存関係確認

## 成果物

### コード成果物
1. `Assets/uPiper/Runtime/Core/Phonemizers/Backend/Flite/`
   - `FliteLTSPhonemizer.cs`
   - `FliteLTSEngine.cs`
   - `FliteWFSTProcessor.cs`
   - `FliteLTSData.Generated.cs`

2. `Assets/uPiper/Tests/Runtime/Phonemizers/Flite/`
   - `FliteLTSEngineTests.cs`
   - `FliteLTSPhonemizerTests.cs`
   - `FliteAccuracyTests.cs`

3. `scripts/`
   - `extract_flite_lts_data.py`
   - `generate_flite_csharp_data.py`
   - `validate_flite_accuracy.py`

### ドキュメント成果物
1. 技術文書
   - `FLITE_LTS_PORTING_PLAN.md`
   - `FLITE_LTS_IMPLEMENTATION_GUIDE.md`
   - `FLITE_LTS_API_REFERENCE.md`

2. ユーザー向け文書
   - `MIGRATION_FROM_NATIVE_FLITE.md`
   - `FLITE_LTS_USAGE_GUIDE.md`

## 連絡事項

- 日次進捗報告
- ブロッカー発生時は即座に報告
- コードレビューは各Phase完了時

## 備考

- Fliteバージョン: 2.2
- 対象Unity: 2021.3 LTS以上
- .NET Standard 2.1準拠
- 非同期処理対応必須