# Fliteプリビルドライブラリ使用手順

エンコーディング問題を回避するため、以下の方法を推奨します：

## 方法1: プリビルドライブラリ

1. Flite公式リリースからバイナリをダウンロード
2. 必要な関数のみをエクスポート

## 方法2: WSL/Linux環境でビルド

```bash
# WSL or Linux
cd NativePlugins/Flite
./download_flite.sh
./build.sh
```

## 方法3: 簡易LTS実装

最小限のLTS機能のみを実装し、完全なFliteは使用しない。