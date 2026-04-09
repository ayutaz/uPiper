using System;
using System.Threading.Tasks;
using DotNetG2P.French;
using DotNetG2P.Korean;
using DotNetG2P.Portuguese;
using DotNetG2P.Spanish;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual.Handlers;

namespace uPiper.Tests.Editor.Phonemizers.Handlers
{
    /// <summary>
    /// Tests for the common ILanguageG2PHandler contract across all 7 handler implementations.
    /// Handlers that require external dictionaries (ja, en, zh) are tested via
    /// the injection constructor or limited to interface-contract-only checks.
    /// </summary>
    [TestFixture]
    public class HandlerCommonTests
    {
        // ── LanguageCode returns correct ISO 639-1 code ─────────────────────

        [Test]
        public void SpanishHandler_LanguageCode_ReturnsEs()
        {
            using var handler = new SpanishG2PHandler();
            Assert.AreEqual("es", handler.LanguageCode);
        }

        [Test]
        public void FrenchHandler_LanguageCode_ReturnsFr()
        {
            using var handler = new FrenchG2PHandler();
            Assert.AreEqual("fr", handler.LanguageCode);
        }

        [Test]
        public void PortugueseHandler_LanguageCode_ReturnsPt()
        {
            using var handler = new PortugueseG2PHandler();
            Assert.AreEqual("pt", handler.LanguageCode);
        }

        [Test]
        public void KoreanHandler_LanguageCode_ReturnsKo()
        {
            using var handler = new KoreanG2PHandler();
            Assert.AreEqual("ko", handler.LanguageCode);
        }

        [Test]
        public void JapaneseHandler_LanguageCode_ReturnsJa()
        {
            using var handler = new JapaneseG2PHandler();
            Assert.AreEqual("ja", handler.LanguageCode);
        }

        [Test]
        public void EnglishHandler_LanguageCode_ReturnsEn()
        {
            using var handler = new EnglishG2PHandler();
            Assert.AreEqual("en", handler.LanguageCode);
        }

        [Test]
        public void ChineseHandler_LanguageCode_ReturnsZh()
        {
            using var handler = new ChineseG2PHandler();
            Assert.AreEqual("zh", handler.LanguageCode);
        }

        // ── IsInitialized is false before init (parameterless constructor) ──

        [Test]
        public void SpanishHandler_IsInitialized_FalseBeforeInit()
        {
            using var handler = new SpanishG2PHandler();
            Assert.IsFalse(handler.IsInitialized);
        }

        [Test]
        public void FrenchHandler_IsInitialized_FalseBeforeInit()
        {
            using var handler = new FrenchG2PHandler();
            Assert.IsFalse(handler.IsInitialized);
        }

        [Test]
        public void PortugueseHandler_IsInitialized_FalseBeforeInit()
        {
            using var handler = new PortugueseG2PHandler();
            Assert.IsFalse(handler.IsInitialized);
        }

        [Test]
        public void KoreanHandler_IsInitialized_FalseBeforeInit()
        {
            using var handler = new KoreanG2PHandler();
            Assert.IsFalse(handler.IsInitialized);
        }

        [Test]
        public void JapaneseHandler_IsInitialized_FalseBeforeInit()
        {
            using var handler = new JapaneseG2PHandler();
            Assert.IsFalse(handler.IsInitialized);
        }

        [Test]
        public void EnglishHandler_IsInitialized_FalseBeforeInit()
        {
            using var handler = new EnglishG2PHandler();
            Assert.IsFalse(handler.IsInitialized);
        }

        [Test]
        public void ChineseHandler_IsInitialized_FalseBeforeInit()
        {
            using var handler = new ChineseG2PHandler();
            Assert.IsFalse(handler.IsInitialized);
        }

        // ── Dispose called twice doesn't throw ──────────────────────────────

        [Test]
        public void SpanishHandler_DoubleDispose_DoesNotThrow()
        {
            var handler = new SpanishG2PHandler();
            Assert.DoesNotThrow(() =>
            {
                handler.Dispose();
                handler.Dispose();
            });
        }

        [Test]
        public void FrenchHandler_DoubleDispose_DoesNotThrow()
        {
            var handler = new FrenchG2PHandler();
            Assert.DoesNotThrow(() =>
            {
                handler.Dispose();
                handler.Dispose();
            });
        }

        [Test]
        public void PortugueseHandler_DoubleDispose_DoesNotThrow()
        {
            var handler = new PortugueseG2PHandler();
            Assert.DoesNotThrow(() =>
            {
                handler.Dispose();
                handler.Dispose();
            });
        }

        [Test]
        public void KoreanHandler_DoubleDispose_DoesNotThrow()
        {
            var handler = new KoreanG2PHandler();
            Assert.DoesNotThrow(() =>
            {
                handler.Dispose();
                handler.Dispose();
            });
        }

        [Test]
        public void JapaneseHandler_DoubleDispose_DoesNotThrow()
        {
            var handler = new JapaneseG2PHandler();
            Assert.DoesNotThrow(() =>
            {
                handler.Dispose();
                handler.Dispose();
            });
        }

        [Test]
        public void EnglishHandler_DoubleDispose_DoesNotThrow()
        {
            var handler = new EnglishG2PHandler();
            Assert.DoesNotThrow(() =>
            {
                handler.Dispose();
                handler.Dispose();
            });
        }

        [Test]
        public void ChineseHandler_DoubleDispose_DoesNotThrow()
        {
            var handler = new ChineseG2PHandler();
            Assert.DoesNotThrow(() =>
            {
                handler.Dispose();
                handler.Dispose();
            });
        }

        // ── InitializeAsync sets IsInitialized = true (no-external-file handlers) ──

        [Test]
        public async Task SpanishHandler_InitializeAsync_SetsIsInitialized()
        {
            using var handler = new SpanishG2PHandler();
            await handler.InitializeAsync();
            Assert.IsTrue(handler.IsInitialized);
        }

        [Test]
        public async Task FrenchHandler_InitializeAsync_SetsIsInitialized()
        {
            using var handler = new FrenchG2PHandler();
            await handler.InitializeAsync();
            Assert.IsTrue(handler.IsInitialized);
        }

        [Test]
        public async Task PortugueseHandler_InitializeAsync_SetsIsInitialized()
        {
            using var handler = new PortugueseG2PHandler();
            await handler.InitializeAsync();
            Assert.IsTrue(handler.IsInitialized);
        }

        [Test]
        public async Task KoreanHandler_InitializeAsync_SetsIsInitialized()
        {
            using var handler = new KoreanG2PHandler();
            await handler.InitializeAsync();
            Assert.IsTrue(handler.IsInitialized);
        }

        // ── Process on empty text returns empty or aligned arrays ────────────

        [Test]
        public async Task SpanishHandler_ProcessEmptyText_ReturnsEmptyArrays()
        {
            using var handler = new SpanishG2PHandler();
            await handler.InitializeAsync();

            var (phonemes, prosodyFlat) = handler.Process("");

            Assert.IsNotNull(phonemes);
            Assert.IsNotNull(prosodyFlat);
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length);
        }

        [Test]
        public async Task FrenchHandler_ProcessEmptyText_ReturnsEmptyArrays()
        {
            using var handler = new FrenchG2PHandler();
            await handler.InitializeAsync();

            var (phonemes, prosodyFlat) = handler.Process("");

            Assert.IsNotNull(phonemes);
            Assert.IsNotNull(prosodyFlat);
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length);
        }

        [Test]
        public async Task PortugueseHandler_ProcessEmptyText_ReturnsEmptyArrays()
        {
            using var handler = new PortugueseG2PHandler();
            await handler.InitializeAsync();

            var (phonemes, prosodyFlat) = handler.Process("");

            Assert.IsNotNull(phonemes);
            Assert.IsNotNull(prosodyFlat);
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length);
        }

        [Test]
        public async Task KoreanHandler_ProcessEmptyText_ReturnsEmptyArrays()
        {
            using var handler = new KoreanG2PHandler();
            await handler.InitializeAsync();

            var (phonemes, prosodyFlat) = handler.Process("");

            Assert.IsNotNull(phonemes);
            Assert.IsNotNull(prosodyFlat);
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length);
        }

        // ── Arrays returned by Process are aligned (same length) ────────────

        [Test]
        public async Task SpanishHandler_ProcessRealText_ArraysAligned()
        {
            using var handler = new SpanishG2PHandler();
            await handler.InitializeAsync();

            var (phonemes, prosodyFlat) = handler.Process("hola mundo");

            Assert.IsTrue(phonemes.Length > 0,
                "Spanish 'hola mundo' should produce phonemes");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length);
        }

        [Test]
        public async Task FrenchHandler_ProcessRealText_ArraysAligned()
        {
            using var handler = new FrenchG2PHandler();
            await handler.InitializeAsync();

            var (phonemes, prosodyFlat) = handler.Process("bonjour");

            Assert.IsTrue(phonemes.Length > 0,
                "French 'bonjour' should produce phonemes");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length);
        }

        [Test]
        public async Task PortugueseHandler_ProcessRealText_ArraysAligned()
        {
            using var handler = new PortugueseG2PHandler();
            await handler.InitializeAsync();

            var (phonemes, prosodyFlat) = handler.Process("bom dia");

            Assert.IsTrue(phonemes.Length > 0,
                "Portuguese 'bom dia' should produce phonemes");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length);
        }

        [Test]
        public async Task KoreanHandler_ProcessRealText_ArraysAligned()
        {
            using var handler = new KoreanG2PHandler();
            await handler.InitializeAsync();

            var (phonemes, prosodyFlat) = handler.Process("\uc548\ub155");

            Assert.IsTrue(phonemes.Length > 0,
                "Korean text should produce phonemes");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length);
        }

        // ── Injection constructor: IsInitialized is true immediately ────────

        [Test]
        public void SpanishHandler_InjectionCtor_IsInitializedTrue()
        {
            var engine = new SpanishG2PEngine();
            using var handler = new SpanishG2PHandler(engine);
            Assert.IsTrue(handler.IsInitialized);
            // Engine is disposed by handler.Dispose() (via 'using var')
        }

        [Test]
        public void FrenchHandler_InjectionCtor_IsInitializedTrue()
        {
            var engine = new FrenchG2PEngine();
            using var handler = new FrenchG2PHandler(engine);
            Assert.IsTrue(handler.IsInitialized);
            // Engine is disposed by handler.Dispose() (via 'using var')
        }

        [Test]
        public void PortugueseHandler_InjectionCtor_IsInitializedTrue()
        {
            var engine = new PortugueseG2PEngine();
            using var handler = new PortugueseG2PHandler(engine);
            Assert.IsTrue(handler.IsInitialized);
            // Engine is disposed by handler.Dispose() (via 'using var')
        }

        [Test]
        public void KoreanHandler_InjectionCtor_IsInitializedTrue()
        {
            var engine = new KoreanG2PEngine();
            using var handler = new KoreanG2PHandler(engine);
            Assert.IsTrue(handler.IsInitialized);
            // Engine is disposed by handler.Dispose() (via 'using var')
        }

        // ── Injection constructor: null engine throws ArgumentNullException ──

        [Test]
        public void SpanishHandler_NullEngine_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SpanishG2PHandler((SpanishG2PEngine)null));
        }

        [Test]
        public void FrenchHandler_NullEngine_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FrenchG2PHandler((FrenchG2PEngine)null));
        }

        [Test]
        public void PortugueseHandler_NullEngine_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PortugueseG2PHandler((PortugueseG2PEngine)null));
        }

        [Test]
        public void KoreanHandler_NullEngine_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new KoreanG2PHandler((KoreanG2PEngine)null));
        }

        [Test]
        public void EnglishHandler_NullEngine_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EnglishG2PHandler((DotNetG2P.English.EnglishG2PEngine)null));
        }

        [Test]
        public void ChineseHandler_NullEngine_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ChineseG2PHandler((DotNetG2P.Chinese.ChineseG2PEngine)null));
        }

        [Test]
        public void JapaneseHandler_NullPhonemizer_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new JapaneseG2PHandler(
                    (uPiper.Core.Phonemizers.Implementations.DotNetG2PPhonemizer)null));
        }

        // ── Process before init throws InvalidOperationException ────────────

        [Test]
        public void SpanishHandler_ProcessBeforeInit_ThrowsInvalidOperation()
        {
            using var handler = new SpanishG2PHandler();
            Assert.Throws<InvalidOperationException>(() => handler.Process("hola"));
        }

        [Test]
        public void FrenchHandler_ProcessBeforeInit_ThrowsInvalidOperation()
        {
            using var handler = new FrenchG2PHandler();
            Assert.Throws<InvalidOperationException>(() => handler.Process("bonjour"));
        }

        [Test]
        public void PortugueseHandler_ProcessBeforeInit_ThrowsInvalidOperation()
        {
            using var handler = new PortugueseG2PHandler();
            Assert.Throws<InvalidOperationException>(() => handler.Process("ola"));
        }

        [Test]
        public void KoreanHandler_ProcessBeforeInit_ThrowsInvalidOperation()
        {
            using var handler = new KoreanG2PHandler();
            Assert.Throws<InvalidOperationException>(() => handler.Process("\uc548\ub155"));
        }

        [Test]
        public void EnglishHandler_ProcessBeforeInit_ThrowsInvalidOperation()
        {
            using var handler = new EnglishG2PHandler();
            Assert.Throws<InvalidOperationException>(() => handler.Process("hello"));
        }

        [Test]
        public void JapaneseHandler_ProcessBeforeInit_ThrowsInvalidOperation()
        {
            using var handler = new JapaneseG2PHandler();
            Assert.Throws<InvalidOperationException>(() => handler.Process("test"));
        }

        // ── Process after dispose throws ObjectDisposedException ────────────

        [Test]
        public async Task SpanishHandler_ProcessAfterDispose_ThrowsObjectDisposed()
        {
            var handler = new SpanishG2PHandler();
            await handler.InitializeAsync();
            handler.Dispose();
            Assert.Throws<ObjectDisposedException>(() => handler.Process("hola"));
        }

        [Test]
        public async Task FrenchHandler_ProcessAfterDispose_ThrowsObjectDisposed()
        {
            var handler = new FrenchG2PHandler();
            await handler.InitializeAsync();
            handler.Dispose();
            Assert.Throws<ObjectDisposedException>(() => handler.Process("bonjour"));
        }

        [Test]
        public async Task PortugueseHandler_ProcessAfterDispose_ThrowsObjectDisposed()
        {
            var handler = new PortugueseG2PHandler();
            await handler.InitializeAsync();
            handler.Dispose();
            Assert.Throws<ObjectDisposedException>(() => handler.Process("ola"));
        }

        [Test]
        public async Task KoreanHandler_ProcessAfterDispose_ThrowsObjectDisposed()
        {
            var handler = new KoreanG2PHandler();
            await handler.InitializeAsync();
            handler.Dispose();
            Assert.Throws<ObjectDisposedException>(() => handler.Process("\uc548\ub155"));
        }

        // ── InitializeAsync is idempotent (second call is no-op) ────────────

        [Test]
        public async Task SpanishHandler_InitializeTwice_DoesNotThrow()
        {
            using var handler = new SpanishG2PHandler();
            await handler.InitializeAsync();
            Assert.DoesNotThrowAsync(async () => await handler.InitializeAsync());
            Assert.IsTrue(handler.IsInitialized);
        }

        [Test]
        public async Task KoreanHandler_InitializeTwice_DoesNotThrow()
        {
            using var handler = new KoreanG2PHandler();
            await handler.InitializeAsync();
            Assert.DoesNotThrowAsync(async () => await handler.InitializeAsync());
            Assert.IsTrue(handler.IsInitialized);
        }
    }
}