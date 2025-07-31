# 日本語フォルダ内ドキュメントの言語状況

## 現状サマリ

日本語フォルダ（`docs/ja/`）内の28ファイル中：
- ✅ 日本語: 10ファイル
- ❌ 英語: 18ファイル（翻訳待ち）

## 日本語化済み ✅

1. **README.md** - メインドキュメント
2. **ARCHITECTURE.md** - アーキテクチャ説明
3. **guides/ci-cd/README.md** - CI/CDガイド（一部日本語）
4. **guides/technical/gpu-inference.md** - GPU推論ガイド
5. **guides/technical/il2cpp-compatibility.md** - IL2CPP互換性レポート（一部日本語）
6. **guides/usage/phonemization-guide.md** - 音素化ガイド
7. **guides/usage/test-execution-guide.md** - テスト実行ガイド
8. **phase5-ios/phase5-ios-detailed-implementation-plan.md** - iOS詳細実装計画
9. **phase5-ios/phase5-ios-implementation-plan.md** - iOS実装計画（タイトルは英語、内容は日本語）
10. **phase5-ios/phase5-ios-technical-research.md** - iOS技術調査

## 英語のまま（翻訳必要） ❌

### CI/CD
11. **guides/ci-cd/il2cpp-solutions.md** - IL2CPP CI/CDソリューション

### 実装ガイド
12. **guides/implementation/android/build-settings.md** - Androidビルド設定
13. **guides/implementation/android/implementation-guide.md** - Android実装ガイド
14. **guides/implementation/android/performance-optimization.md** - Androidパフォーマンス最適化
15. **guides/implementation/android/technical-report.md** - Android技術レポート
16. **guides/implementation/asian-language-support.md** - アジア言語サポート
17. **guides/implementation/phonemization-system/implementation-summary.md** - 音素化システム実装サマリ
18. **guides/implementation/phonemization-system/license-evaluation-report.md** - ライセンス評価レポート
19. **guides/implementation/phonemization-system/technical-specification.md** - 技術仕様書

### セットアップ
20. **guides/setup/additional-language-support.md** - 追加言語サポート
21. **guides/setup/cmu-dictionary-setup.md** - CMU辞書セットアップ
22. **guides/setup/flite-build-guide.md** - Fliteビルドガイド

### 技術詳細
23. **guides/technical/README.md** - 技術ドキュメント概要
24. **guides/technical/il2cpp.md** - IL2CPPサポートガイド

## 特記事項

### タイトルは日本語、内容は英語
- guides/implementation/android/build-settings.md
- guides/implementation/android/implementation-guide.md
- guides/implementation/android/performance-optimization.md
- guides/implementation/android/technical-report.md
- guides/implementation/phonemization-system/license-evaluation-report.md
- guides/implementation/phonemization-system/technical-specification.md

これらはタイトルだけ日本語化されているが、本文は英語のままです。

## 翻訳優先順位

### 高優先度 🔥
1. guides/setup/additional-language-support.md - 新しい言語を追加する方法
2. guides/technical/README.md - 技術ドキュメントの索引
3. guides/implementation/android/implementation-guide.md - Android実装手順

### 中優先度 📚
4. guides/setup/cmu-dictionary-setup.md - CMU辞書の設定
5. guides/setup/flite-build-guide.md - Fliteのビルド手順
6. guides/ci-cd/il2cpp-solutions.md - IL2CPP CI/CDの解決策

### 低優先度 📝
その他の技術仕様書や詳細レポート