# uPiper TTS技術調査レポート - 2026年3月版

## 1. 最新TTS技術のトレンド

### VITS2（2023年発表）

**概要**
VITS2は元祖VITSの改良版で、以下の主要な改善を含む：
- 敵対的学習によるStochastic Duration Predictor
- Transformer blockを利用した正規化フロー
- Speaker-conditioned text encoder（マルチスピーカー特性のモデリング改善）
- Mel spectrogram posterior encoder

**ベンチマーク**
UTMOS 3.4を記録し、Matcha-TTSより高い音質スコアを達成。

**現状の課題**
- uPiperは初代VITSアーキテクチャベースのpiper-plusを使用しており、VITS2の改善を享受できていない
- Speaker-conditioned encoderによるマルチスピーカー品質向上の恩恵がない

**推奨する改善**
- piper-plusコミュニティまたは独自にVITS2ベースのモデル訓練を検討
- VITS2のONNXエクスポート可能性の検証

**実装難易度**: 高
**期待されるインパクト**: 中（音質向上10-15%、特にマルチスピーカー環境で効果）

---

### StyleTTS2（2024年）

**概要**
StyleTTS2は**人間レベルのTTS**を目指したモデルで、以下の特徴を持つ：
- スタイルベクトルによる音声コンテンツとスタイルの分離
- Style Diffusion Denoiser + Prosodic Style/Text Encoder
- Large Speech Language Modelsとの敵対的訓練
- NaturalSpeechに対してCMOS +1.07を達成

**現状の課題**
- uPiperは固定Prosodyパラメータ（A1/A2/A3）のみで、スタイル・感情の動的制御が不可

**推奨する改善**
- StyleTTS2アーキテクチャの調査とONNX互換性検証
- スタイルベクトルAPIの設計（感情パラメータ: 喜び/悲しみ/怒り等）

**実装難易度**: 高
**期待されるインパクト**: 高（ゲーム/VTuber用途で感情表現が劇的に向上）

---

### Matcha-TTS

**概要**
Flow Matchingベースの拡散モデルで、以下の特性：
- VITS2より明瞭性とイントネーションで優位
- VITS2より若干低速（UTMOS 3.4以下）
- スタイルベクトル非使用（均一な音声に特化）

**推奨する改善**
- 明瞭性重視用途（ナレーション、教育コンテンツ）向けのオプションモデルとして検討

**実装難易度**: 中
**期待されるインパクト**: 低（特定用途向け）

---

### Kokoro TTS（2026年最新）

**概要**
**StyleTTS2ベース**の超軽量モデル（82Mパラメータ）：
- 5-15倍大きいモデルを上回る品質と速度
- 処理時間：**0.3秒未満**（あらゆるテキスト長）
- 多言語対応（英語、フランス語、韓国語、**日本語**、中国語）
- Apache 2.0ライセンス（商用利用可）

**推奨する改善**
- **最優先候補**：Kokoroモデルの日本語版をONNXエクスポートして統合
- 現行モデルとのベンチマーク比較（品質・速度・モデルサイズ）

**実装難易度**: 中
**期待されるインパクト**: 高（速度3-5倍改善、モデルサイズ削減、Apache 2.0ライセンス）

---

### F5-TTS（2026年）

**概要**
非自己回帰型Flow Matching + Diffusion Transformer（DiT）：
- **Voice Cloning**に特化（現在最高の多話者モデル）
- 処理時間：7秒未満
- 音声品質で他の多話者モデルを凌駕

**推奨する改善**
- ユーザー音声からのカスタムボイス生成機能の追加（長期ロードマップ）

**実装難易度**: 高
**期待されるインパクト**: 高（VTuber/ゲーム用途で革新的、ただし実装コスト大）

---

## 2. VITSからの進化パス

### 推奨移行戦略

| ステップ | モデル | 理由 | タイムライン |
|---------|--------|------|------------|
| **Phase 1** | Kokoro TTS | 軽量・高速・Apache 2.0・日本語対応 | 3-6ヶ月 |
| **Phase 2** | VITS2 | VITSからの自然な進化、音質向上 | 6-12ヶ月 |
| **Phase 3** | StyleTTS2ベース | 感情・スタイル制御の本格実装 | 12-18ヶ月 |
| **Phase 4** | F5-TTS統合 | ボイスクローニング機能追加 | 18-24ヶ月 |

---

## 3. リアルタイムTTSの最新動向

### レイテンシベンチマーク（2026年）

| システム | TTFB（Time To First Byte） | 用途 |
|---------|---------------------------|------|
| Speechmatics TTS | **150ms** | リアルタイム会話 |
| Inworld TTS | **<200ms** | ボイスエージェント |
| MARS8-Flash | **100ms**（Blackwell GPU） | 高性能インフラ |
| CosyVoice2-0.5B | **150ms** | ストリーミング |
| Kyutai Pocket TTS | CPU動作・リアルタイム | エッジデバイス |
| XTTS-v2 | **<150ms**（PyTorch+Consumer GPU） | ローカル実行 |

### 推奨する改善
1. **チャンクベースストリーミング**: 音素単位/文単位での逐次生成・再生
2. **TTFB測定**: 初回バイト出力までの時間を計測・最適化
3. **キャッシング強化**: 頻出フレーズのプリコンパイル

**実装難易度**: 中
**期待されるインパクト**: 高（VRChat/リアルタイム会話用途で必須）

---

## 4. 音声品質向上技術

### BigVGAN v2（2026年）
- **3倍高速化**（初代BigVGANより）
- 人間の可聴域全体をカバー
- ゼロショット波形生成で最先端品質

**推奨する改善**
- piper-plusモデルをBigVGAN v2ベースで再訓練
- または既存VITS/VITS2モデルのボコーダー部分のみBigVGANに置換

**実装難易度**: 高（モデル訓練が必要）
**期待されるインパクト**: 中（音質向上15-20%、特に高周波の自然さ）

---

## 5. 感情・スタイル制御

### 最新技術（2026年）

#### Fish Audio S2
- **ワードレベル感情制御**: `[laugh]`, `[whispers]`, `[super happy]`等のタグ
- フレーズ単位の精密制御

#### ECE-TTS（Easy-Control Emotion TTS）
- **ゼロショット感情制御**
- Valence/Arousal/Dominance（VAD）値でベクトル演算制御

### 推奨する改善
1. **短期**: 現行モデルで話速・ピッチシフトパラメータ追加（Sentis後処理）
2. **中期**: VADベースの感情ベクトルAPI設計
3. **長期**: タグベース制御実装

**実装難易度**: 低（短期）/ 中（中期）/ 高（長期）
**期待されるインパクト**: 高（キャラクターTTS、感情的ナレーション用途で不可欠）

---

## 6. ONNX Runtime / Sentis互換性

### 重要な互換性問題
- **Sentisのオペレータ制限**: `If`オペレータ非対応等
- ONNX Runtimeで動作してもSentisでは失敗するケースあり

### 推奨する改善
1. 新モデル導入時のONNX Opsetバージョン確認 + Sentis互換性テスト
2. ONNXSimplifierで事前にIf/Loop等を除去
3. ハイブリッド戦略: 主要プラットフォームはSentis、実験的モデルはONNX Runtime Native Plugin

**実装難易度**: 中
**期待されるインパクト**: 高（新モデル採用の柔軟性向上）

---

## 優先順位マトリクス

| 改善項目 | 難易度 | インパクト | 優先度 | 推奨期間 |
|---------|-------|-----------|--------|---------|
| **Kokoro TTS統合** | 中 | 高 | **最優先** | 3-6ヶ月 |
| **ストリーミング合成** | 中 | 高 | **高** | 3-6ヶ月 |
| **話速・ピッチ制御** | 低 | 中 | 高 | 1-3ヶ月 |
| **VITS2移行** | 高 | 中 | 中 | 6-12ヶ月 |
| **BigVGAN v2ボコーダー** | 高 | 中 | 中 | 6-12ヶ月 |
| **VAD感情制御** | 中 | 高 | 中 | 6-9ヶ月 |
| **StyleTTS2統合** | 高 | 高 | 中 | 12-18ヶ月 |
| **F5-TTSボイスクローニング** | 高 | 高 | 低 | 18-24ヶ月 |

---

## Sources

- [VITS2: Improving Quality and Efficiency of Single-Stage Text-to-Speech](https://arxiv.org/abs/2307.16430)
- [StyleTTS 2: Towards Human-Level Text-to-Speech](https://pmc.ncbi.nlm.nih.gov/articles/PMC11759097/)
- [Kokoro TTS: Advanced AI Text-to-Speech Model](https://kokorottsai.com/)
- [Best TTS APIs for Real-Time Voice Agents (2026 Benchmarks)](https://inworld.ai/resources/best-voice-ai-tts-apis-for-real-time-voice-agents-2026-benchmarks)
- [BigVGAN: A Universal Neural Vocoder](https://research.nvidia.com/labs/adlr/projects/bigvgan/)
- [ECE-TTS: A Zero-Shot Emotion Text-to-Speech Model](https://www.mdpi.com/2076-3417/15/9/5108)
- [Jets TTS on Sentis - Unity Discussions](https://discussions.unity.com/t/jets-tts-on-sentis-ai-inference-engine/1682414)
