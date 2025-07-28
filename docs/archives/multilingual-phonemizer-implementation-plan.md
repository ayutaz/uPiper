# 多言語音素化実装計画書

## 概要

本文書は、uPiperプロジェクトにおける多言語音素化機能の実装計画をまとめたものです。商用利用可能なライセンス（MIT、Apache 2.0、BSD系）のライブラリを使用し、世界の主要言語に対応する音素化システムを構築します。

## 対応言語と優先順位

### Tier 1: 最優先言語（話者数上位）

| 順位 | 言語 | 話者数 | 推奨ライブラリ | ライセンス | 実装状態 |
|-----|------|--------|----------------|------------|----------|
| 1 | 中国語（北京官話） | 9.2億人 | pypinyin | MIT | 基本実装済み、完全移植予定 |
| 2 | スペイン語 | 5.5億人 | OpenPhonemizer | BSD-3-Clause Clear | 実装予定 |
| 3 | 英語 | 3.8億人 | Flite | MIT-CMU | 実装予定 |
| 4 | ヒンディー語 | 3.4億人 | IndicXlit | MIT | 実装予定 |
| 5 | ベンガル語 | 2.7億人 | bnbphoneticparser | MIT | 実装予定 |

### Tier 2: 重要言語（経済圏・話者数考慮）

| 言語 | 重要性 | 推奨ライブラリ | ライセンス | 実装状態 |
|------|--------|----------------|------------|----------|
| ポルトガル語 | 2.6億人 | OpenPhonemizer | BSD-3-Clause Clear | 実装予定 |
| ロシア語 | 2.6億人 | 調査中 | - | 検討中 |
| 日本語 | 1.3億人 | OpenJTalk | 修正BSD | ✅ 実装済み |
| ドイツ語 | EU経済圏 | OpenPhonemizer | BSD-3-Clause Clear | 実装予定 |
| フランス語 | EU・アフリカ | OpenPhonemizer | BSD-3-Clause Clear | 実装予定 |
| 韓国語 | アジア経済圏 | g2pK（改善） | 要確認 | 基本実装済み |
| イタリア語 | EU | OpenPhonemizer | BSD-3-Clause Clear | 実装予定 |

### 除外言語

- アラビア語：商用利用可能なライブラリが見つからないため、現時点では対応を見送り

## 技術アーキテクチャ

### システム構成

```
uPiper/
├── Assets/
│   └── uPiper/
│       ├── Runtime/
│       │   └── Core/
│       │       └── Phonemizers/
│       │           ├── Multilingual/          # 多言語対応コア
│       │           │   ├── PhonemizerRegistry.cs
│       │           │   └── UnifiedPhonemizer.cs
│       │           ├── Backend/               # 言語別実装
│       │           │   ├── OpenPhonemizerBackend.cs
│       │           │   ├── FlitePhonemizer.cs
│       │           │   ├── ChinesePhonemizer.cs
│       │           │   ├── IndicXlitPhonemizer.cs
│       │           │   └── ...
│       │           └── Licenses/              # ライセンス管理
│       │               └── LicenseManager.cs
│       └── Licenses/                          # ライセンスファイル
│           ├── MIT-LICENSE.txt
│           ├── Apache-2.0-LICENSE.txt
│           ├── BSD-3-Clause-LICENSE.txt
│           └── THIRD-PARTY-NOTICES.txt
└── NativePlugins/                             # ネイティブライブラリ
    ├── OpenPhonemizer/
    └── Flite/
```

### 統一インターフェース

```csharp
public interface IMultilingualPhonemizer
{
    string Name { get; }
    string Version { get; }
    string License { get; }
    string[] SupportedLanguages { get; }
    
    Task<PhonemeResult> PhonemizeAsync(string text, string language, CancellationToken cancellationToken = default);
    bool IsLanguageSupported(string languageCode);
    string GetLicenseText();
}
```

## ライブラリ別実装詳細

### 1. OpenPhonemizer（多言語対応）

**対応言語**: スペイン語、ポルトガル語、ドイツ語、フランス語、イタリア語、オランダ語等

**技術仕様**:
- ニューラルネットワークベース（DeepPhonemizer）
- eSpeak-NG互換の出力形式
- C++実装、P/Invoke経由で統合

**実装方法**:
1. ソースコードのビルド（Windows/macOS/Linux）
2. Unity向けラッパーAPI作成
3. 非同期処理対応

### 2. Flite（英語）

**技術仕様**:
- Festival Liteの軽量版
- 高速処理（リアルタイムの70倍速）
- メモリ使用量 < 1MB

**実装方法**:
1. CMU辞書との連携
2. Letter-to-Sound (LTS) ルールの活用
3. OpenJTalkと同様のネイティブプラグイン構成

### 3. pypinyin（中国語）

**技術仕様**:
- Pure Python実装
- 多音字対応
- フレーズベースの音素選択

**実装方法**:
1. コアアルゴリズムのC#移植
2. 辞書データの最適化（圧縮・バイナリ化）
3. メモリ効率的な実装

### 4. IndicXlit（ヒンディー語・インド系言語）

**技術仕様**:
- AI4Bharatプロジェクト
- 21のインド言語対応
- Transformerベース

**実装方法**:
1. Python APIラッパー作成
2. またはONNXモデルとして統合
3. バッチ処理対応

## 実装フェーズ

### Phase 3.1: 基盤整備（1週間）
- [x] ライセンスファイルの整理
- [ ] 統一インターフェースの実装
- [ ] PhonemizerRegistryの構築
- [ ] LicenseManagerの実装

### Phase 3.2: OpenPhonemizer統合（1週間）
- [ ] ソースコード取得とビルド環境構築
- [ ] ネイティブライブラリビルド
- [ ] P/Invokeインターフェース実装
- [ ] 5言語以上の動作確認

### Phase 3.3: 個別言語実装（2-3週間）
- [ ] Flite統合（英語）
- [ ] pypinyin完全移植（中国語）
- [ ] IndicXlit統合（ヒンディー語）
- [ ] bnbphoneticparser移植（ベンガル語）

### Phase 3.4: 品質保証（1週間）
- [ ] 各言語のベンチマークテスト
- [ ] メモリ使用量の最適化
- [ ] エラーハンドリングの統一

## ライセンス管理

### ライセンス表示要件

1. **MIT/Apache 2.0ライセンス**
   - 著作権表示
   - ライセンス全文の包含

2. **BSDライセンス（3条項）**
   - 著作権表示
   - ライセンス全文の包含
   - 宣伝での名前使用禁止

3. **統合通知ファイル**
   - `THIRD-PARTY-NOTICES.txt`に全ライブラリのライセンスを記載
   - 自動生成スクリプトで管理

### コンプライアンス対応

```csharp
// 起動時のライセンス確認
public class LicenseCompliance : MonoBehaviour
{
    void Start()
    {
        #if UNITY_EDITOR
        LicenseManager.ValidateLicenses();
        LicenseManager.GenerateNotices();
        #endif
    }
}
```

## パフォーマンス目標

| 指標 | 目標値 | 備考 |
|------|--------|------|
| 音素化速度 | < 100ms/文 | キャッシュ無効時 |
| メモリ使用量 | < 100MB | 全言語合計 |
| 初期化時間 | < 3秒 | 全言語ロード |
| 精度 | > 90% | 各言語平均 |

## リスクと対策

### 技術的リスク

1. **ライセンス確認の遅延**
   - 対策：並行して基本実装を進める
   - 代替ライブラリの調査

2. **メモリ使用量の増大**
   - 対策：選択的言語ロード機能
   - 辞書データの圧縮

3. **プラットフォーム互換性**
   - 対策：各プラットフォームでのCI/CDテスト
   - フォールバック実装の準備

## 今後の拡張計画

1. **追加言語サポート**
   - パンジャブ語（IndicXlit経由）
   - トルコ語、ポーランド語（OpenPhonemizer）
   - インドネシア語、マレー語

2. **品質向上**
   - 機械学習モデルの更新
   - ユーザーフィードバックによる改善
   - 方言対応の拡充

3. **パフォーマンス最適化**
   - GPU加速（可能な場合）
   - ストリーミング処理
   - エッジデバイス対応

## 参考資料

- [OpenPhonemizer GitHub](https://github.com/NeuralVox/OpenPhonemizer)
- [Flite Documentation](http://www.festvox.org/flite/)
- [pypinyin Repository](https://github.com/mozillazg/python-pinyin)
- [IndicXlit Project](https://github.com/AI4Bharat/IndicXlit)

## 更新履歴

- 2024-01-XX: 初版作成
- 2024-01-XX: アラビア語を除外、BSD系ライセンスを含める方針に変更