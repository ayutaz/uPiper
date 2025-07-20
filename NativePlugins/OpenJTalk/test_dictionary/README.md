# Test Dictionary for OpenJTalk

This directory contains minimal dictionary files for testing purposes.
These files are not suitable for production use but allow tests to run without the full mecab-naist-jdic dictionary.

## Files

- `sys.dic` - Minimal system dictionary
- `unk.dic` - Minimal unknown word dictionary
- `matrix.bin` - Connection cost matrix
- `char.bin` - Character type definitions
- `left-id.def` - Left context IDs
- `right-id.def` - Right context IDs
- `pos-id.def` - Part-of-speech IDs
- `rewrite.def` - Rewrite rules

## Usage

Set the dictionary path to this directory when running tests:
```bash
./test_openjtalk ../test_dictionary
```