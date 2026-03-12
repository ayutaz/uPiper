# 音素化システム

## 概要

uPiperの音素化システムは、テキストを音素（phonemes）に変換し、VITSモデルによる音声合成を可能にする。すべてのコアコンポーネントはMIT/BSDライセンスで構成されており、GPL依存はない。

## アーキテクチャ

### データフロー

```
テキスト入力
    |
カスタム辞書による前処理 (CustomDictionary)
    |
Phonemizer (テキスト -> 音素変換)
    - 日本語: DotNetG2PPhonemizer (dot-net-g2p, MeCab辞書)
    - 英語: FliteLTSPhonemizer (Flite LTS, 純粋C#)
    |
音素エンコーディング (PhonemeEncoder)
    - PUAモデル: Unicode Private Use Area文字
    - IPAモデル: 国際音声記号
    |
VITS推論 (ONNX via Unity.InferenceEngine)
    |
AudioClip出力 (22050Hz, float32)
```

### レイヤー構造

```
Unity Application Layer (PiperTTS)
    |
    +-- 日本語: DotNetG2PPhonemizer (直接呼び出し)
    |     - dot-net-g2p ライブラリ (MeCab辞書)
    |     - Prosody対応 (PhonemizeWithProsody)
    |     - カスタム辞書 (CustomDictionary)
    |
    +-- 英語: UnifiedPhonemizer -> FliteLTSPhonemizerBackend
          - Flite LTS エンジン (純粋C#)
          - CMU辞書 (130,000+ 単語)
```

### 主要コンポーネント

| コンポーネント | パス | ライセンス | 役割 |
|--------------|------|-----------|------|
| `DotNetG2PPhonemizer` | `Implementations/` | MIT | 日本語G2P (dot-net-g2p) |
| `FliteLTSPhonemizer` | `Backend/Flite/` | BSD | 英語G2P (Flite LTS) |
| `IPhonemizerBackend` | `Backend/` | MIT | バックエンド抽象インターフェース |
| `PhonemizerBackendBase` | `Backend/` | MIT | バックエンド共通基底クラス |
| `CustomDictionary` | `Phonemizers/` | MIT | カスタム辞書 (技術用語/固有名詞) |
| `UnifiedPhonemizer` | `Phonemizers/` | MIT | 英語バックエンド統合 |
| `PhonemeEncoder` | `Core/` | MIT | 音素 -> ID変換 (PUA/IPA自動判定) |

## 日本語音素化 (DotNetG2PPhonemizer)

[dot-net-g2p](https://github.com/ayutaz/dot-net-g2p) ライブラリを使用した純粋C#実装。MeCab辞書による形態素解析を行い、日本語テキストを音素に変換する。

### 主な機能

- **形態素解析**: MeCab辞書による高精度な日本語解析
- **Prosody対応**: アクセント情報 (A1/A2/A3) の取得
- **カスタム辞書**: 技術用語・固有名詞の読み変換 (例: "Docker" -> "ドッカー")
- **WebGL対応**: `UNITY_WEBGL` 条件付きコンパイルによる非同期ローディング

### Prosodyパラメータ

Prosody対応モデル (tsukuyomi-chan等) で使用:

| パラメータ | 説明 |
|-----------|------|
| A1 (ProsodyA1) | アクセント句内でのモーラ位置 (0始まり) |
| A2 (ProsodyA2) | アクセント句内のアクセント核位置 |
| A3 (ProsodyA3) | 呼気段落内でのアクセント句位置 |

## 英語音素化 (FliteLTSPhonemizer)

Carnegie Mellon University の Flite (Festival Lite) をベースとした純粋C#実装。Letter-to-Sound (LTS) ルールとCMU辞書を組み合わせて英語テキストを音素に変換する。

### 主な機能

- **CMU辞書**: 130,000+ 単語の発音辞書 (パブリックドメイン)
- **LTSエンジン**: 未知語に対するルールベース音素推定
- **ARPABET -> IPA変換**: Piperモデル用の音素表現変換
- **ネイティブ依存なし**: 純粋C#による完全な自己完結型実装

## ライセンス情報

| コンポーネント | ライセンス | 備考 |
|--------------|-----------|------|
| uPiper音素化コア | MIT | 自社実装 |
| dot-net-g2p | MIT | 日本語G2P |
| MeCab辞書 | BSD/LGPL | 形態素解析辞書 |
| Flite LTS | BSD | CMU由来の英語音素化 |
| CMU Pronouncing Dictionary | パブリックドメイン | 英語発音辞書 |

GPL依存コンポーネント (eSpeak-NG等) は使用していない。

## 音素エンコーディング

Piperモデルには2種類の音素表現がある:

| モデルタイプ | 音素表現 | 対応モデル |
|------------|---------|-----------|
| PUA (Private Use Area) | Unicode私用領域文字 | ja_JP-test-medium |
| IPA (International Phonetic Alphabet) | 国際音声記号 | tsukuyomi-chan |

`PhonemeEncoder` はモデルの `phoneme_id_map` を検査し、IPA文字 (`ɕ` 等) の有無で自動判定する。

### 主要な音素マッピング

| G2P出力 | PUA文字 | IPA音素 |
|---------|---------|---------|
| `ch` (ち) | `\ue00e` | `tɕ` |
| `ts` (つ) | `\ue00f` | `ts` |
| `sh` (し) | `\ue010` | `ɕ` |
| `cl` (っ) | `\ue005` | `q` |
| `N` (ん) | `N` | `ɴ` |

## ディレクトリ構造

```
Assets/uPiper/Runtime/Core/Phonemizers/
    Backend/
        Flite/              # Flite LTS エンジン (BSD)
        RuleBased/          # ルールベース音素化・CMU辞書
        IPhonemizerBackend.cs
        PhonemizerBackendBase.cs
    Implementations/
        DotNetG2PPhonemizer.cs  # 日本語G2P (dot-net-g2p)
    Cache/                  # LRUキャッシュ
    Threading/              # スレッドセーフプール
    ErrorHandling/          # サーキットブレーカー・フォールバック
    CustomDictionary.cs     # カスタム辞書
    UnifiedPhonemizer.cs    # 英語バックエンド統合
```