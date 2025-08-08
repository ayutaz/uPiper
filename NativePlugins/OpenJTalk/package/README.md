## WebAssemblyでOpen JTalkをビルドしてみました

## 概要

フリーの日本語音声合成エンジン [OpenJTalk](http://open-jtalk.sourceforge.net/) を
[Emscripten](https://emscripten.org/)というWebAssemblyのコンパイラツールチェーン
を使用してビルドしてみました。

現状では、Node.js環境で動くコマンドラインツールとしてビルドしています。

## インストール

```sh
npm -g install wasm_open_jtalk
```


## 実行

`open_jtalk.js` コマンドを実行します。コマンド引数の詳細は本家のOpen JTalkの方を参照ください。

```sh
open_jtalk.js
```

## ビルド

ビルドは以下の環境で試しました。

* OS: Ubuntu 20.04
* Emscripten
    - emcc: 2.0.14
    - clang: 13.0.0
    - Target: wasm32-unknown-emscripten

### ビルド手順

このリポジトリのルートディレクトリで、以下のコマンドを実行します。

```sh
make install-emsdk
make install-hts_engine_API
make install-open_jtalk
```

これで、`js`フォルダにopen_jtalk.jsおよびopen_jtalk.wasmができます。
このビルドしたopen_jtalk.jsを実行する例を以下に記載します(Node.jsが必要)。

```sh
cd js
# ヘルプを表示する例。コマンド引数の詳細は元々のOpen JTalkの方を参照ください
node open_jtalk.js --help
```

TODO: その他、ビルドに必要なパッケージ等を調査

## 参考

* [OpenJTalk](http://open-jtalk.sourceforge.net/)
* [Emscripten](https://emscripten.org/)
* [Emscripten いじってた時のメモ](https://gist.github.com/xl1/d263b41661d262b613935525c3093e81)
* https://github.com/r9y9/open_jtalk
* https://github.com/r9y9/hts_engine_API
