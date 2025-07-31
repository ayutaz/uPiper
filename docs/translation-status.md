# ドキュメント翻訳状況

## 翻訳完了 ✅

### ルートレベル
- README.md (言語選択ページ)
- ja/README.md (日本語)
- en/README.md (英語)
- ja/ARCHITECTURE.md (日本語)
- en/ARCHITECTURE.md (英語)

### CI/CD
- ja/guides/ci-cd/README.md (日本語)

### Phase 5 iOS
- ja/phase5-ios/*.md (日本語で作成済み)
- en/phase5-ios/*.md (翻訳待ち)

## 翻訳待ち 🔄

### 日本語化が必要
以下のファイルはja/フォルダに存在しますが、内容が英語のままである可能性があり、日本語翻訳が必要か確認が必要です：

#### 実装ガイド
- ja/guides/implementation/android/*.md (4ファイル)
- ja/guides/implementation/phonemization-system/*.md (3ファイル)
- ja/guides/implementation/asian-language-support.md

#### セットアップ
- ja/guides/setup/*.md (3ファイル)

#### 技術詳細
- ja/guides/technical/*.md (4ファイル)

#### 使用方法
- ja/guides/usage/*.md (2ファイル)

#### CI/CD
- ja/guides/ci-cd/il2cpp-solutions.md

### 英語化が必要
- en/phase5-ios/*.md (3ファイル) - 日本語から英語への翻訳

## 翻訳優先順位

### 高優先度 (基本的な使用に必要)
1. ja/guides/usage/phonemization-guide.md - 音素化ガイド
2. ja/guides/usage/test-execution-guide.md - テスト実行ガイド
3. ja/guides/setup/additional-language-support.md - 追加言語サポート

### 中優先度 (開発者向け)
4. ja/guides/technical/README.md - 技術ドキュメント概要
5. ja/guides/implementation/android/implementation-guide.md - Android実装ガイド
6. ja/guides/ci-cd/il2cpp-solutions.md - IL2CPPソリューション

### 低優先度 (詳細技術情報)
7. その他の技術仕様書
8. パフォーマンス最適化ガイド
9. 詳細な実装レポート

## メンテナンス方針

1. **新規ドキュメント**: 最初から両言語で作成
2. **更新時**: 両言語版を同時に更新
3. **翻訳品質**: 技術用語は統一、読みやすさを重視
4. **レビュー**: ネイティブスピーカーによる確認推奨