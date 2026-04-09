using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual.Handlers;

namespace uPiper.Tests.Editor.Phonemizers;

/// <summary>
/// Verifies that different language G2P handlers produce distinct phoneme outputs
/// for the same Latin-script input text.
/// </summary>
[TestFixture]
public class CrossLanguagePhonemeTests
{
    [Test]
    public async Task SameLatinText_DifferentLanguage_DifferentPhonemes()
    {
        using var enHandler = new EnglishG2PHandler();
        using var esHandler = new SpanishG2PHandler();

        try
        {
            await enHandler.InitializeAsync();
        }
        catch (Exception)
        {
            Assert.Ignore("EnglishG2PHandler initialization failed (dictionary not available)");
            return;
        }

        if (!enHandler.IsInitialized)
        {
            Assert.Ignore("EnglishG2PHandler not initialized");
            return;
        }

        try
        {
            await esHandler.InitializeAsync();
        }
        catch (Exception)
        {
            Assert.Ignore("SpanishG2PHandler initialization failed (dictionary not available)");
            return;
        }

        if (!esHandler.IsInitialized)
        {
            Assert.Ignore("SpanishG2PHandler not initialized");
            return;
        }

        var (enPhonemes, _) = enHandler.Process("Hola");
        var (esPhonemes, _) = esHandler.Process("Hola");

        Assert.IsNotNull(enPhonemes, "English phonemes should not be null");
        Assert.IsNotNull(esPhonemes, "Spanish phonemes should not be null");
        Assert.Greater(enPhonemes.Length, 0, "English handler should produce phonemes for 'Hola'");
        Assert.Greater(esPhonemes.Length, 0, "Spanish handler should produce phonemes for 'Hola'");

        // English and Spanish G2P engines apply different pronunciation rules,
        // so the phoneme sequences for the same Latin text should differ.
        // Spanish treats 'h' as silent (/ola/) while English does not.
        CollectionAssert.AreNotEqual(enPhonemes, esPhonemes,
            "Same text should produce different phonemes for different languages");
    }

    [Test]
    public async Task SpanishHandler_ProducesLanguageSpecificPhonemes()
    {
        using var handler = new SpanishG2PHandler();
        try
        {
            await handler.InitializeAsync();
        }
        catch (Exception)
        {
            Assert.Ignore("SpanishG2PHandler initialization failed (dictionary not available)");
            return;
        }

        if (!handler.IsInitialized)
        {
            Assert.Ignore("SpanishG2PHandler not initialized");
            return;
        }

        var (phonemes, prosodyFlat) = handler.Process("Buenos días");

        Assert.IsNotNull(phonemes, "Phonemes should not be null");
        Assert.Greater(phonemes.Length, 0, "Spanish handler should produce phonemes");
        Assert.IsNotNull(prosodyFlat, "ProsodyFlat should not be null");
        Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
            "ProsodyFlat length must equal Phonemes.Length * 3");
    }

    [Test]
    public async Task FrenchHandler_ProducesLanguageSpecificPhonemes()
    {
        using var handler = new FrenchG2PHandler();
        try
        {
            await handler.InitializeAsync();
        }
        catch (Exception)
        {
            Assert.Ignore("FrenchG2PHandler initialization failed (dictionary not available)");
            return;
        }

        if (!handler.IsInitialized)
        {
            Assert.Ignore("FrenchG2PHandler not initialized");
            return;
        }

        var (phonemes, prosodyFlat) = handler.Process("Bonjour");

        Assert.IsNotNull(phonemes, "Phonemes should not be null");
        Assert.Greater(phonemes.Length, 0, "French handler should produce phonemes");
        Assert.IsNotNull(prosodyFlat, "ProsodyFlat should not be null");
        Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
            "ProsodyFlat length must equal Phonemes.Length * 3");
    }
}