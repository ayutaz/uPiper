using uPiper.Core;

namespace uPiper.Tests.Editor.TestHelpers
{
    /// <summary>
    /// テスト用PiperVoiceConfigファクトリ。テスト間でヘルパーを共有する。
    /// </summary>
    internal static class TestVoiceConfigFactory
    {
        /// <summary>特殊トークンのみ。実音素なし。</summary>
        internal static PiperVoiceConfig CreateMinimal()
        {
            return new PiperVoiceConfig
            {
                VoiceId = "test-minimal",
                Language = "ja",
                PhonemeIdMap = TestPhonemeIdMapFactory.CreateMinimal()
            };
        }

        /// <summary>特殊トークン+基本音素(20個)。一般テスト用。</summary>
        internal static PiperVoiceConfig CreateValid()
        {
            return new PiperVoiceConfig
            {
                VoiceId = "test-valid",
                Language = "ja",
                PhonemeIdMap = TestPhonemeIdMapFactory.CreateValid()
            };
        }

        /// <summary>50+エントリ。バリデーション通過用。</summary>
        internal static PiperVoiceConfig CreateFull()
        {
            return new PiperVoiceConfig
            {
                VoiceId = "test-full",
                Language = "ja",
                PhonemeIdMap = TestPhonemeIdMapFactory.CreateFull()
            };
        }
    }
}