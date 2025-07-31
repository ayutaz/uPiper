# CMU発音辞書セットアップガイド

## 概要

CMU発音辞書は、北米英語の機械可読発音辞書で、134,000以上の単語とその発音を含んでいます。

## ダウンロード手順

### 方法1: 直接ダウンロード（推奨）

1. 辞書ファイルをダウンロード:
   ```bash
   # 最新版をダウンロード
   curl -O https://raw.githubusercontent.com/cmusphinx/cmudict/master/cmudict.dict
   
   # または特定バージョン（0.7b）をダウンロード
   curl -O http://svn.code.sf.net/p/cmusphinx/code/trunk/cmudict/cmudict-0.7b
   ```

2. ファイルを以下に配置:
   ```
   Assets/StreamingAssets/uPiper/Phonemizers/cmudict-0.7b.txt
   ```

3. `RuleBasedPhonemizer.cs`を更新して完全な辞書を使用:
   ```csharp
   private string GetDefaultDictionaryPath()
   {
       // cmudict-sample.txtからcmudict-0.7b.txtに変更
       return Path.Combine(Application.streamingAssetsPath, 
           "uPiper", "Phonemizers", "cmudict-0.7b.txt");
   }
   ```

### 方法2: Gitクローン

```bash
# リポジトリをクローン
git clone https://github.com/cmusphinx/cmudict.git

# 辞書ファイルをコピー
cp cmudict/cmudict.dict Assets/StreamingAssets/uPiper/Phonemizers/cmudict-0.7b.txt
```

## ファイル形式

CMU辞書は以下の形式を使用します：
```
WORD  W ER1 D
WORD'S  W ER1 D Z
WORD(1)  W ER1 D
```

- 最初の列: 単語（大文字）
- 以降の列: ARPABET音素
- 数字はストレスを示す（0=ストレスなし、1=第一ストレス、2=第二ストレス）
- (1)、(2)等は代替発音を示す

## サイズに関する考慮事項

- 完全な辞書: 約4MB（非圧縮）
- 約134,000の単語エントリ
- 約10-15MBのRAMにロード

### モバイル最適化

モバイルプラットフォームの場合、以下を検討してください：

1. **圧縮形式**:
   ```csharp
   // より高速なロードのためのバイナリ形式を作成
   public class CompressedDictionary
   {
       public void CompressDictionary(string inputPath, string outputPath)
       {
           // テキストをバイナリ形式に変換
           // 音素に文字列インターンを使用
           // 高速検索用のインデックスを作成
       }
   }
   ```

2. **部分的ロード**:
   ```csharp
   // 最初は一般的な単語のみロード
   public class PartialDictionary
   {
       private const int CommonWordCount = 10000;
       
       public async Task LoadCommonWordsAsync()
       {
           // 最も頻出する単語を最初にロード
       }
       
       public async Task LoadFullDictionaryAsync()
       {
           // 残りの単語をバックグラウンドでロード
       }
   }
   ```

## 統合テスト

完全な辞書を追加した後：

```csharp
[Test]
public async Task FullDictionary_ShouldLoadSuccessfully()
{
    var phonemizer = new RuleBasedPhonemizer();
    await phonemizer.InitializeAsync(null);
    
    // 複雑な単語でテスト
    var testWords = new[] 
    {
        "internationalization",
        "pharmaceutical",
        "acknowledgment",
        "entrepreneurship"
    };
    
    foreach (var word in testWords)
    {
        var result = await phonemizer.PhonemizeAsync(word, "en-US");
        Assert.IsNotEmpty(result.Phonemes);
    }
}
```

## パフォーマンスへの影響

完全な辞書を使用した場合：
- 初期ロード時間: 約100-300ms（デスクトップ）、約500ms-1s（モバイル）
- メモリ使用量: +10-15MB
- 検索パフォーマンス: ハッシュテーブルでO(1)

## ライセンス

CMU発音辞書は**パブリックドメイン**であり、商用プロジェクトで自由に使用できます。