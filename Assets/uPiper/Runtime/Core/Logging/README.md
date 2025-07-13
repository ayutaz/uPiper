# uPiper Logging

uPiperでは統一されたログシステムを提供するため、軽量なログラッパーを実装しています。

## 使用方法

```csharp
using uPiper.Core.Logging;

// ログ出力
PiperLogger.LogInfo("Information message");
PiperLogger.LogWarning("Warning message");
PiperLogger.LogError("Error message");
PiperLogger.LogDebug("Debug message");

// パラメータ付きログ
PiperLogger.LogInfo("Loading model {0} for language {1}", modelPath, language);
```

## ログレベル

- **Debug**: 詳細なデバッグ情報（開発時のみ）
- **Info**: 一般的な情報
- **Warning**: 警告（動作は継続）
- **Error**: エラー（処理失敗）
- **Fatal**: 致命的エラー（復旧不可）

## 設定

```csharp
// 最小ログレベルの変更
PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Warning);

// 初期化（オプション - 自動的に行われます）
PiperLogger.Initialize();
```

## Unity Debug.Logとの違い

- **統一されたプレフィックス**: 全てのログに[uPiper]プレフィックスが付く
- **パフォーマンス**: LogDebugは条件付きコンパイルで本番ビルドでは除外
- **ログレベルフィルタリング**: 動的にログレベルを変更可能
- **シンプルな実装**: Unity標準のDebug.Logをラップ

## 移行ガイド

```csharp
// Before
Debug.Log("Loading model: " + modelPath);
Debug.LogWarning($"Sample rate {rate}Hz is non-standard");
Debug.LogError("Failed to load: " + error.Message);

// After
PiperLogger.LogInfo("Loading model: {0}", modelPath);
PiperLogger.LogWarning("Sample rate {0}Hz is non-standard", rate);
PiperLogger.LogError("Failed to load: {0}", error.Message);
```