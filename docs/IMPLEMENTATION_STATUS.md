# uPiper 実装状況

## 現在のフェーズ: Phase 3 - 多言語音素化 ✅ 完了 (2025-01-28)

### Phase 1: 基盤実装 ✅
- Unity統合
- 基本的なTTSパイプライン
- ONNXモデルローディング
- 音声生成

### Phase 1.10: OpenJTalk統合 ✅
- ネイティブライブラリビルドシステム
- 日本語音素化
- クロスプラットフォーム対応（Windows/macOS/Linux）
- PUA文字マッピング

### Phase 2: Android対応 ✅
- 全Android ABI対応（arm64-v8a, armeabi-v7a, x86, x86_64）
- OpenJTalkネイティブライブラリのAndroidビルド
- Unity Android統合
- CI/CD統合（GitHub Actions）
- 実機での日本語TTS動作確認

### Phase 3: 多言語音素化 ✅ (2025-01-28)
**スコープ: 日本語と英語のみ**

#### 完了したコンポーネント:
1. **英語音素化**
   - SimpleLTSPhonemizer（純粋なC#実装）
   - 基本的なLetter-to-Soundルール
   - CMU辞書サポート
   - 外部依存なし

2. **混在言語サポート**
   - MixedLanguagePhonemizer
   - 自動言語検出
   - シームレスな日英切り替え
   - 適切な句読点処理

3. **統一インターフェース**
   - UnifiedPhonemizerがメインエントリーポイント
   - 自動検出とバックエンド選択
   - 拡張可能なアーキテクチャ
   - パフォーマンス最適化

4. **エラー耐性**
   - CircuitBreakerパターン完全統合
   - 自動フォールバック機構
   - エラー状態の追跡

5. **テスト**
   - 包括的なテストスイート
   - Unity Test Framework統合
   - 全テスト合格

#### 主要ファイル:
- `Assets/uPiper/Runtime/Core/Phonemizers/UnifiedPhonemizer.cs`
- `Assets/uPiper/Runtime/Core/Phonemizers/MixedLanguagePhonemizer.cs`
- `Assets/uPiper/Runtime/Core/Phonemizers/Backend/SimpleLTSPhonemizer.cs`
- `Assets/uPiper/Runtime/Core/Phonemizers/Backend/OpenJTalkBackendAdapter.cs`

### 精度とパフォーマンス

#### 日本語音素化（OpenJTalk）
- **精度**: 95%以上
- **特徴**: 形態素解析、アクセント情報、辞書ベース
- **例**: "今日は良い天気ですね" → [k y o o w a i i t e N k i d e s u n e]

#### 英語音素化（SimpleLTS）
- **精度**: 60-70%（基本単語）
- **制限**: 不規則動詞未対応、ストレスマークなし、ルールベースのみ
- **例**: "Hello world" → [HH EH L OW  W ER L D]

#### 混在言語処理
- **精度**: 85-90%
- **強み**: 正確な言語境界検出
- **課題**: カタカナ英語の処理

### 次のフェーズ（計画中）

#### Phase 4: 追加言語サポート
- eSpeak-NGオプショナルプラグイン（英語精度向上）
- OpenPhonemizer統合（5言語以上）
- 中国語、韓国語、スペイン語等

#### Phase 5: iOS対応
- Xcodeプロジェクト設定
- Objective-C++ブリッジ
- App Store要件対応

#### Phase 6: WebGL対応
- Emscriptenビルド
- WebAssembly最適化
- ブラウザ互換性

### ライセンス状況
全コンポーネントが商用利用可能なライセンス:
- OpenJTalk: 修正BSD
- SimpleLTS: MIT
- Core uPiper: MIT
- GPLの依存関係なし

### 現在の制限事項
1. 言語: 日本語と英語のみ
2. 英語: 基本的なLTSルール（高度な言語機能なし）
3. 混在テキスト: 明確な言語境界で最良の結果
4. パフォーマンス: 非常に長いテキスト（10,000文字以上）では未最適化

### テスト方法
```bash
# Unity Editorで
1. Window > General > Test Runner
2. PlayModeタブを選択
3. uPiper.Tests.Runtime.Core.Phonemizersのテストを実行
```

### 使用例
```csharp
// 初期化
var phonemizer = new UnifiedPhonemizer();
await phonemizer.InitializeAsync();

// 使用
var result = await phonemizer.PhonemizeAsync("Hello, 世界", "auto");
```

### プロジェクトサイズ削減
2025年1月28日、プロジェクトサイズを約450-500MB削減:
- 2D機能パック削除
- Universal Render Pipeline削除
- 不要なモジュール削除（Physics、VR/XR、Timeline等）
- 開発に必要なパッケージは維持（InputSystem、IDE統合、Android開発ツール）