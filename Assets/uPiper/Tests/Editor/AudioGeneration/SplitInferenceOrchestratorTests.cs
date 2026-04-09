using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Collections;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// SplitInferenceOrchestrator の統合テスト。
    /// 沈黙トークンによる句分割と無音挿入を検証する。
    /// </summary>
    [TestFixture]
    public class SplitInferenceOrchestratorTests
    {
        private StubInferenceAudioGenerator _stubGenerator;
        private SplitInferenceOrchestrator _orchestrator;

        /// <summary>
        /// テスト用の最小限 phonemeIdMap。
        /// PhonemeSilenceProcessor.SplitAtPhonemeSilence が
        /// phonemeSilence のキーを phonemeIdMap で逆引きするために使用。
        /// </summary>
        private static Dictionary<string, int[]> CreateMinimalPhonemeIdMap()
        {
            return new Dictionary<string, int[]>
            {
                ["_"] = new[] { 0 },  // PAD（沈黙トークンとしても使用）
                ["^"] = new[] { 1 },  // BOS
                ["$"] = new[] { 2 },  // EOS
                ["a"] = new[] { 3 },
                ["i"] = new[] { 4 },
                ["u"] = new[] { 5 },
                ["k"] = new[] { 8 },
                ["o"] = new[] { 7 },
            };
        }

        [SetUp]
        public void SetUp()
        {
            _stubGenerator = new StubInferenceAudioGenerator
            {
                // 各推論呼び出しで50サンプルの固定データを返す
                AudioDataToReturn = CreateFixedAudioData(50, 0.5f),
            };
            _orchestrator = new SplitInferenceOrchestrator(_stubGenerator);
        }

        // ── 単一句（分割なし）───────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_SinglePhrase_ReturnsSinglePhraseAudio()
        {
            // Arrange: 沈黙トークンを含まない phonemeIds
            // phonemeSilence に "#" を設定するが、phonemeIds には "#" の ID が無いため分割されない
            var phonemeIds = new[] { 1, 3, 4, 5, 2 }; // ^, a, i, u, $
            var phonemeSilence = new Dictionary<string, float> { ["#"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();
            phonemeIdMap["#"] = new[] { 99 }; // phonemeSilence のキー用（phonemeIds に含まれない）

            // Act
            var result = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(result.IsCreated, "結果が作成済みであること");
                Assert.AreEqual(1, _stubGenerator.GenerateCallCount,
                    "分割なしの場合、推論は1回のみ呼ばれること");
                // 単一句なので、返却データはスタブの固定データと同じ長さ
                Assert.AreEqual(50, result.Length,
                    "単一句のオーディオデータ長が一致すること");
            }
            finally
            {
                if (result.IsCreated)
                    result.Dispose();
            }
        }

        // ── 複数句（沈黙トークンで分割）──────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_MultiplePhrases_ConcatenatesWithSilence()
        {
            // Arrange: phonemeIds に沈黙トークン "_" (ID=0) を含める
            // [^(1), a(3), _(0), k(8), o(7), $(2)]
            // "_" の位置で分割される → 句1: [1,3,0], 句2: [8,7,2]
            var phonemeIds = new[] { 1, 3, 0, 8, 7, 2 };
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // 0.5秒 × 22050Hz = 11025 サンプルの無音が挿入される
            var expectedSilenceSamples = (int)(0.5f * 22050);

            // Act
            var result = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(result.IsCreated, "結果が作成済みであること");
                Assert.AreEqual(2, _stubGenerator.GenerateCallCount,
                    "2句に分割されるため、推論が2回呼ばれること");

                // 合計長 = 句1のオーディオ(50) + 無音(11025) + 句2のオーディオ(50)
                var expectedTotalLength = 50 + expectedSilenceSamples + 50;
                Assert.AreEqual(expectedTotalLength, result.Length,
                    "合計サンプル数が句のオーディオ+無音の合計と一致すること");

                // 無音区間が0.0fで埋められていることを確認
                // 句1のオーディオ(50サンプル)の後から無音区間が始まる
                for (var i = 50; i < 50 + expectedSilenceSamples; i++)
                {
                    Assert.AreEqual(0.0f, result[i],
                        $"無音区間のサンプル[{i}]が 0.0f であること");
                }
            }
            finally
            {
                if (result.IsCreated)
                    result.Dispose();
            }
        }

        // ── 空の phonemeIds ──────────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_EmptyPhonemeIds_ReturnsEmpty()
        {
            // Arrange: 空の phonemeIds
            // SplitAtPhonemeSilence は空配列に対して空の Phrase (PhonemeIds=[]) を1つ返す
            // GenerateWithSilenceSplitAsync は PhonemeIds が空の Phrase をスキップする
            var phonemeIds = Array.Empty<int>();
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // Act
            var result = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(result.IsCreated, "結果が作成済みであること");
                // 空の Phrase のみが生成され、PhonemeIds が空のためスキップされる
                // → totalLength は 0 になるが、結果配列自体は空（長さ0）
                Assert.AreEqual(0, _stubGenerator.GenerateCallCount,
                    "空入力に対して推論が呼ばれないこと");
                Assert.AreEqual(0, result.Length,
                    "空入力に対して空のオーディオデータが返ること");
            }
            finally
            {
                if (result.IsCreated)
                    result.Dispose();
            }
        }

        // ── Prosody付き句分割 ─────────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplitAsync_WithProsody_PropagatesProsodyToGenerator()
        {
            // Arrange: phonemeIds に沈黙トークン "_" (ID=0) を含め、prosody配列も設定
            var phonemeIds = new[] { 1, 3, 0, 8, 7, 2 };  // ^, a, _, k, o, $
            // stride=3 flat prosody: 6 phonemes * 3 = 18 elements
            var prosodyFlat = new[]
            {
                0, 1, 0,  // phoneme 0: a1=0, a2=1, a3=0
                1, 1, 0,  // phoneme 1: a1=1, a2=1, a3=0
                0, 0, 0,  // phoneme 2: a1=0, a2=0, a3=0
                2, 2, 1,  // phoneme 3: a1=2, a2=2, a3=1
                3, 2, 1,  // phoneme 4: a1=3, a2=2, a3=1
                0, 0, 0   // phoneme 5: a1=0, a2=0, a3=0
            };
            var phonemeSilence = new Dictionary<string, float> { ["_"] = 0.5f };
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // Act
            var result = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(result.IsCreated);
                Assert.AreEqual(2, _stubGenerator.GenerateCallCount,
                    "Prosody付きでも2句に分割されること");
                // 最後の呼び出しの prosodyFlat が non-null であることを確認
                Assert.IsNotNull(_stubGenerator.LastProsodyFlat,
                    "Prosody配列が下流に伝播されること");
            }
            finally
            {
                if (result.IsCreated)
                    result.Dispose();
            }
        }

        // ── コンストラクタ null チェック ──────────────────────────

        [Test]
        public void Constructor_NullGenerator_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SplitInferenceOrchestrator(null));
        }

        // ── Short text mitigation ────────────────────────────────

        [Test]
        public async Task GenerateWithSilenceSplit_ShortPhrase_AppliesShortTextMitigation()
        {
            // Arrange: 短い phonemeIds（5要素 < MinPhonemeIds=40）
            var phonemeIds = new[] { 1, 5, 6, 7, 2 }; // ^, u, ?, ?, $
            var phonemeSilence = new Dictionary<string, float>(); // 空 → 分割なし
            var phonemeIdMap = CreateMinimalPhonemeIdMap();

            // Act
            var result = await _orchestrator.GenerateWithSilenceSplitAsync(
                phonemeIds,
                prosodyFlat: null,
                phonemeSilence, phonemeIdMap,
                sampleRate: 22050);

            try
            {
                // Assert
                Assert.IsTrue(result.IsCreated, "結果が作成済みであること");
                Assert.AreEqual(1, _stubGenerator.GenerateCallCount,
                    "分割なしの場合、推論は1回のみ呼ばれること");
                // ShortTextProcessor が適用され、phonemeIds が MinPhonemeIds 以上にパディングされていること
                Assert.GreaterOrEqual(_stubGenerator.LastPhonemeIds.Length,
                    ShortTextProcessor.MinPhonemeIds,
                    "短いフレーズにパディングが適用され、phonemeIds が MinPhonemeIds 以上であること");
            }
            finally
            {
                if (result.IsCreated)
                    result.Dispose();
            }
        }

        // ── ヘルパー ─────────────────────────────────────────────

        /// <summary>
        /// 指定値で埋められた固定長オーディオデータを生成する。
        /// </summary>
        private static float[] CreateFixedAudioData(int length, float value)
        {
            var data = new float[length];
            for (var i = 0; i < length; i++)
                data[i] = value;
            return data;
        }
    }
}