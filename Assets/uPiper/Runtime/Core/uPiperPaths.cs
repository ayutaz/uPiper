using System.IO;
using UnityEngine;

namespace uPiper.Core
{
    /// <summary>
    /// Central configuration for uPiper file paths across different environments
    /// </summary>
    public static class uPiperPaths
    {
        // Development environment paths (Samples~ folder)
        public const string SAMPLES_FOLDER = "Samples~";
        public const string OPENJTALK_DICT_FOLDER = "OpenJTalk Dictionary Data";
        public const string CMU_DICT_FOLDER = "CMU Pronouncing Dictionary";
        public const string VOICE_MODELS_FOLDER = "Voice Models";
        public const string OPENJTALK_DICT_NAME = "open_jtalk_dic_utf_8-1.11";

        // Runtime paths
        public const string STREAMING_ASSETS_UPIPER = "uPiper";
        public const string RESOURCES_MODELS_PATH = "uPiper/Models";
        public const string LEGACY_MODELS_PATH = "Models";

        /// <summary>
        /// Get the OpenJTalk dictionary path for development environment
        /// </summary>
        public static string GetDevelopmentOpenJTalkPath()
        {
#if UNITY_EDITOR && UPIPER_DEVELOPMENT
            return Path.Combine(Application.dataPath, "uPiper", SAMPLES_FOLDER,
                OPENJTALK_DICT_FOLDER, "naist_jdic", OPENJTALK_DICT_NAME);
#else
            // CI環境やPackage Manager環境では通常のStreamingAssetsパスを返す
            return GetRuntimeOpenJTalkPath();
#endif
        }

        /// <summary>
        /// Get the CMU dictionary path for development environment
        /// </summary>
        public static string GetDevelopmentCMUPath(string fileName)
        {
#if UNITY_EDITOR && UPIPER_DEVELOPMENT
            return Path.Combine(Application.dataPath, "uPiper", SAMPLES_FOLDER,
                CMU_DICT_FOLDER, fileName);
#else
            // CI環境やPackage Manager環境では通常のStreamingAssetsパスを返す
            return GetRuntimeCMUPath(fileName);
#endif
        }

        /// <summary>
        /// Get the runtime OpenJTalk dictionary path
        /// </summary>
        public static string GetRuntimeOpenJTalkPath()
        {
            return Path.Combine(Application.streamingAssetsPath, STREAMING_ASSETS_UPIPER,
                "OpenJTalk", "naist_jdic", OPENJTALK_DICT_NAME);
        }

        /// <summary>
        /// Get the runtime CMU dictionary path
        /// </summary>
        public static string GetRuntimeCMUPath(string fileName)
        {
            return Path.Combine(Application.streamingAssetsPath, STREAMING_ASSETS_UPIPER,
                "Phonemizers", fileName);
        }
    }
}