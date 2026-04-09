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
    public void LoadFromJson_PathTraversal_ThrowsArgumentException()
    {
        var dict = new CustomDictionary(loadDefaults: false);

        var ex = Assert.Throws<ArgumentException>(
            () => dict.LoadDictionaryFromPath("../../../etc/passwd"));
        StringAssert.Contains("traversal", ex.Message);
        Assert.AreEqual(0, dict.GetStats().TotalEntries);
    }
}