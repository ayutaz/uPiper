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

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("ドッカー", entries["Docker"].pronunciation);
            Assert.AreEqual(9, entries["Docker"].priority);
            Assert.AreEqual("ギットハブ", entries["GitHub"].pronunciation);
            Assert.AreEqual(8, entries["GitHub"].priority);
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

            Assert.AreEqual(0, entries.Count);
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

            Assert.AreEqual(1, entries.Count);
            Assert.IsTrue(entries.ContainsKey("Docker"));
        }

        [Test]
        public void ReadEntries_FileNotFound_ReturnsEmpty()
        {
            var path = TempFile("nonexistent.json");

            var entries = DictionaryJsonEditor.ReadEntries(path);

            Assert.AreEqual(0, entries.Count);
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

            Assert.AreEqual(5, entries["Test"].priority);
        }

        [Test]
        public void ReadEntries_PathTraversal_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(
                () => DictionaryJsonEditor.ReadEntries("../../../etc/passwd"));
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
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("クバネティス", entries["Kubernetes"].pronunciation);
            Assert.AreEqual(8, entries["Kubernetes"].priority);
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
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("ドッカ", entries["Docker"].pronunciation);
            Assert.AreEqual(7, entries["Docker"].priority);
        }

        [Test]
        public void UpsertEntry_NewFile_CreatesJson()
        {
            var path = TempFile("new_dict.json");

            DictionaryJsonEditor.UpsertEntry(path, "Hello", "ハロー", 5);

            Assert.IsTrue(File.Exists(path));
            var entries = DictionaryJsonEditor.ReadEntries(path);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("ハロー", entries["Hello"].pronunciation);
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

            Assert.IsTrue(removed);
            var entries = DictionaryJsonEditor.ReadEntries(path);
            Assert.AreEqual(1, entries.Count);
            Assert.IsFalse(entries.ContainsKey("Docker"));
            Assert.IsTrue(entries.ContainsKey("GitHub"));
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

            Assert.IsFalse(removed);
        }

        [Test]
        public void RemoveEntry_FileNotFound_ReturnsFalse()
        {
            var path = TempFile("nonexistent.json");

            var removed = DictionaryJsonEditor.RemoveEntry(path, "Docker");

            Assert.IsFalse(removed);
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

            Assert.AreEqual(original.Count, readBack.Count);
            foreach (var kvp in original)
            {
                Assert.IsTrue(readBack.ContainsKey(kvp.Key), $"Missing key: {kvp.Key}");
                Assert.AreEqual(kvp.Value.pronunciation, readBack[kvp.Key].pronunciation);
                Assert.AreEqual(kvp.Value.priority, readBack[kvp.Key].priority);
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
            Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "File should not have UTF-8 BOM");
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
            StringAssert.Contains("\"version\": \"2.0\"", json);
            StringAssert.Contains("\"entries\"", json);
        }
    }
}