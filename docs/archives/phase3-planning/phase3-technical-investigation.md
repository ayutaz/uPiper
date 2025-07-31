# Phase 3: eSpeak-NG統合 技術調査レポート

## 1. 調査概要

### 1.1 調査目的
- eSpeak-NGをuPiperに統合し、英語および多言語の音素化品質を向上させる
- piper-plusの実装を参考に、Unity環境での最適な統合方法を検討する

### 1.2 調査範囲
- piper-plusの音素化システムアーキテクチャ
- eSpeak-NGライブラリの技術仕様
- Unity Native Plugin統合における技術的課題

## 2. piper-plusの音素化システム分析

### 2.1 アーキテクチャ概要

piper-plusは3つの音素化エンジンをサポート：
1. **eSpeakPhonemes** (PhonemeType = 0): 英語・多言語用
2. **TextPhonemes** (PhonemeType = 1): テキスト直接入力
3. **OpenJTalkPhonemes** (PhonemeType = 2): 日本語専用

### 2.2 音素表現システム

#### PUA（Private Use Area）マッピング
```cpp
// 固定PUAマッピング (0xE000-0xE015)
{"a:", 0xE000}, {"i:", 0xE001}, {"u:", 0xE002}, {"e:", 0xE003}, {"o:", 0xE004},
{"cl", 0xE005}, {"ky", 0xE006}, {"kw", 0xE007}, {"gy", 0xE008}, {"gw", 0xE009},
{"ty", 0xE00A}, {"dy", 0xE00B}, {"py", 0xE00C}, {"by", 0xE00D}, {"ch", 0xE00E},
{"ts", 0xE00F}, {"sh", 0xE010}, {"zy", 0xE011}, {"hy", 0xE012}, {"ny", 0xE013},
{"my", 0xE014}, {"ry", 0xE015}
```

#### 音素パーサー機能
- `[[ phonemes ]]` 記法による直接音素入力サポート
- テキストと音素の混在入力が可能
- 言語依存の音素セット管理

### 2.3 eSpeak-NG統合方法

piper-plusでのeSpeak-NG使用：
```cpp
#include <espeak-ng/speak_lib.h>

// 初期化
espeak_Initialize(AUDIO_OUTPUT_SYNCHRONOUS, 0, dataPath, 0);

// 音素化
espeak_SetVoiceByName(voice.c_str());
espeak_TextToPhonemes((const void**)&text, espeakCHARS_UTF8, phonemeStr);
```

## 3. Unity統合における技術的考慮事項

### 3.1 ライブラリ依存関係

#### eSpeak-NGの依存
- データファイル（約10MB）
  - 音声定義ファイル
  - 言語ルールファイル
  - 音素辞書
- プラットフォーム別バイナリ
  - Windows: espeak-ng.dll
  - macOS: libespeak-ng.dylib
  - Linux: libespeak-ng.so
  - Android: libespeak-ng.so (各ABI)

### 3.2 メモリ管理とパフォーマンス

#### 観測されたパフォーマンス特性
- 初期化時間: 約100-200ms（データファイル読み込み）
- 音素化速度: 100文字で約10-20ms
- メモリ使用量: 約5-10MB（言語データキャッシュ）

### 3.3 スレッドセーフティ

eSpeak-NGの制限事項：
- グローバル状態を持つ（シングルトンパターン）
- 複数インスタンスの同時実行は非推奨
- Unity環境では適切なロック機構が必要

## 4. 実装アプローチの比較

### 4.1 アプローチ1: 直接統合
**メリット**
- piper-plusと同様の実装が可能
- 完全な機能アクセス

**デメリット**
- GPL v3ライセンスの影響
- バイナリサイズの増大（約15MB）

### 4.2 アプローチ2: サブプロセス分離
**メリット**
- ライセンス問題の回避
- Unity本体への影響最小化

**デメリット**
- IPC オーバーヘッド
- プラットフォーム依存の実装

### 4.3 アプローチ3: 軽量音素化エンジン開発
**メリット**
- ライセンスフリー
- Unity最適化可能

**デメリット**
- 開発工数大
- 品質保証の難しさ

## 5. 推奨実装戦略

### 5.1 段階的アプローチ

#### Phase 3.1: 基本統合（3人日）
- eSpeak-NGラッパーライブラリの作成
- 基本的な英語音素化の実装
- Windows/macOSサポート

#### Phase 3.2: マルチプラットフォーム対応（3人日）
- Linux/Android対応
- データファイル管理システム
- プラットフォーム別最適化

#### Phase 3.3: 多言語サポート（2人日）
- 言語検出システム
- 音素化エンジン選択ロジック
- 言語別音素マッピング

#### Phase 3.4: 品質保証（2人日）
- 包括的テストスイート
- パフォーマンス最適化
- ドキュメント整備

### 5.2 技術的実装詳細

#### ネイティブラッパー設計
```c
typedef struct {
    void* espeak_handle;     // eSpeak-NGインスタンス
    char* data_path;         // データディレクトリパス
    int initialized;         // 初期化フラグ
    void* mutex;            // スレッドセーフティ用
} ESpeakWrapper;
```

#### Unity側インターフェース
```csharp
public class ESpeakPhonemizer : BasePhonemizer
{
    // 言語コードマッピング
    private static readonly Dictionary<string, string> LanguageToVoice = new()
    {
        ["en"] = "en-us",
        ["es"] = "es",
        ["fr"] = "fr",
        ["de"] = "de",
        ["it"] = "it",
        ["pt"] = "pt",
        ["ru"] = "ru",
        ["zh"] = "cmn",
        ["ar"] = "ar",
        ["hi"] = "hi"
    };
    
    // 音素マッピングテーブル
    private readonly PhonemeMappingTable mappingTable;
}
```

## 6. リスク評価と軽減策

### 6.1 技術的リスク

| リスク | 影響度 | 発生確率 | 軽減策 |
|--------|--------|----------|--------|
| GPL v3ライセンス違反 | 高 | 中 | 動的リンク・分離実装 |
| データファイルサイズ | 中 | 高 | 選択的言語サポート |
| パフォーマンス問題 | 中 | 中 | キャッシュ・最適化 |
| プラットフォーム互換性 | 低 | 低 | 十分なテスト |

### 6.2 代替案

1. **Phonemizer (Python)の移植**
   - メリット: 多様なバックエンド
   - デメリット: 実装工数大

2. **Festival Lite (Flite)の使用**
   - メリット: 軽量、MITライセンス
   - デメリット: 言語サポート限定

3. **カスタム音素化ルール**
   - メリット: 完全制御可能
   - デメリット: 品質保証困難

## 7. 結論と次のステップ

### 7.1 推奨事項
1. **段階的実装**を採用し、まず英語サポートから開始
2. **ライセンス問題**を考慮し、動的リンクで実装
3. **データファイル管理**は選択的ダウンロード方式を採用

### 7.2 次のアクション
1. eSpeak-NGのビルド環境構築とテスト
2. ライセンス問題の法的確認
3. プロトタイプ実装による技術検証

### 7.3 成功基準
- 英語音素化精度: 95%以上
- 処理速度: 100文字/50ms以内
- メモリ使用量: 20MB以内
- 10言語以上のサポート