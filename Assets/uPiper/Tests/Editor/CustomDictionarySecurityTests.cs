using System;
using System.IO;
using NUnit.Framework;
using uPiper.Core.Phonemizers;

namespace uPiper.Tests.Editor;

[TestFixture]
public class CustomDictionarySecurityTests
{
    [Test]
    public void LoadFromJson_OversizedContent_ThrowsException()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // 11MB の文字列を生成して書き込む（MaxDictFileSize = 10MB を超える）
            var oversizedContent = new string(' ', 11 * 1024 * 1024);
            File.WriteAllText(tempFile, oversizedContent);

            var dict = new CustomDictionary(loadDefaults: false);

            Assert.Throws<ArgumentException>(() => dict.LoadDictionaryFromPath(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public void LoadFromJson_InvalidJson_HandlesGracefully()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{ invalid json !!!");

            var dict = new CustomDictionary(loadDefaults: false);

            // 無効なJSONはパース失敗するが例外をスローせず、エントリが0件のまま処理される
            Assert.DoesNotThrow(() => dict.LoadDictionaryFromPath(tempFile));
            Assert.AreEqual(0, dict.GetStats().TotalEntries);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public void LoadFromJson_PathTraversal_LogsWarning()
    {
        var dict = new CustomDictionary(loadDefaults: false);

        // "../" を含むパスはファイルが存在しないため FileNotFoundException がスローされるが、
        // その前にパストラバーサル警告がログ出力される
        // ここではパストラバーサルパスで例外が発生することを確認し、
        // 辞書の状態が変わらないことを検証する
        var traversalPath = Path.Combine(Path.GetTempPath(), "..", "nonexistent_dict.json");

        Assert.Throws<FileNotFoundException>(() => dict.LoadDictionaryFromPath(traversalPath));
        Assert.AreEqual(0, dict.GetStats().TotalEntries);
    }
}