# OpenJTalk WASM ビルド用 Dockerfile (簡易版)
FROM emscripten/emsdk:3.1.39

# 必要なツールをインストール
RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    git \
    python3 \
    python3-pip \
    nodejs \
    npm \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Emscripten環境変数設定
ENV EMSDK=/emsdk
ENV EM_CONFIG=/emsdk/.emscripten
ENV EMSCRIPTEN=/emsdk/upstream/emscripten

# 作業ディレクトリ設定
WORKDIR /workspace/openjtalk-wasm

# ソースコードをコピー
COPY openjtalk_wasm.c ./
COPY build_wasm_docker.sh ./
COPY test-openjtalk.mjs ./

# ビルドスクリプトを実行可能にする
RUN chmod +x build_wasm_docker.sh

# ビルド実行
CMD ["/workspace/openjtalk-wasm/build_wasm_docker.sh"]