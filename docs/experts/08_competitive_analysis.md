# uPiper 競合分析レポート - 2026年3月版

## 競合比較表

### クラウド型

| サービス | 価格/百万文字 | レイテンシ | 言語数 | オフライン |
|---------|-------------|-----------|--------|-----------|
| ElevenLabs | $180-240 | 75ms | 70+ | No |
| OpenAI TTS | $15-30 | ~200ms | 13+ | No |
| Google Cloud TTS | 従量課金 | ~150ms | 40+ | No |
| Azure Speech | 従量課金 | ~150ms | 100+ | No |
| Amazon Polly | 従量課金 | ~100ms | 30+ | No |

### オフライン/OSS型

| ソリューション | ライセンス | 日本語 | Unity統合 | 特徴 |
|-------------|----------|--------|-----------|------|
| **uPiper** | Apache 2.0 | Prosody対応 | ネイティブ | VITS、WebGL対応 |
| Piper.unity | MIT | 基本対応 | Sentis | Prosody未対応 |
| Coqui TTS | MPL 2.0 | 基本対応 | 要カスタム | 音声クローニング |
| Kokoro-TTS | Apache 2.0 | - | 要カスタム | 超軽量82M |
| VOICEVOX | 無料 | 高品質 | 外部プロセス | 40+キャラクター |

## uPiperの差別化ポイント

| 要素 | uPiper | 競合 |
|------|--------|------|
| 完全オフライン | GDPR/HIPAA対応容易 | クラウド型はコンプライアンスコスト高 |
| Unity専用設計 | Sentis統合、WebGL対応 | 他OSSは統合未整備 |
| 日本語Prosody | dot-net-g2p + A1/A2/A3 | Piper.unityは未対応 |
| カスタム辞書 | 技術用語最適化 | 他OSSには類似機能なし |
| コストゼロ | 年間$0 | ElevenLabsは年$216,000 |

## 日本市場の特殊性

| 要素 | uPiper | VOICEVOX | CoeFont |
|------|--------|---------|---------|
| Unity統合 | ネイティブ | 外部プロセス | REST API |
| WebGL対応 | あり | なし | あり |
| 音声数 | 3モデル | 40+ | 10,000+ |
| コスト | 無料 | 無料 | 従量課金 |

## 戦略提案

### 短期（2026年）
1. Unity Asset Store公開（無料版+有料版$19.99-$49.99）
2. 英語ドキュメント整備
3. パフォーマンスベンチマーク公開

### 中期（2026-2027年）
1. 音声クローニング機能
2. リアルタイムストリーミング
3. uPiperモデルマーケットプレイス

### 長期（2027年以降）
1. マルチモーダルAI統合（リップシンク）
2. 企業向けサービス（SLA、カスタムサポート）
3. 100+言語対応

## Sources

- [Best TTS APIs for Real-Time Voice Agents (2026 Benchmarks)](https://inworld.ai/resources/best-voice-ai-tts-apis-for-real-time-voice-agents-2026-benchmarks)
- [Piper.unity - Unity Discussions](https://discussions.unity.com/t/piper-unity-open-fast-and-high-quality-tts/337243)
- [VOICEVOX 公式サイト](https://voicevox.hiroshiba.jp/)
