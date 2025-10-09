# iOS実機テストガイド

## 前提条件

### 必要なもの
- ✅ macOS搭載のMac
- ✅ Xcode 14.0以降
- ✅ iOS 11.0以降のiPhone/iPad
- ✅ USBケーブル（Lightning または USB-C）
- ✅ Apple ID（無料版でも可）
- ✅ Unity から生成されたXcodeプロジェクト

## ステップ 1: iOS デバイスの準備

### 1.1 開発者モードを有効化（iOS 16以降）
```
設定アプリを開く
↓
プライバシーとセキュリティ
↓
デベロッパモード
↓
オン（再起動が必要）
```

### 1.2 デバイスをMacに接続
1. USBケーブルでiPhoneをMacに接続
2. 「このコンピュータを信頼しますか？」→ **信頼** をタップ
3. デバイスのパスコードを入力

## ステップ 2: Xcode プロジェクトを開く

### 2.1 プロジェクトを開く
```bash
# Unityでビルドしたフォルダに移動
cd [Unity Build フォルダ]

# Xcodeプロジェクトを開く
open Unity-iPhone.xcodeproj
```

### 2.2 プロジェクト設定の確認
1. 左側のナビゲータで **Unity-iPhone** を選択
2. **TARGETS** → **Unity-iPhone** を選択
3. **General** タブを確認

## ステップ 3: 署名設定（Code Signing）

### 3.1 自動署名の設定
1. **Signing & Capabilities** タブを開く
2. **Automatically manage signing** にチェック
3. **Team** ドロップダウンから以下を選択：
   - 無料版: "Your Name (Personal Team)"
   - 有料版: 組織名

### 3.2 Bundle Identifier の設定
```
現在: com.DefaultCompany.uPiper
↓
変更: com.[あなたの名前].uPiper
（例: com.yamada.uPiper）
```

**重要**: Bundle Identifier は一意である必要があります

### 3.3 Apple ID の追加（初回のみ）
もし Team が表示されない場合：
1. Xcode → Settings (⌘,)
2. **Accounts** タブ
3. **+** ボタン → **Apple ID**
4. Apple ID とパスワードでサインイン

## ステップ 4: デバイスの選択とビルド

### 4.1 デバイスを選択
1. Xcode 上部のスキーム選択で **Unity-iPhone** を確認
2. デバイス選択で実機の名前を選択（例: "田中のiPhone"）

### 4.2 ビルドと実行
```
方法1: ショートカットキー
⌘ + R

方法2: メニューから
Product → Run
```

### 4.3 ビルド進行状況
- 初回ビルド: 5-10分程度
- 2回目以降: 1-3分程度

## ステップ 5: 初回実行時の設定

### 5.1 「信頼されていない開発者」エラーの対処
初回実行時、デバイスに以下のエラーが表示されます：
```
「"uPiper"を開けません」
デベロッパが信頼されていません
```

**解決手順**:
1. iPhone の **設定** アプリを開く
2. **一般** → **VPNとデバイス管理**
3. **デベロッパAPP** セクション
4. あなたのApple ID を選択
5. **"[あなたの名前]"を信頼** をタップ
6. ポップアップで **信頼** を選択

### 5.2 アプリの起動
ホーム画面から uPiper アプリをタップして起動

## ステップ 6: 動作確認

### 6.1 基本動作テスト
```
チェックリスト:
□ アプリが正常に起動する
□ InferenceEngineDemo シーンが表示される
□ UIが正しくレンダリングされる
□ 日本語フォントが表示される
```

### 6.2 TTS機能テスト
```
1. モデル選択: ja_JP-test-medium
2. テキスト入力: こんにちは
3. 「生成」ボタンをタップ
4. 音声が再生されることを確認
```

### 6.3 デバッグログの確認
Xcode のコンソール（下部パネル）で以下を確認：
```
[iOS Debug] === iOS Setup Debug ===
[iOS Debug] Platform: iOS
[iOS Debug] Dictionary exists: ✓
[OpenJTalk] Initialized successfully
```

## トラブルシューティング

### エラー: "Could not launch"
**原因**: プロビジョニングプロファイルの問題
**解決**:
```bash
# Xcodeでクリーンビルド
Product → Clean Build Folder (⇧⌘K)
# 再ビルド
Product → Run (⌘R)
```

### エラー: "No eligible devices"
**原因**: デバイスのiOSバージョンが古い
**解決**:
- Deployment Target を iOS 11.0 に変更
- General → Deployment Info → iOS 11.0

### エラー: "Failed to verify code signature"
**原因**: 証明書の問題
**解決**:
1. デバイスを再起動
2. Xcode → Product → Clean Build Folder
3. DerivedData を削除:
```bash
rm -rf ~/Library/Developer/Xcode/DerivedData
```

### 音声が再生されない
**原因**: AudioSession の問題
**解決**:
1. デバイスの音量を確認
2. マナーモードを解除
3. Bluetooth デバイスを切断

### 日本語が文字化けする
**原因**: フォントまたはエンコーディングの問題
**解決**:
- StreamingAssets の辞書ファイルを確認
- Info.plist に日本語ロケール追加

## デバッグ Tips

### 1. コンソールログ
```bash
# Xcodeコンソールでフィルタリング
フィルタバーに入力: uPiper
または: PiperLogger
```

### 2. パフォーマンス監視
- Xcode → Debug Navigator (⌘7)
- CPU, Memory, Energy Impact を確認

### 3. デバイスログ
```
Window → Devices and Simulators (⇧⌘2)
デバイスを選択 → View Device Logs
```

### 4. 実機でのクラッシュログ
```
設定 → プライバシーとセキュリティ → 解析と改善 → 解析データ
```

## パフォーマンス最適化

### メモリ使用量の確認
- 辞書ファイル: 約100MB
- モデルファイル: 約30MB
- 推奨空きメモリ: 200MB以上

### 音声生成時間の目標
- 日本語5文字: < 100ms
- 日本語10文字: < 150ms
- 日本語20文字: < 200ms

## 次のステップ

### TestFlight配布（ベータテスト）
1. Archive ビルドを作成
2. App Store Connect にアップロード
3. TestFlight でテスターに配布

### App Store申請準備
1. アプリアイコン設定
2. スクリーンショット準備
3. アプリ説明文作成
4. プライバシーポリシー準備

## サポート情報

### 必要な場合の連絡先
- Unity Forum: https://forum.unity.com
- Apple Developer Forums: https://developer.apple.com/forums
- uPiper Issues: [プロジェクトのGitHub Issues]

### 参考リンク
- [Apple Developer Documentation](https://developer.apple.com/documentation/)
- [Unity iOS Build Settings](https://docs.unity3d.com/Manual/class-PlayerSettingsiOS.html)
- [Xcode Help](https://help.apple.com/xcode/mac/current/)

---

最終更新: 2025-10-10