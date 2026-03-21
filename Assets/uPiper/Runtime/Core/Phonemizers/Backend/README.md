# Phonemizer Backend Directory

This directory contains the phonemizer backend interface and shared types for uPiper. All G2P backends have been migrated to standalone DotNetG2P packages and are called directly from `MultilingualPhonemizer`.

## Current Files

- **IPhonemizerBackend.cs** - Interface for phonemizer backends (used by test stubs)
- **PhonemeOptions.cs** - Shared phoneme option/result types

## Architecture

G2P processing for each language is handled by the corresponding DotNetG2P package, called directly from `MultilingualPhonemizer` (in `../Multilingual/`):

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