# 商用利用可能な音素化ライブラリ調査レポート

## エグゼクティブサマリー

本調査では、商用利用可能（MIT、Apache 2.0、BSD等）なライセンスを持つ音素化ライブラリについて、世界の主要言語を対象に詳細な調査を実施しました。

### 主要な発見事項

1. **汎用ライブラリ**
   - **OpenPhonemizer** (BSD-3-Clause Clear): eSpeak互換のニューラルネットワークベース音素化ライブラリ
   - **DeepPhonemizer** (MIT): Transformerベースの高精度音素化ライブラリ

2. **言語特化型ライブラリ**
   - 多くの言語で商用可能なライセンスのライブラリが限定的
   - GPL v3ライセンスの制約が大きな課題

## 言語別詳細調査結果

### 1. 中国語（北京官話）

#### 利用可能なライブラリ
- **pypinyin** (ライセンス要確認): 中国語文字を拼音に変換
- **g2pC** (ライセンス要確認): CRFベースの文脈考慮型音素化
- **g2pW** (ライセンス要確認): BERTベースの最新音素化ツール

#### 課題
- 多音字（同じ文字で複数の読み方）の処理
- 商用ライセンスの明確な確認が必要

#### 推奨案
- 基本的な変換: pypinyin
- 高精度が必要: g2pW
- ライセンス確認後の採用を推奨

### 2. スペイン語

#### 利用可能なライブラリ
- **OpenPhonemizer** (BSD-3-Clause Clear): 推奨
- **epitran** (ライセンス要確認): マッピングベースのG2P

#### 対応方言
- ヨーロッパスペイン語
- ラテンアメリカスペイン語

#### 推奨案
OpenPhonemizer を第一選択として推奨

### 3. 英語

#### 既存実装
- Phase 2でeSpeak-NG統合予定
- OpenPhonemizer (BSD-3-Clause Clear) も選択肢

#### 推奨案
既存計画通りeSpeak-NG統合を進めつつ、ライセンス問題がある場合はOpenPhonemizerを検討

### 4. ヒンディー語

#### 利用可能なライブラリ
- **IndicXlit** (MIT): AI4Bharatによる21のインド言語対応
- **CharsiuG2P** (MIT): 100言語対応の汎用ライブラリ
- **g2p (roedoejet/g2p)** (MIT): ルールベースのカスタマイズ可能

#### 推奨案
IndicXlitを第一選択として推奨（インド言語に特化）

### 5. アラビア語

#### 課題
- MIT/Apache/BSDライセンスの専用ライブラリが見つからない
- 既存ライブラリはGPLまたはCC-BY-NC

#### 代替案
1. Apache 2.0ライセンスのアラビア語データセットを使用して独自実装
2. OpenPhonemizerの対応状況を確認
3. 商用ライセンスの交渉

### 6. ベンガル語

#### 利用可能なライブラリ
- **bnbphoneticparser** (MIT): バングリッシュ→ベンガル語変換
- **Phonetisaurus** (BSD): 汎用G2Pツールキット

#### 推奨案
bnbphoneticparserを基盤として採用

### 7. ポルトガル語

#### 利用可能なライブラリ
- **OpenPhonemizer** (BSD-3-Clause Clear): 推奨
- ブラジル・ヨーロッパ両方言の対応確認が必要

#### 推奨案
OpenPhonemizerを採用し、両方言の対応を検証

### 8. ロシア語

#### 利用可能なライブラリ
- **russian_g2p** (ライセンス要確認): NSU-AI開発
- **ru_g2p_ipa_bert_large** (Hugging Face): BERTベース
- **gruut** (ライセンス要確認): 多言語対応

#### 推奨案
ライセンス確認後、russian_g2pを第一選択

### 9. 日本語

#### 既存実装
- **OpenJTalk** (修正BSD): Phase 1.10で実装済み
- 高精度な音素化を実現

#### 状態
✅ 実装完了

### 10. パンジャブ語

#### 課題
- 専用ライブラリの情報が限定的
- IndicXlit (MIT) でのサポート可能性

#### 推奨案
IndicXlitの対応状況を確認

### 主要経済圏の言語

#### ドイツ語・フランス語・イタリア語
- **OpenPhonemizer** (BSD-3-Clause Clear): 対応可能性高
- **DeepPhonemizer**: 多言語対応

#### 韓国語
- **g2pK** (ライセンス要確認): Mecab使用の高精度変換
- **KoG2P** (ライセンス要確認): 既存のオープンソース実装

#### オランダ語
- OpenPhonemizer/DeepPhonemizerで対応検討

#### トルコ語・ポーランド語・インドネシア語
- **OpenPhonemizer** (BSD-3-Clause Clear): 第一選択
- 言語別の対応確認が必要

## 技術的考察

### Unity統合の観点

1. **ネイティブライブラリ統合**
   - C/C++実装: P/Invokeで統合可能
   - Python実装: IronPythonまたはプロセス間通信

2. **Pure C#実装の可能性**
   - ルールベース: 実装可能だが精度に課題
   - 辞書ベース: メモリ使用量とのトレードオフ

3. **パフォーマンス考慮事項**
   - リアルタイム処理: キャッシュ機構必須
   - バッチ処理: 非同期実行を推奨

## 実装戦略提案

### フェーズ1: コアライブラリ選定
1. **OpenPhonemizer** (BSD-3-Clause Clear) を基盤として採用
2. 各言語の対応状況を検証
3. 不足言語は個別ライブラリで補完

### フェーズ2: 言語別最適化
1. **高優先度言語**（話者数上位）
   - 中国語: g2pW (ライセンス確認後)
   - ヒンディー語: IndicXlit (MIT)
   - アラビア語: カスタム実装検討

2. **中優先度言語**
   - 韓国語: g2pK (ライセンス確認後)
   - ロシア語: russian_g2p (ライセンス確認後)

### フェーズ3: 統合レイヤー実装
```csharp
public interface IMultilingualPhonemizer
{
    string Language { get; }
    string[] Phonemize(string text);
    bool IsSupported(string languageCode);
}

public class UnifiedPhonemizer
{
    private Dictionary<string, IMultilingualPhonemizer> phonemizers;
    
    public string[] Phonemize(string text, string language)
    {
        if (phonemizers.TryGetValue(language, out var phonemizer))
        {
            return phonemizer.Phonemize(text);
        }
        // フォールバック処理
        return OpenPhonemizerFallback(text, language);
    }
}
```

## リスク管理

### ライセンスリスク
1. 各ライブラリのライセンスを法務確認
2. GPLライブラリは分離実行で対応
3. 商用ライセンス交渉の準備

### 技術リスク
1. 言語別精度のベンチマーク実施
2. フォールバック機構の実装
3. ユーザーによるカスタム辞書対応

## 推奨アクションアイテム

1. **即時実施**
   - OpenPhonemizerの言語対応調査
   - ライセンス不明ライブラリの確認

2. **短期（1-2週間）**
   - 主要言語のプロトタイプ実装
   - パフォーマンステスト

3. **中期（1ヶ月）**
   - 統合レイヤーの設計・実装
   - 言語別最適化

## 結論

商用利用可能な音素化ライブラリは言語によって状況が大きく異なります。OpenPhonemizerを基盤としつつ、言語特化型ライブラリで補完する戦略が現実的です。特に、アラビア語など一部言語では独自実装の検討が必要となる可能性があります。

継続的な調査と、各ライブラリのライセンス確認を並行して進めることを推奨します。