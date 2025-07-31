using System;
using System.Text.RegularExpressions;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Helper class for version string parsing and comparison
    /// </summary>
    public static class VersionHelper
    {
        /// <summary>
        /// Parse version string and extract major version number
        /// Supports formats like "2.0.0", "3.1.2", "2.0.0-full", etc.
        /// </summary>
        /// <param name="versionString">Version string to parse</param>
        /// <returns>Major version number, or -1 if parsing failed</returns>
        public static int GetMajorVersion(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return -1;

            // Match pattern like "2.0.0", "3.1.2", "2.0.0-full", etc.
            var match = Regex.Match(versionString, @"^(\d+)\.(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var majorVersion))
            {
                return majorVersion;
            }

            return -1;
        }

        /// <summary>
        /// Check if version string represents a valid OpenJTalk version
        /// Accepts versions 2.x and 3.x as they are both valid
        /// </summary>
        /// <param name="versionString">Version string to validate</param>
        /// <returns>True if version is valid OpenJTalk version</returns>
        public static bool IsValidOpenJTalkVersion(string versionString)
        {
            var majorVersion = GetMajorVersion(versionString);
            return majorVersion == 2 || majorVersion == 3;
        }

        /// <summary>
        /// Parse full version information from version string
        /// </summary>
        /// <param name="versionString">Version string to parse</param>
        /// <returns>Version info or null if parsing failed</returns>
        public static VersionInfo ParseVersion(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return null;

            // Match pattern like "2.0.0-full" or "3.1.2"
            var match = Regex.Match(versionString, @"^(\d+)\.(\d+)(?:\.(\d+))?(?:-(.+))?");
            if (!match.Success)
                return null;

            if (!int.TryParse(match.Groups[1].Value, out var major))
                return null;

            if (!int.TryParse(match.Groups[2].Value, out var minor))
                return null;

            var patch = 0;
            if (!string.IsNullOrEmpty(match.Groups[3].Value))
            {
                int.TryParse(match.Groups[3].Value, out patch);
            }

            var suffix = match.Groups[4].Value;
            if (string.IsNullOrEmpty(suffix))
                suffix = null;

            return new VersionInfo(major, minor, patch, suffix);
        }
    }

    /// <summary>
    /// Represents version information
    /// </summary>
    public class VersionInfo
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string Suffix { get; }

        public VersionInfo(int major, int minor, int patch = 0, string suffix = null)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Suffix = suffix;
        }

        public override string ToString()
        {
            var version = $"{Major}.{Minor}.{Patch}";
            if (!string.IsNullOrEmpty(Suffix))
                version += $"-{Suffix}";
            return version;
        }

        /// <summary>
        /// Check if this version is compatible with OpenJTalk requirements
        /// </summary>
        public bool IsCompatibleOpenJTalkVersion => Major == 2 || Major == 3;
    }
}