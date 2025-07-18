# C# Linter セットアップガイド

> **注意**: 現在、既存コードのフォーマット修正中のため、GitHub Actionsでのフォーマットチェックはスキップされるよう設定されています。新規コードは`.editorconfig`のルールに従ってください。
>
> **重要**: 以下のルール違反はエラーとして扱われ、CIが失敗します：
> - アクセシビリティ修飾子の欠如（private, public等の明示が必要）
> - privateフィールドの命名規則違反（`_`プレフィックスが必要）
> - private staticフィールドの命名規則違反（`s_`プレフィックスが必要）

このドキュメントでは、uPiperプロジェクトに導入したC#の静的解析ツール（linter）の設定について説明します。

## 概要

以下のツールを使用してC#コードの品質を保証します：

- **dotnet format**: .NET SDKに含まれる公式のコードフォーマッター
- **EditorConfig**: コードスタイルの定義
- **GitHub Actions**: PRでの自動チェック

## EditorConfig

プロジェクトルートに`.editorconfig`ファイルを配置しています。このファイルには以下の設定が含まれています：

### 基本設定
- インデント: スペース4文字
- 文字コード: UTF-8
- 改行コード: LF
- ファイル末尾の改行: あり
- 行末の空白: 削除

### C#固有の設定
- 命名規則（フィールド、プロパティ、メソッドなど）
  - privateフィールド: `_camelCase`（例: `_openjtalkHandle`）
  - private staticフィールド: `s_camelCase`（例: `s_instanceCount`）
  - publicフィールド: `PascalCase`
  - const: `PascalCase`
- アクセシビリティ修飾子の強制（private, public, protected等を明示）
- コードスタイル（varの使用、式形式のメンバーなど）
- フォーマット（改行、インデント、スペースなど）

### Unity固有のアナライザー
Microsoft.Unity.Analyzersの警告レベルを設定：
- UNT0001〜UNT0032: Unity特有のコードパターンに関する警告

## GitHub Actions

`.github/workflows/dotnet-format.yml`でPR時の自動チェックを設定しています。

### ワークフローの動作
1. PRが作成または更新された時に実行
2. C#ファイル、プロジェクトファイル、EditorConfigが変更された場合のみ実行
3. `dotnet format`コマンドでコードフォーマットをチェック
4. フォーマットエラーがある場合はPRのチェックが失敗

## 使用方法

### ローカルでのフォーマット確認

```bash
# フォーマットのチェック（変更なし）
dotnet format --verify-no-changes

# フォーマットの適用
dotnet format
```

### Visual Studio / Visual Studio Code

EditorConfigに対応したエディタを使用している場合、自動的に設定が適用されます。

### JetBrains Rider

Riderは標準でEditorConfigをサポートしています。プロジェクトを開くと自動的に設定が読み込まれます。

## トラブルシューティング

### フォーマットエラーの修正

PRでフォーマットチェックが失敗した場合：

1. ローカルで`dotnet format`を実行
2. 変更をコミット
3. PRにプッシュ

### 特定のルールの無効化

特定の行でルールを無効化する場合：

```csharp
#pragma warning disable IDE0051 // Remove unused private member
private void UnusedMethod() { }
#pragma warning restore IDE0051
```

## 参考リンク

- [EditorConfig for .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options)
- [dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
- [Microsoft.Unity.Analyzers](https://github.com/microsoft/Microsoft.Unity.Analyzers)