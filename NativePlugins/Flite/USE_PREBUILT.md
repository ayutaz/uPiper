# Fliteプリビルドライブラリ使用手順

> **注記（レガシー）**: v1.4.0以降、英語G2Pは DotNetG2P.English（純粋C#）へ移行済みで、このネイティブFliteライブラリは現行アーキテクチャ（dot-net-g2p）では使用されていません（`README.md` 参照）。以下の手順は歴史的な参考情報です。

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