# Basic TTS Demo

Unity向けのテキスト読み上げ（TTS）機能の基本的なデモシーンです。

## 必要な環境

### Unity バージョン
- Unity 6000.0.55f1 以降

### 入力システムについて

**重要**: このデモシーンはUnity Input Systemを使用しています。

正常に動作させるには、プロジェクトにInput Systemパッケージがインストールされ、Active Input HandlingがInput System（またはBoth）に設定されている必要があります。

#### セットアップ手順

1. Window > Package ManagerでInput Systemパッケージをインストール
2. Edit > Project Settings > Player
3. Active Input Handling を "Input System" または "Both" に設定
4. Unityを再起動（設定変更後に必要）

#### トラブルシューティング

**UIボタンがクリックできない場合：**

Input Systemが正しくセットアップされていることを確認してください。Input Manager（レガシー）のみの環境では、このデモシーンは動作しません。

## デモの機能

### 日本語音声合成
- OpenJTalkを使用した高品質な日本語音声合成
- 漢字、ひらがな、カタカナ、英数字の混在テキストに対応

### 英語音声合成  
- CMU辞書を使用した英語音声合成
- 正確な発音記号変換

### UI機能
- テキスト入力フィールド
- 言語選択ドロップダウン
- 音声生成ボタン
- 生成状況の表示

## 使い方

1. デモシーンを開く
2. テキスト入力フィールドに読み上げたいテキストを入力
3. 言語を選択（日本語/英語）
4. 「Generate Speech」ボタンをクリック
5. 音声が自動的に再生されます

## 注意事項

- 初回実行時は辞書データの読み込みに時間がかかる場合があります
- Android実行時は自動的に辞書データが展開されます
- WebGLビルドには対応していません

## 依存関係

- uPiperコアライブラリ
- Unity AI Inference Engine
- Unity Input System
- TextMeshPro（日本語フォント表示用）

## ライセンス

Apache License 2.0