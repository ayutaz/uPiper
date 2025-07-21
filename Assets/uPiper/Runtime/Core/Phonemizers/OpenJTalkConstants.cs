namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Constants shared across OpenJTalk components
    /// </summary>
    public static class OpenJTalkConstants
    {
        /// <summary>
        /// Required dictionary files for OpenJTalk
        /// </summary>
        public static readonly string[] RequiredDictionaryFiles =
        {
            "sys.dic",
            "unk.dic",
            "char.bin",
            "matrix.bin",
            "left-id.def",
            "right-id.def",
            "pos-id.def",
            "rewrite.def"
        };

        /// <summary>
        /// Environment variable name for test mode detection
        /// </summary>
        public const string TestModeEnvironmentVariable = "UPIPER_TEST_MODE";

        /// <summary>
        /// Alternative environment variable for test detection
        /// </summary>
        public const string IsTestEnvironmentVariable = "IS_TEST_ENVIRONMENT";
    }
}