using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using uPiper.Editor.DictionaryManager;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class DictionaryJsonEditorTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "uPiperDictTest_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string TempFile(string name = "test.json")
        {
            return Path.Combine(_tempDir, name);
        }

        private void WriteSampleJson(string path, string json)
        {
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        // --- ReadEntries ---

        [Test]
        public void ReadEntries_ValidJson_ReturnsAllEntries()
        {
            var path = TempFile();
            WriteSampleJson(path, @"{
  ""version"": ""2.0"",
  ""entries"": {
    ""Docker"": {""pronunciation"": ""ドッカー"", ""priority"": 9},
    ""GitHub"": {""pronunciation"": ""ギットハブ"", ""priority"": 8}
  }
}");

            var entries = DictionaryJsonEditor.ReadEntries(path);

            Assert.That(entries.Count, Is.EqualTo(2),
                "Should read 2 entries from valid JSON");
            Assert.That(entries["Docker"].pronunciation, Is.EqualTo("ドッカー"),
                "Docker pronunciation should be parsed correctly");
            Assert.That(entries["Docker"].priority, Is.EqualTo(9),
                "Docker priority should be parsed correctly");
            Assert.That(entries["GitHub"].pronunciation, Is.EqualTo("ギットハブ"),
                "GitHub pronunciation should be parsed correctly");
            Assert.That(entries["GitHub"].priority, Is.EqualTo(8),
                "GitHub priority should be parsed correctly");
        }

        [Test]
        public void ReadEntries_EmptyEntries_ReturnsEmpty()
        {
            var path = TempFile();
            WriteSampleJson(path, @"{
  ""version"": ""2.0"",
  ""entries"": {}
}");

            var entries = DictionaryJsonEditor.ReadEntries(path);

            Assert.That(entries.Count, Is.EqualTo(0),
                "Empty entries object should return 0 entries");
        }

        [Test]
        public void ReadEntries_CommentKeys_AreSkipped()
        {
            var path = TempFile();
            WriteSampleJson(path, @"{
  ""version"": ""2.0"",
  ""entries"": {
    ""// This is a comment"": """",
    ""Docker"": {""pronunciation"": ""ドッカー"", ""priority"": 9},
    ""//Another comment"": """"
  }
}");

            var entries = DictionaryJsonEditor.ReadEntries(path);

            Assert.That(entries.Count, Is.EqualTo(1),
                "Comment keys should be skipped");
            Assert.That(entries.ContainsKey("Docker"), Is.True,
                "Non-comment entry should be present");
        }

        [Test]
        public void ReadEntries_FileNotFound_ReturnsEmpty()
        {
            var path = TempFile("nonexistent.json");

            var entries = DictionaryJsonEditor.ReadEntries(path);

            Assert.That(entries.Count, Is.EqualTo(0),
                "Non-existent file should return empty dictionary");
        }

        [Test]
        public void ReadEntries_DefaultPriority_IsFive()
        {
            var path = TempFile();
            WriteSampleJson(path, @"{
  ""version"": ""2.0"",
  ""entries"": {
    ""Test"": {""pronunciation"": ""テスト""}
  }
}");

            var entries = DictionaryJsonEditor.ReadEntries(path);

            Assert.That(entries["Test"].priority, Is.EqualTo(5),
                "Default priority should be 5 when not specified");
        }

        [Test]
        public void ReadEntries_PathTraversal_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(
                () => DictionaryJsonEditor.ReadEntries("../../../etc/passwd"));
        }

        [Test]
        public void ReadEntries_OversizedFile_ThrowsArgumentException()
        {
            var path = TempFile("oversized.json");
            // 10MB + 1 byte のダミーファイルを作成
            using (var fs = File.Create(path))
            {
                fs.SetLength(10 * 1024 * 1024 + 1);
            }

            Assert.That(
                () => DictionaryJsonEditor.ReadEntries(path),
                Throws.TypeOf<System.ArgumentException>()
                    .With.Message.Contains("too large"));
        }

        // --- UpsertEntry ---

        [Test]
        public void UpsertEntry_NewEntry_AddsToJson()
        {
            var path = TempFile();
            WriteSampleJson(path, @"{
  ""version"": ""2.0"",
  ""entries"": {
    ""Docker"": {""pronunciation"": ""ドッカー"", ""priority"": 9}
  }
}");

            DictionaryJsonEditor.UpsertEntry(path, "Kubernetes", "クバネティス", 8);

            var entries = DictionaryJsonEditor.ReadEntries(path);
            Assert.That(entries.Count, Is.EqualTo(2),
                "Should have 2 entries after upsert");
            Assert.That(entries["Kubernetes"].pronunciation, Is.EqualTo("クバネティス"),
                "New entry pronunciation should be set correctly");
            Assert.That(entries["Kubernetes"].priority, Is.EqualTo(8),
                "New entry priority should be set correctly");
        }

        [Test]
        public void UpsertEntry_ExistingEntry_Updates()
        {
            var path = TempFile();
            WriteSampleJson(path, @"{
  ""version"": ""2.0"",
  ""entries"": {
    ""Docker"": {""pronunciation"": ""ドッカー"", ""priority"": 9}
  }
}");

            DictionaryJsonEditor.UpsertEntry(path, "Docker", "ドッカ", 7);

            var entries = DictionaryJsonEditor.ReadEntries(path);
            Assert.That(entries.Count, Is.EqualTo(1),
                "Entry count should remain 1 after updating existing entry");
            Assert.That(entries["Docker"].pronunciation, Is.EqualTo("ドッカ"),
                "Pronunciation should be updated");
            Assert.That(entries["Docker"].priority, Is.EqualTo(7),
                "Priority should be updated");
        }

        [Test]
        public void UpsertEntry_NewFile_CreatesJson()
        {
            var path = TempFile("new_dict.json");

            DictionaryJsonEditor.UpsertEntry(path, "Hello", "ハロー", 5);

            Assert.That(File.Exists(path), Is.True,
                "Upsert on non-existent file should create the file");
            var entries = DictionaryJsonEditor.ReadEntries(path);
            Assert.That(entries.Count, Is.EqualTo(1),
                "Newly created file should contain 1 entry");
            Assert.That(entries["Hello"].pronunciation, Is.EqualTo("ハロー"),
                "Entry pronunciation should be set correctly in new file");
        }

        // --- RemoveEntry ---

        [Test]
        public void RemoveEntry_Existing_Removes()
        {
            var path = TempFile();
            WriteSampleJson(path, @"{
  ""version"": ""2.0"",
  ""entries"": {
    ""Docker"": {""pronunciation"": ""ドッカー"", ""priority"": 9},
    ""GitHub"": {""pronunciation"": ""ギットハブ"", ""priority"": 8}
  }
}");

            var removed = DictionaryJsonEditor.RemoveEntry(path, "Docker");

            Assert.That(removed, Is.True,
                "RemoveEntry should return true for existing entry");
            var entries = DictionaryJsonEditor.ReadEntries(path);
            Assert.That(entries.Count, Is.EqualTo(1),
                "Should have 1 entry after removal");
            Assert.That(entries.ContainsKey("Docker"), Is.False,
                "Docker should no longer be present");
            Assert.That(entries.ContainsKey("GitHub"), Is.True,
                "GitHub should still be present");
        }

        [Test]
        public void RemoveEntry_NonExisting_ReturnsFalse()
        {
            var path = TempFile();
            WriteSampleJson(path, @"{
  ""version"": ""2.0"",
  ""entries"": {
    ""Docker"": {""pronunciation"": ""ドッカー"", ""priority"": 9}
  }
}");

            var removed = DictionaryJsonEditor.RemoveEntry(path, "NonExistent");

            Assert.That(removed, Is.False,
                "RemoveEntry should return false for non-existing entry");
        }

        [Test]
        public void RemoveEntry_FileNotFound_ReturnsFalse()
        {
            var path = TempFile("nonexistent.json");

            var removed = DictionaryJsonEditor.RemoveEntry(path, "Docker");

            Assert.That(removed, Is.False,
                "RemoveEntry should return false when file does not exist");
        }

        // --- ExportToJson ---

        [Test]
        public void ExportToJson_RoundTrip_PreservesData()
        {
            var path = TempFile();
            var original = new Dictionary<string, (string pronunciation, int priority)>
            {
                { "Docker", ("ドッカー", 9) },
                { "GitHub", ("ギットハブ", 8) },
                { "API", ("エーピーアイ", 5) }
            };

            DictionaryJsonEditor.ExportToJson(path, original);
            var readBack = DictionaryJsonEditor.ReadEntries(path);

            Assert.That(readBack.Count, Is.EqualTo(original.Count),
                "Round-trip should preserve entry count");
            foreach (var kvp in original)
            {
                Assert.That(readBack.ContainsKey(kvp.Key), Is.True,
                    $"Missing key after round-trip: {kvp.Key}");
                Assert.That(readBack[kvp.Key].pronunciation, Is.EqualTo(kvp.Value.pronunciation),
                    $"Pronunciation mismatch for {kvp.Key}");
                Assert.That(readBack[kvp.Key].priority, Is.EqualTo(kvp.Value.priority),
                    $"Priority mismatch for {kvp.Key}");
            }
        }

        [Test]
        public void ExportToJson_Utf8NoBom()
        {
            var path = TempFile();
            DictionaryJsonEditor.ExportToJson(path, new Dictionary<string, (string, int)>
            {
                { "Test", ("テスト", 5) }
            });

            var bytes = File.ReadAllBytes(path);
            // UTF-8 BOM is EF BB BF; first byte should NOT be 0xEF
            Assert.That(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                Is.False, "File should not have UTF-8 BOM");
        }

        [Test]
        public void ExportToJson_ContainsVersionField()
        {
            var path = TempFile();
            DictionaryJsonEditor.ExportToJson(path, new Dictionary<string, (string, int)>
            {
                { "Test", ("テスト", 5) }
            });

            var json = File.ReadAllText(path);
            Assert.That(json, Does.Contain("\"version\": \"2.0\""),
                "Exported JSON should contain version field");
            Assert.That(json, Does.Contain("\"entries\""),
                "Exported JSON should contain entries field");
        }
    }
}