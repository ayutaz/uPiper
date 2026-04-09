using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Unit tests for <see cref="TrigramProfileLoader.ParseJson"/>.
    /// Covers valid JSON, invalid JSON, empty profiles, and missing version.
    /// </summary>
    [TestFixture]
    public class TrigramProfileLoaderTests
    {
        [Test]
        public void ParseJson_ValidJson_ReturnsProfiles()
        {
            var json = @"{
                ""version"": 1,
                ""profiles"": {
                    ""en"": [""the"", ""ing"", ""and""],
                    ""es"": [""que"", ""ent"", ""con""]
                }
            }";

            var result = TrigramProfileLoader.ParseJson(json);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.ContainsKey("en"));
            Assert.IsTrue(result.ContainsKey("es"));
            Assert.AreEqual("en", result["en"].Language);
            Assert.AreEqual(3, result["en"].Count);
            Assert.AreEqual("es", result["es"].Language);
            Assert.AreEqual(3, result["es"].Count);
        }

        [Test]
        public void ParseJson_InvalidJson_ReturnsNull()
        {
            var json = "{ this is not valid JSON !!!";

            var result = TrigramProfileLoader.ParseJson(json);

            Assert.IsNull(result);
        }

        [Test]
        public void ParseJson_EmptyProfiles_ReturnsNull()
        {
            // All profile arrays are null/empty, so no valid profiles are found
            var json = @"{
                ""version"": 1,
                ""profiles"": {}
            }";

            var result = TrigramProfileLoader.ParseJson(json);

            Assert.IsNull(result);
        }

        [Test]
        public void ParseJson_MissingVersion_ReturnsNull()
        {
            // version defaults to 0 when missing, which is treated as invalid
            var json = @"{
                ""profiles"": {
                    ""en"": [""the"", ""ing"", ""and""]
                }
            }";

            var result = TrigramProfileLoader.ParseJson(json);

            Assert.IsNull(result);
        }
    }
}
