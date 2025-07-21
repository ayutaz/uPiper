using NUnit.Framework;
using uPiper.Core.Platform;

namespace uPiper.Tests.Runtime.Core.Platform
{
    /// <summary>
    /// Tests for VersionHelper utility class
    /// </summary>
    [TestFixture]
    public class VersionHelperTest
    {
        [Test]
        public void GetMajorVersion_ValidVersions_ReturnsCorrectMajorVersion()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(2, VersionHelper.GetMajorVersion("2.0.0"));
            Assert.AreEqual(3, VersionHelper.GetMajorVersion("3.1.2"));
            Assert.AreEqual(2, VersionHelper.GetMajorVersion("2.0.0-full"));
            Assert.AreEqual(1, VersionHelper.GetMajorVersion("1.5"));
            Assert.AreEqual(10, VersionHelper.GetMajorVersion("10.0.1"));
        }

        [Test]
        public void GetMajorVersion_InvalidVersions_ReturnsMinusOne()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(-1, VersionHelper.GetMajorVersion(""));
            Assert.AreEqual(-1, VersionHelper.GetMajorVersion(null));
            Assert.AreEqual(-1, VersionHelper.GetMajorVersion("invalid"));
            Assert.AreEqual(-1, VersionHelper.GetMajorVersion("v2.0.0"));
            Assert.AreEqual(-1, VersionHelper.GetMajorVersion("not.a.version"));
        }

        [Test]
        public void IsValidOpenJTalkVersion_ValidVersions_ReturnsTrue()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(VersionHelper.IsValidOpenJTalkVersion("2.0.0"));
            Assert.IsTrue(VersionHelper.IsValidOpenJTalkVersion("2.0.0-full"));
            Assert.IsTrue(VersionHelper.IsValidOpenJTalkVersion("3.1.2"));
            Assert.IsTrue(VersionHelper.IsValidOpenJTalkVersion("3.0"));
        }

        [Test]
        public void IsValidOpenJTalkVersion_InvalidVersions_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.IsFalse(VersionHelper.IsValidOpenJTalkVersion("1.0.0"));
            Assert.IsFalse(VersionHelper.IsValidOpenJTalkVersion("4.0.0"));
            Assert.IsFalse(VersionHelper.IsValidOpenJTalkVersion(""));
            Assert.IsFalse(VersionHelper.IsValidOpenJTalkVersion(null));
            Assert.IsFalse(VersionHelper.IsValidOpenJTalkVersion("invalid"));
        }

        [Test]
        public void ParseVersion_ValidVersions_ReturnsCorrectVersionInfo()
        {
            // Test "2.0.0"
            var version = VersionHelper.ParseVersion("2.0.0");
            Assert.IsNotNull(version);
            Assert.AreEqual(2, version.Major);
            Assert.AreEqual(0, version.Minor);
            Assert.AreEqual(0, version.Patch);
            Assert.IsNull(version.Suffix);
            Assert.IsTrue(version.IsCompatibleOpenJTalkVersion);

            // Test "3.1.2"
            version = VersionHelper.ParseVersion("3.1.2");
            Assert.IsNotNull(version);
            Assert.AreEqual(3, version.Major);
            Assert.AreEqual(1, version.Minor);
            Assert.AreEqual(2, version.Patch);
            Assert.IsNull(version.Suffix);
            Assert.IsTrue(version.IsCompatibleOpenJTalkVersion);

            // Test "2.0.0-full"
            version = VersionHelper.ParseVersion("2.0.0-full");
            Assert.IsNotNull(version);
            Assert.AreEqual(2, version.Major);
            Assert.AreEqual(0, version.Minor);
            Assert.AreEqual(0, version.Patch);
            Assert.AreEqual("full", version.Suffix);
            Assert.IsTrue(version.IsCompatibleOpenJTalkVersion);

            // Test "2.1" (without patch version)
            version = VersionHelper.ParseVersion("2.1");
            Assert.IsNotNull(version);
            Assert.AreEqual(2, version.Major);
            Assert.AreEqual(1, version.Minor);
            Assert.AreEqual(0, version.Patch);
            Assert.IsNull(version.Suffix);
            Assert.IsTrue(version.IsCompatibleOpenJTalkVersion);
        }

        [Test]
        public void ParseVersion_InvalidVersions_ReturnsNull()
        {
            // Arrange & Act & Assert
            Assert.IsNull(VersionHelper.ParseVersion(""));
            Assert.IsNull(VersionHelper.ParseVersion(null));
            Assert.IsNull(VersionHelper.ParseVersion("invalid"));
            Assert.IsNull(VersionHelper.ParseVersion("v2.0.0"));
            Assert.IsNull(VersionHelper.ParseVersion("not.a.version"));
        }

        [Test]
        public void VersionInfo_ToString_ReturnsCorrectFormat()
        {
            // Test without suffix
            var version = new VersionInfo(2, 0, 0);
            Assert.AreEqual("2.0.0", version.ToString());

            // Test with suffix
            version = new VersionInfo(2, 0, 0, "full");
            Assert.AreEqual("2.0.0-full", version.ToString());

            // Test different versions
            version = new VersionInfo(3, 1, 2);
            Assert.AreEqual("3.1.2", version.ToString());
        }

        [Test]
        public void VersionInfo_IsCompatibleOpenJTalkVersion_ReturnsCorrectValues()
        {
            // Valid versions
            Assert.IsTrue(new VersionInfo(2, 0, 0).IsCompatibleOpenJTalkVersion);
            Assert.IsTrue(new VersionInfo(3, 1, 2).IsCompatibleOpenJTalkVersion);

            // Invalid versions
            Assert.IsFalse(new VersionInfo(1, 0, 0).IsCompatibleOpenJTalkVersion);
            Assert.IsFalse(new VersionInfo(4, 0, 0).IsCompatibleOpenJTalkVersion);
        }
    }
}