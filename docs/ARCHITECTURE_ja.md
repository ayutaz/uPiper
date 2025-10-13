# uPiper アーキテクチャドキュメント

## 概要

uPiperは、Unity環境でPiper TTSを使用するためのプラグインです。ニューラルネットワークベースの音声合成（VITS）を採用し、高品質な多言語音声合成を実現しています。

## アーキテクチャ概要

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Text Input    │ --> │   Phonemizer     │ --> │  VITS Model     │
│   (Japanese)    │     │   (OpenJTalk)    │     │  (ONNX/Unity)   │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                                 │                         │
                                 ↓                         ↓
                        ┌──────────────────┐     ┌─────────────────┐
                        │ Phoneme Sequence │     │  Audio Output   │
                        │ "k o n n i ch i" │     │    (Unity)      │
                        └──────────────────┘     └─────────────────┘
```

## コンポーネント詳細

### 1. テキスト入力層

- **日本語**: 漢字・ひらがな・カタカナ混じりのテキスト
- **英語**: アルファベットテキスト（Phase 2でeSpeak-NG対応予定）
- **その他言語**: 将来的に中国語、韓国語などをサポート予定

### 2. 音素化層（Phonemizer）

#### OpenJTalk（日本語）
- **役割**: 日本語テキストを音素列に変換
- **実装**: C/C++ネイティブライブラリ（P/Invoke経由で呼び出し）
- **辞書**: mecab-naist-jdic（789,120エントリ）を使用
- **処理フロー**:
  ```
  テキスト → MeCab解析 → NJD処理 → JPCommon → 音素列
  ```

#### 重要な設計判断：音素タイミング
```c
// Phase 1.10では全ての音素に50ms固定の継続時間を設定
result->durations[i] = 0.05f; // Default 50ms duration
```

**理由**:
1. VITSモデルはDuration Predictorを内蔵
2. 入力された継続時間は参考程度に使用される
3. 実際のタイミングはモデルが自動的に最適化
4. HTS Engineとの統合は不要（Piperがニューラル音声合成のため）

### 3. 音素エンコーディング層

#### PUA（Private Use Area）マッピング
複数文字の音素を単一のUnicode文字にマッピング：

```csharp
// 例：「きょう」の処理
"ky" + "o" + "u" → "\ue006" + "o" + "u"
```

**PUAマッピング表**:
- `ky` → `\ue006` （きゃ、きゅ、きょ）
- `ch` → `\ue00e` （ち、ちゃ、ちゅ、ちょ）
- `ts` → `\ue00f` （つ）
- `sh` → `\ue010` （し、しゃ、しゅ、しょ）

### 4. 音声合成層（VITS Model）

#### Unity.InferenceEngine統合
- **モデル形式**: ONNX
- **推論エンジン**: Unity.InferenceEngine（旧Sentis）
- **入力**: 音素ID配列
- **出力**: 音声波形（float配列）

#### VITSアーキテクチャ
```
音素ID → TextEncoder → Duration Predictor → Flow Decoder → 音声波形
         ↓
         潜在表現 → Stochastic Duration Predictor
                    （音素タイミング自動推定）
```

### 5. 音声出力層

- **AudioClipBuilder**: float配列からUnity AudioClipを生成
- **正規化処理**: 音量レベルの自動調整
- **サンプルレート**: 22050Hz（標準）

## データフロー例

### 日本語テキスト「こんにちは」の処理

1. **入力**: "こんにちは"

2. **OpenJTalk音素化**:
   ```
   k o N n i ch i w a
   ```

3. **PUA変換とエンコーディング**:
   ```
   k o N n i ch i w a → k o N n i \ue00e i w a
   → [9, 8, 23, 15, 5, 25, 5, 22, 4]
   ```

4. **VITS推論**:
   - Duration Predictor: 各音素の継続時間を推定
   - Decoder: 音声波形を生成

5. **音声出力**: Unity AudioSourceで再生

## 重要な設計判断の根拠

### 1. HTS Engine非使用の判断

**背景**:
- 従来のOpenJTalkはHTS Engine（HMMベース）で音声合成
- PiperはVITS（ニューラルネット）で音声合成

**決定**:
- OpenJTalkは音素化のみに使用
- HTS Engineは完全に除外
- 結果：軽量化、依存関係の削減

### 2. 音素タイミング簡略化の判断

**調査結果**:
- Piperの実装を確認した結果、HTS Engineを使用していない
- VITSモデルが音素タイミングを自動推定

**決定**:
- 固定50msで十分（モデルが再計算するため）
- 実装の簡略化と保守性の向上

### 3. PUA文字使用の判断

**課題**:
- 日本語には複数文字の音素が存在（"ky", "ch"など）
- Piperモデルは1音素=1文字を期待

**解決策**:
- Unicode PUA領域を使用
- pyopenjtalkと互換性のあるマッピング

## プラットフォームサポート

### サポート済みプラットフォーム
- **Windows**: x64 (Windows 10/11)
- **macOS**: Intel/Apple Silicon (macOS 11+)
- **Linux**: x64 (Ubuntu 20.04+)
- **Android**: arm64-v8a, armeabi-v7a, x86, x86_64 (API 21+)
- **iOS**: arm64 (iOS 11.0+)

### プラットフォーム固有の実装

#### Windows
- ネイティブライブラリ: `openjtalk_wrapper.dll`
- コンパイラ: MSVC 2019+
- Unityバックエンド: Mono/IL2CPP

#### macOS
- ネイティブライブラリ: `openjtalk_wrapper.bundle`
- コンパイラ: Clang
- Universal Binaryサポート

#### Android
- ネイティブライブラリ: `libopenjtalk_wrapper.so`
- NDK: r21+
- アーキテクチャ: arm64-v8a, armeabi-v7a, x86, x86_64

#### iOS
- ネイティブライブラリ: `libopenjtalk_wrapper.a` (`__Internal`経由でリンク)
- Xcode: 14+
- アーキテクチャ: arm64 (iOS 11.0+)

## パフォーマンス特性

### メモリ使用量
- モデル読み込み: ~100MB (VITSモデル)
- 辞書: ~30MB (圧縮済み)
- ランタイム: ~50-200MB (使用状況による)

### 処理速度
- 音素化: 一般的な文章で <10ms
- 推論: ~100-500ms (ハードウェアによる)
- 合計レイテンシー: ほとんどのケースで <1秒

### 最適化戦略
1. **音素キャッシュ**: 頻繁に使用されるテキストをキャッシュ
2. **モデル量子化**: オプションでINT8量子化
3. **GPUアクセラレーション**: Unity AI Inference Engine経由でサポート
4. **ストリーミング**: 長いテキストのチャンク処理

## 拡張ポイント

### カスタム音素化器
`IPhonemizerBackend`インターフェースを実装：
```csharp
public interface IPhonemizerBackend
{
    string Language { get; }
    PhonemeResult Phonemize(string text);
}
```

### 音声モデルサポート
- ONNXモデルを`StreamingAssets/uPiper/Models/`に配置
- `PiperVoiceConfig`で設定

### 言語拡張
1. 言語固有の音素化器を実装
2. 音素からIDへのマッピングを追加
3. 互換性のあるVITSモデルをトレーニングまたは取得

## セキュリティ上の考慮事項

- すべての処理はローカルで実行（クラウド依存なし）
- 個人データの収集なし
- モデルと辞書は読み取り専用
- Unity環境内でサンドボックス実行