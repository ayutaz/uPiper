# uPiper Logging

uPiperではUnity Loggingパッケージ（com.unity.logging）を使用して統一されたログシステムを提供します。

## 使用方法

```csharp
using uPiper.Core.Logging;

// ログ出力
PiperLogger.Logger.LogInfo("Information message");
PiperLogger.Logger.LogWarning("Warning message");
PiperLogger.Logger.LogError("Error message");
PiperLogger.Logger.LogDebug("Debug message");

// 構造化ログ
PiperLogger.Logger.LogInfo("Loading model {ModelPath} for language {Language}", 
    modelPath, language);
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
PiperLogger.SetMinimumLevel(LogLevel.Warning);

// カスタム設定
var config = new LoggerConfig();
config.MinimumLevel = LogLevel.Debug;
config.WriteTo.UnityDebugLog();
config.WriteTo.File("upiper.log");
PiperLogger.Configure(config);
```

## Unity Debug.Logとの違い

- **構造化ログ**: パラメータを個別に渡せる
- **パフォーマンス**: 条件付きコンパイルで本番ビルドでの負荷を削減
- **フィルタリング**: ログレベルによる動的フィルタリング
- **拡張性**: カスタムシンクの追加が可能

## 移行ガイド

```csharp
// Before
Debug.Log("Loading model: " + modelPath);
Debug.LogWarning($"Sample rate {rate}Hz is non-standard");
Debug.LogError("Failed to load: " + error.Message);

// After
PiperLogger.Logger.LogInfo("Loading model: {ModelPath}", modelPath);
PiperLogger.Logger.LogWarning("Sample rate {Rate}Hz is non-standard", rate);
PiperLogger.Logger.LogError("Failed to load: {Error}", error.Message);
```