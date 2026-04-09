# Model License Information

## multilingual-test-medium.onnx

| 項目 | 内容 |
|------|------|
| **ライセンス** | Apache-2.0 |
| **学習者** | ayutaz (piper-plus project) |
| **モデルアーキテクチャ** | VITS (Variational Inference with adversarial learning for end-to-end Text-to-Speech) |
| **対応言語** | ja, en, zh, es, fr, pt |
| **精度** | fp16 |
| **phoneme_type** | multilingual |

### espeak-ng と本モデルの関係

- **学習時**: 音素ラベル生成に espeak-ng を使用
- **推論時**: espeak-ng は一切使用しない。piper-plus / uPiper 独自の G2P エンジン (DotNetG2P) で音素化を行う

モデル重みは espeak-ng の派生物ではなく、espeak-ng は学習パイプラインのツールとして使用されたのみです。これは GPL コンパイラ (GCC 等) で生成したバイナリが GPL の対象外であるのと同じ原則に基づきます。

### 推論時のライセンス構成

```
テキスト入力
  → DotNetG2P (Apache-2.0)  ※espeak-ng不使用
  → PuaTokenMapper (Apache-2.0)
  → multilingual-test-medium.onnx (Apache-2.0)
  → 音声出力
```

推論パイプライン全体が Apache-2.0 / MIT 互換であり、GPL 依存は存在しません。
