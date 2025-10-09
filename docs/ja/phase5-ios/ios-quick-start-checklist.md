# iOS実機テスト クイックスタートチェックリスト 📱

## 今すぐ始めるための5ステップ

### ステップ 1: デバイス接続 (1分)
- [ ] iPhoneをUSBケーブルでMacに接続
- [ ] 「このコンピュータを信頼しますか？」→ **信頼** をタップ
- [ ] デバイスのパスコードを入力

### ステップ 2: Xcodeプロジェクトを開く (1分)
```bash
# Unity でビルドしたフォルダを開く
open [ビルドフォルダ]/Unity-iPhone.xcodeproj

# または、ヘルパースクリプトを使用
./scripts/ios_deploy_helper.sh open
```

### ステップ 3: 署名設定 (3分)
1. [ ] Xcodeで **Unity-iPhone** ターゲット選択
2. [ ] **Signing & Capabilities** タブ
3. [ ] **Automatically manage signing** をチェック
4. [ ] **Team** を選択（Personal Team でOK）
5. [ ] Bundle ID を変更: `com.[あなたの名前].uPiper`

### ステップ 4: ビルドと実行 (5-10分)
1. [ ] 上部でデバイスを選択（例: "山田のiPhone"）
2. [ ] **⌘ + R** でビルド開始
3. [ ] ビルド完了を待つ

### ステップ 5: 初回起動設定 (2分)
エラー「信頼されていない開発者」が表示されたら：
1. [ ] iPhoneで **設定** アプリを開く
2. [ ] **一般** → **VPNとデバイス管理**
3. [ ] **デベロッパAPP** のあなたのApple IDを選択
4. [ ] **信頼** をタップ
5. [ ] アプリを再度起動

---

## ✅ 動作確認チェックリスト

### 基本動作
- [ ] アプリが起動する
- [ ] UIが表示される
- [ ] 日本語が文字化けしていない

### TTS機能
- [ ] モデル選択: `ja_JP-test-medium` を選択
- [ ] テキスト入力: 「こんにちは」を入力
- [ ] 「生成」ボタンをタップ
- [ ] 音声が再生される

---

## 🔧 トラブルシューティング

### 問題が発生した場合

#### ヘルパースクリプトを使用:
```bash
# 環境チェック
./scripts/ios_deploy_helper.sh check

# 問題を自動修正
./scripts/ios_deploy_helper.sh fix

# ビルドをクリーン
./scripts/ios_deploy_helper.sh clean
```

#### よくある問題:

**Q: デバイスが表示されない**
- A: USBケーブルを抜き差し、デバイスで「信頼」を再度選択

**Q: ビルドエラーが発生**
- A: Product → Clean Build Folder (⇧⌘K) → 再ビルド

**Q: 音声が再生されない**
- A: マナーモード解除、音量確認

---

## 📊 Xcodeコンソールで確認すべきログ

成功時のログ例:
```
[iOS Debug] Platform: iOS
[iOS Debug] ✓ Dictionary exists
[OpenJTalk] Initialized successfully
[InferenceEngineDemo] Audio playback started
```

エラー時のチェックポイント:
```
# フィルタで検索
uPiper
PiperLogger
OpenJTalk
```

---

## 🚀 次のステップ

テストが成功したら:
1. **パフォーマンス測定**: Xcode Instruments でプロファイリング
2. **複数デバイステスト**: 異なるiOSバージョンでテスト
3. **TestFlight準備**: ベータ配布の準備

---

## 📝 詳細ドキュメント

より詳しい情報は以下を参照:
- [iOS実機テストガイド](./ios-device-testing-guide.md)
- [iOS最終ステータスレポート](./ios-final-status-report.md)

---

最終更新: 2025-10-10
所要時間: 約15-20分