using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;
using uPiper.Core.Phonemizers.Multilingual.Handlers;

namespace uPiper.Tests.Editor.Phonemizers.Handlers
{
    /// <summary>
    /// Tests for MultilingualPhonemizer's handler dispatch path.
    /// Uses StubG2PHandler to verify routing, call counts, and disposal.
    /// </summary>
    [TestFixture]
    public class HandlerDispatchTests
    {
        // ── Handler dispatch: StubHandler.Process is called ─────────────────

        [Test]
        public async Task PhonemizeWithProsodyAsync_WithStubHandler_CallsProcess()
        {
            var stub = new StubG2PHandler("en",
                phonemes: new[] { "h", "e", "l", "o", "$" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler> { ["en"] = stub }
            };
            var mp = new MultilingualPhonemizer(options);
            await mp.InitializeAsync();

            var result = await mp.PhonemizeWithProsodyAsync("hello");

            Assert.AreEqual(1, stub.ProcessCallCount,
                "StubHandler.Process should be called exactly once");
            Assert.IsNotNull(result.Phonemes);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "Phonemes and ProsodyA1 must be aligned");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                "Phonemes and ProsodyA2 must be aligned");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
                "Phonemes and ProsodyA3 must be aligned");

            mp.Dispose();
        }

        // ── Handler dispatch: correct text is passed to handler ─────────────

        [Test]
        public async Task PhonemizeWithProsodyAsync_WithStubHandler_PassesCorrectText()
        {
            var stub = new StubG2PHandler("en",
                phonemes: new[] { "t", "e", "s", "t" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler> { ["en"] = stub }
            };
            var mp = new MultilingualPhonemizer(options);
            await mp.InitializeAsync();

            await mp.PhonemizeWithProsodyAsync("test input");

            Assert.AreEqual("test input", stub.LastProcessedText,
                "The full text should be passed to the handler");

            mp.Dispose();
        }

        // ── Handler dispatch: prosody values from stub are propagated ───────

        [Test]
        public async Task PhonemizeWithProsodyAsync_WithStubProsody_ValuesPreserved()
        {
            var phonemes = new[] { "a", "b", "c" };
            var a1 = new[] { 1, 2, 3 };
            var a2 = new[] { 4, 5, 6 };
            var a3 = new[] { 7, 8, 9 };
            var stub = new StubG2PHandler("en",
                phonemes: phonemes, a1: a1, a2: a2, a3: a3);
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler> { ["en"] = stub }
            };
            var mp = new MultilingualPhonemizer(options);
            await mp.InitializeAsync();

            var result = await mp.PhonemizeWithProsodyAsync("abc");

            Assert.AreEqual(phonemes, result.Phonemes);
            Assert.AreEqual(a1, result.ProsodyA1);
            Assert.AreEqual(a2, result.ProsodyA2);
            Assert.AreEqual(a3, result.ProsodyA3);

            mp.Dispose();
        }

        // ── Handler dispatch: multiple languages route correctly ─────────────

        [Test]
        public async Task PhonemizeWithProsodyAsync_MultipleStubHandlers_EachCalled()
        {
            var jaStub = new StubG2PHandler("ja",
                phonemes: new[] { "ko", "N_uvular" });
            var enStub = new StubG2PHandler("en",
                phonemes: new[] { "h", "e", "l", "o" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler>
                {
                    ["ja"] = jaStub,
                    ["en"] = enStub
                }
            };
            var mp = new MultilingualPhonemizer(options);
            await mp.InitializeAsync();

            // Mixed text: Japanese characters followed by English
            var result = await mp.PhonemizeWithProsodyAsync("\u3053\u3093hello");

            // Both handlers should have been called (one for each language segment)
            Assert.AreEqual(1, jaStub.ProcessCallCount,
                "Japanese handler should be called once");
            Assert.AreEqual(1, enStub.ProcessCallCount,
                "English handler should be called once");
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Combined result should have phonemes");

            mp.Dispose();
        }

        // ── Unregistered language: falls back to legacy backend ─────────────

        [Test]
        public async Task PhonemizeWithProsodyAsync_UnregisteredLang_FallsBackToLegacy()
        {
            // Only register "ja" handler; "en" has no handler and no legacy backend.
            // The phonemizer should skip the unhandled segment without crashing.
            var jaStub = new StubG2PHandler("ja",
                phonemes: new[] { "ko", "N_uvular" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler>
                {
                    ["ja"] = jaStub
                }
            };
            var mp = new MultilingualPhonemizer(options);

            // InitializeAsync will create a default EnglishG2PHandler for "en"
            // since no handler or legacy backend is provided.
            await mp.InitializeAsync();

            // The text "hello" will be routed to the auto-created English handler
            var result = await mp.PhonemizeWithProsodyAsync("hello");
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);

            // Japanese handler should NOT have been called (pure Latin text)
            Assert.AreEqual(0, jaStub.ProcessCallCount,
                "Japanese handler should not be called for pure Latin text");

            mp.Dispose();
        }

        // ── InitializeAsync creates default handlers for missing languages ──

        [Test]
        public async Task InitializeAsync_CreatesDefaultHandlersForMissingLanguages()
        {
            // Only provide a stub for "ja"; "es" should get auto-created
            var jaStub = new StubG2PHandler("ja",
                phonemes: new[] { "a" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "es" },
                DefaultLatinLanguage = "es",
                Handlers = new Dictionary<string, ILanguageG2PHandler>
                {
                    ["ja"] = jaStub
                }
            };
            var mp = new MultilingualPhonemizer(options);
            await mp.InitializeAsync();

            Assert.IsTrue(mp.IsInitialized);

            // "hola" routes to Spanish (defaultLatinLanguage = "es")
            // If no default handler was created, this would skip or fail
            var result = await mp.PhonemizeWithProsodyAsync("hola");
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Auto-created Spanish handler should produce phonemes");

            mp.Dispose();
        }

        // ── InitializeAsync calls handler.InitializeAsync on stubs ──────────

        [Test]
        public async Task InitializeAsync_CallsHandlerInitializeAsync()
        {
            var stub = new StubG2PHandler("en",
                phonemes: new[] { "p" });
            Assert.IsFalse(stub.IsInitialized,
                "Stub should not be initialized before MultilingualPhonemizer.InitializeAsync");

            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler> { ["en"] = stub }
            };
            var mp = new MultilingualPhonemizer(options);
            await mp.InitializeAsync();

            Assert.IsTrue(stub.IsInitialized,
                "MultilingualPhonemizer.InitializeAsync should call handler.InitializeAsync");

            mp.Dispose();
        }

        // ── Dispose does NOT dispose externally-provided handlers ────────────

        [Test]
        public void Dispose_DoesNotDisposeExternalHandlers()
        {
            var stub1 = new TrackingDisposableStub("ja");
            var stub2 = new TrackingDisposableStub("en");
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "ja", "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler>
                {
                    ["ja"] = stub1,
                    ["en"] = stub2
                }
            };
            var mp = new MultilingualPhonemizer(options);
            Assert.IsFalse(stub1.IsDisposed);
            Assert.IsFalse(stub2.IsDisposed);

            mp.Dispose();

            // Handlers provided via options.Handlers are NOT owned (isOwned: false),
            // so MultilingualPhonemizer must not dispose them.
            Assert.IsFalse(stub1.IsDisposed,
                "Externally-provided Japanese handler should NOT be disposed");
            Assert.IsFalse(stub2.IsDisposed,
                "Externally-provided English handler should NOT be disposed");
        }

        // ── Dispose called twice on MultilingualPhonemizer does not throw ───

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var stub = new StubG2PHandler("en",
                phonemes: new[] { "a" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler> { ["en"] = stub }
            };
            var mp = new MultilingualPhonemizer(options);

            Assert.DoesNotThrow(() =>
            {
                mp.Dispose();
                mp.Dispose();
            });
        }

        // ── Empty text returns empty arrays ─────────────────────────────────

        [Test]
        public async Task PhonemizeWithProsodyAsync_EmptyText_ReturnsEmptyResult()
        {
            var stub = new StubG2PHandler("en",
                phonemes: new[] { "x" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler> { ["en"] = stub }
            };
            var mp = new MultilingualPhonemizer(options);
            await mp.InitializeAsync();

            var result = await mp.PhonemizeWithProsodyAsync("");

            Assert.AreEqual(0, result.Phonemes.Length);
            Assert.AreEqual(0, result.ProsodyA1.Length);
            Assert.AreEqual(0, result.ProsodyA2.Length);
            Assert.AreEqual(0, result.ProsodyA3.Length);
            // Handler should NOT be called for empty text
            Assert.AreEqual(0, stub.ProcessCallCount,
                "Handler should not be called for empty text");

            mp.Dispose();
        }

        // ── Phonemize before initialize throws ──────────────────────────────

        [Test]
        public void PhonemizeWithProsodyAsync_BeforeInit_ThrowsInvalidOperation()
        {
            var stub = new StubG2PHandler("en",
                phonemes: new[] { "a" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler> { ["en"] = stub }
            };
            var mp = new MultilingualPhonemizer(options);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await mp.PhonemizeWithProsodyAsync("hello"));

            mp.Dispose();
        }

        // ── Phonemize after dispose throws ──────────────────────────────────

        [Test]
        public async Task PhonemizeWithProsodyAsync_AfterDispose_ThrowsObjectDisposed()
        {
            var stub = new StubG2PHandler("en",
                phonemes: new[] { "a" });
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler> { ["en"] = stub }
            };
            var mp = new MultilingualPhonemizer(options);
            await mp.InitializeAsync();
            mp.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await mp.PhonemizeWithProsodyAsync("hello"));
        }

        // ── Handler returning empty phonemes is skipped gracefully ───────────

        [Test]
        public async Task PhonemizeWithProsodyAsync_HandlerReturnsEmpty_GracefullySkips()
        {
            var stub = new StubG2PHandler("en",
                phonemes: Array.Empty<string>());
            var options = new MultilingualPhonemizerOptions
            {
                Languages = new List<string> { "en" },
                DefaultLatinLanguage = "en",
                Handlers = new Dictionary<string, ILanguageG2PHandler> { ["en"] = stub }
            };
            var mp = new MultilingualPhonemizer(options);
            await mp.InitializeAsync();

            var result = await mp.PhonemizeWithProsodyAsync("hello");

            Assert.AreEqual(1, stub.ProcessCallCount,
                "Handler should still be called");
            Assert.AreEqual(0, result.Phonemes.Length,
                "Result should have no phonemes when handler returns empty");

            mp.Dispose();
        }

        // ── Helper: disposable stub that tracks Dispose calls ───────────────

        private sealed class TrackingDisposableStub : ILanguageG2PHandler
        {
            public string LanguageCode { get; }
            public bool IsInitialized { get; private set; }
            public bool IsDisposed { get; private set; }

            public TrackingDisposableStub(string languageCode)
            {
                LanguageCode = languageCode;
            }

            public Task InitializeAsync(
                System.Threading.CancellationToken cancellationToken = default)
            {
                IsInitialized = true;
                return Task.CompletedTask;
            }

            public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
            {
                return (Array.Empty<string>(), Array.Empty<int>(),
                    Array.Empty<int>(), Array.Empty<int>());
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
