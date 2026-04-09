# Phonemizer Backend Directory

This directory contains shared types for uPiper phonemizer backends. All G2P backends have been migrated to standalone DotNetG2P packages and are called via `ILanguageG2PHandler` implementations in `../Multilingual/Handlers/`.

## Current Files

- **PhonemeOptions.cs** - Shared phoneme option/result types (`PhonemeResult`, `PhonemeOptions`)

## Architecture

G2P processing for each language is handled by the corresponding DotNetG2P package, called via per-language `ILanguageG2PHandler` implementations from `MultilingualPhonemizer` (in `../Multilingual/`):

| Language | DotNetG2P Package |
|----------|-------------------|
| Japanese | DotNetG2P.MeCab |
| English | DotNetG2P.English |
| Spanish | DotNetG2P.Spanish |
| French | DotNetG2P.French |
| Portuguese | DotNetG2P.Portuguese |
| Chinese | DotNetG2P.Chinese |
| Korean | DotNetG2P.Korean |

## License
All backends use commercial-friendly licenses (MIT/BSD).
