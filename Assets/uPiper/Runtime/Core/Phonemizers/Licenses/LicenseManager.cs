using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Licenses
{
    /// <summary>
    /// Manages third-party licenses for phonemizer libraries
    /// </summary>
    public static class LicenseManager
    {
        private static readonly Dictionary<string, LicenseInfo> Licenses = new()
        {
            ["OpenJTalk"] = new LicenseInfo
            {
                Name = "OpenJTalk",
                Type = "Modified BSD",
                CopyrightHolder = "Nagoya Institute of Technology",
                Year = "2008-2016",
                RequiresAttribution = true,
                ProhibitsEndorsement = true
            },
            ["CMUDict"] = new LicenseInfo
            {
                Name = "CMU Pronouncing Dictionary",
                Type = "BSD-style",
                CopyrightHolder = "Carnegie Mellon University",
                Year = "1993-2015",
                RequiresAttribution = true,
                ProhibitsEndorsement = false
            },
            ["OpenPhonemizer"] = new LicenseInfo
            {
                Name = "OpenPhonemizer",
                Type = "BSD-3-Clause Clear",
                CopyrightHolder = "OpenPhonemizer Contributors",
                Year = "2023",
                RequiresAttribution = true,
                ProhibitsEndorsement = true,
                ClearPatentGrant = true
            },
            ["Flite"] = new LicenseInfo
            {
                Name = "Flite - Festival Lite",
                Type = "MIT-CMU",
                CopyrightHolder = "Carnegie Mellon University",
                Year = "1999-2014",
                RequiresAttribution = true,
                ProhibitsEndorsement = false
            },
            ["pypinyin"] = new LicenseInfo
            {
                Name = "pypinyin",
                Type = "MIT",
                CopyrightHolder = "mozillazg",
                Year = "2016",
                RequiresAttribution = true,
                ProhibitsEndorsement = false
            },
            ["IndicXlit"] = new LicenseInfo
            {
                Name = "IndicXlit",
                Type = "MIT",
                CopyrightHolder = "AI4Bharat",
                Year = "2021",
                RequiresAttribution = true,
                ProhibitsEndorsement = false
            }
        };

        /// <summary>
        /// License information structure
        /// </summary>
        public class LicenseInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string CopyrightHolder { get; set; }
            public string Year { get; set; }
            public bool RequiresAttribution { get; set; }
            public bool ProhibitsEndorsement { get; set; }
            public bool ClearPatentGrant { get; set; }
        }

        /// <summary>
        /// Generate third-party notices file
        /// </summary>
        public static void GenerateThirdPartyNotices()
        {
            var notices = new StringBuilder();
            notices.AppendLine("================================================================================");
            notices.AppendLine("                        THIRD-PARTY SOFTWARE NOTICES");
            notices.AppendLine("================================================================================");
            notices.AppendLine();
            notices.AppendLine("This software includes third-party components with the following licenses:");
            notices.AppendLine();

            var index = 1;
            foreach (var license in Licenses)
            {
                notices.AppendLine("================================================================================");
                notices.AppendLine($"{index}. {license.Value.Name}");
                notices.AppendLine("================================================================================");
                notices.AppendLine($"License: {license.Value.Type}");
                notices.AppendLine($"Copyright (c) {license.Value.Year} {license.Value.CopyrightHolder}");
                notices.AppendLine();

                // Add specific license text based on type
                AppendLicenseText(notices, license.Value);
                notices.AppendLine();
                index++;
            }

            var outputPath = Path.Combine(Application.dataPath, "uPiper/Licenses/THIRD-PARTY-NOTICES.txt");
            File.WriteAllText(outputPath, notices.ToString());

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif

            Debug.Log($"Generated third-party notices at: {outputPath}");
        }

        /// <summary>
        /// Validate that all required licenses are properly acknowledged
        /// </summary>
        public static bool ValidateLicenses()
        {
            var errors = new List<string>();
            var licensePath = Path.Combine(Application.dataPath, "uPiper/Licenses");

            if (!Directory.Exists(licensePath))
            {
                errors.Add($"License directory not found: {licensePath}");
            }

            foreach (var license in Licenses)
            {
                if (license.Value.RequiresAttribution)
                {
                    var noticesPath = Path.Combine(licensePath, "THIRD-PARTY-NOTICES.txt");
                    if (!File.Exists(noticesPath) || !File.ReadAllText(noticesPath).Contains(license.Value.Name))
                    {
                        errors.Add($"Missing attribution for: {license.Value.Name}");
                    }
                }
            }

            if (errors.Count > 0)
            {
                Debug.LogError("License validation failed:\n" + string.Join("\n", errors));
                return false;
            }

            Debug.Log("License validation passed!");
            return true;
        }

        /// <summary>
        /// Get license text for a specific library
        /// </summary>
        public static string GetLicenseText(string libraryName)
        {
            if (!Licenses.TryGetValue(libraryName, out var info))
            {
                return $"License information not found for: {libraryName}";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"{info.Name} - {info.Type} License");
            sb.AppendLine($"Copyright (c) {info.Year} {info.CopyrightHolder}");
            sb.AppendLine();

            AppendLicenseText(sb, info);

            return sb.ToString();
        }

        private static void AppendLicenseText(StringBuilder sb, LicenseInfo info)
        {
            switch (info.Type)
            {
                case "MIT":
                    AppendMITLicense(sb);
                    break;
                case "Modified BSD":
                case "BSD-style":
                case "BSD-3-Clause":
                    AppendBSD3License(sb, info);
                    break;
                case "BSD-3-Clause Clear":
                    AppendBSD3ClearLicense(sb, info);
                    break;
                case "MIT-CMU":
                    AppendMITCMULicense(sb);
                    break;
                default:
                    sb.AppendLine("[Full license text to be added]");
                    break;
            }
        }

        private static void AppendMITLicense(StringBuilder sb)
        {
            sb.AppendLine("Permission is hereby granted, free of charge, to any person obtaining a copy");
            sb.AppendLine("of this software and associated documentation files (the \"Software\"), to deal");
            sb.AppendLine("in the Software without restriction, including without limitation the rights");
            sb.AppendLine("to use, copy, modify, merge, publish, distribute, sublicense, and/or sell");
            sb.AppendLine("copies of the Software, and to permit persons to whom the Software is");
            sb.AppendLine("furnished to do so, subject to the following conditions:");
            sb.AppendLine();
            sb.AppendLine("The above copyright notice and this permission notice shall be included in all");
            sb.AppendLine("copies or substantial portions of the Software.");
            sb.AppendLine();
            sb.AppendLine("THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR");
            sb.AppendLine("IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,");
            sb.AppendLine("FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE");
            sb.AppendLine("AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER");
            sb.AppendLine("LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,");
            sb.AppendLine("OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE");
            sb.AppendLine("SOFTWARE.");
        }

        private static void AppendBSD3License(StringBuilder sb, LicenseInfo info)
        {
            sb.AppendLine("Redistribution and use in source and binary forms, with or without");
            sb.AppendLine("modification, are permitted provided that the following conditions are met:");
            sb.AppendLine();
            sb.AppendLine("1. Redistributions of source code must retain the above copyright notice, this");
            sb.AppendLine("   list of conditions and the following disclaimer.");
            sb.AppendLine();
            sb.AppendLine("2. Redistributions in binary form must reproduce the above copyright notice,");
            sb.AppendLine("   this list of conditions and the following disclaimer in the documentation");
            sb.AppendLine("   and/or other materials provided with the distribution.");
            sb.AppendLine();

            if (info.ProhibitsEndorsement)
            {
                sb.AppendLine("3. Neither the name of the copyright holder nor the names of its");
                sb.AppendLine("   contributors may be used to endorse or promote products derived from");
                sb.AppendLine("   this software without specific prior written permission.");
                sb.AppendLine();
            }

            sb.AppendLine("THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS \"AS IS\"");
            sb.AppendLine("AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE");
            sb.AppendLine("IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE");
            sb.AppendLine("DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE");
            sb.AppendLine("FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL");
            sb.AppendLine("DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR");
            sb.AppendLine("SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER");
            sb.AppendLine("CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,");
            sb.AppendLine("OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE");
            sb.AppendLine("OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.");
        }

        private static void AppendBSD3ClearLicense(StringBuilder sb, LicenseInfo info)
        {
            AppendBSD3License(sb, info);

            if (info.ClearPatentGrant)
            {
                sb.AppendLine();
                sb.AppendLine("NO EXPRESS OR IMPLIED LICENSES TO ANY PARTY'S PATENT RIGHTS ARE GRANTED BY");
                sb.AppendLine("THIS LICENSE.");
            }
        }

        private static void AppendMITCMULicense(StringBuilder sb)
        {
            // MIT-CMU is similar to MIT but with CMU-specific language
            AppendMITLicense(sb);
        }
    }
}