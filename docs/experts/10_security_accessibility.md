# uPiper セキュリティ・アクセシビリティレポート - 2026年3月版

## 最優先: プライバシー方針の明記
- README/ドキュメントに「完全オフライン動作、データ送信なし」を明記
- GDPRコンプライアンス声明追加
- **難易度**: 低 / **インパクト**: 高

## モデルライセンス文書化
- 各ONNXモデルのライセンス・商用利用可否を明確化
- THIRD-PARTY-NOTICESにモデルライセンス追加
- LICENSE FAQ作成
- **難易度**: 中 / **インパクト**: 高

## 入力サニタイゼーション
- テキスト入力の文字数制限（MaxInputLength: 5000）
- 制御文字除去
- レート制限
- **難易度**: 低-中 / **インパクト**: 中

## アクセシビリティ規格対応

### WCAG 2.2 AA
- 音声制御機能（再生/一時停止/音量）
- テキスト代替提供
- **難易度**: 中 / **インパクト**: 高

### Xbox Accessibility Guidelines
- XAG 119: テキストチャットTTS変換
- XAG 106/107: 音声ナレーション速度・ピッチ調整
- **難易度**: 中-高 / **インパクト**: 高

### ゲームアクセシビリティ
- Unity Accessibility Plugin統合
- 音声タイプ別ボリューム調整
- **難易度**: 中 / **インパクト**: 高

## ONNXモデルセキュリティ
- SHA-256ハッシュによるモデル整合性検証
- 署名付きモデル配布
- **難易度**: 中 / **インパクト**: 中

## WebGLセキュリティ
- IndexedDBデータの暗号化検討
- Content Security Policy実装
- Subresource Integrity
- **難易度**: 中 / **インパクト**: 中

## コンテンツ安全性
- 倫理的利用ガイドライン（Responsible AI Use Policy）
- 透明性マーキング（不可聴ウォーターマーク、オプション機能）
- **難易度**: 低（ガイドライン）-高（ウォーターマーク） / **インパクト**: 中

## 法規制対応
- Section 508（米国）: WCAG 2.0 AA準拠確認
- EAA 2025（EU）: EN 301 549適合確認
- 障害者差別解消法（日本）: 合理的配慮としてのTTS位置づけ
- **難易度**: 低-中 / **インパクト**: 中

## ロードマップ

### 即座（文書化のみ）
1. プライバシー方針追加
2. モデルライセンステーブル作成
3. 倫理的利用ガイドライン策定

### 短期（1-3ヶ月）
4. 入力サニタイゼーション
5. WCAG音声制御機能
6. LICENSE FAQ

### 中期（3-6ヶ月）
7. Unity Accessibility Plugin統合
8. ONNXモデル整合性検証
9. WCAG 2.2 AA準拠テスト

### 長期（6ヶ月以上）
10. Xbox Accessibility Guidelines完全準拠
11. 合成音声ウォーターマーク
12. 多言語モデル拡充

## Sources

- [WCAG 2.2 Complete Guide](https://www.allaccessible.org/blog/wcag-22-complete-guide-2025)
- [Xbox Accessibility Guidelines](https://learn.microsoft.com/en-us/gaming/accessibility/guidelines)
- [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/)
- [Local Text-to-Speech with Cloud Quality](https://picovoice.ai/blog/local-text-to-speech-with-cloud-quality/)
