# Phase 5: iOS Support - 技術調査と実現性検証

## 1. 技術調査結果

### 1.1 Unity iOS Native Plugin仕様（2024-2025最新）

#### 基本アーキテクチャ
- **静的ライブラリ形式**: iOSでは動的ライブラリ（.dylib）ではなく静的ライブラリ（.a）を使用
- **DllImport指定**: iOSでは`__Internal`を使用（他プラットフォームと異なる）
- **ファイル配置**: `Assets/Plugins/iOS/`ディレクトリに配置
- **Xcodeプロジェクト統合**: Unityがビルド時に自動的にXcodeプロジェクトに統合

#### P/Invoke実装例
```csharp
#if UNITY_IOS
    private const string LIBRARY_NAME = "__Internal";  // iOS固有の指定
#else
    private const string LIBRARY_NAME = "openjtalk_wrapper";
#endif

[DllImport(LIBRARY_NAME)]
private static extern IntPtr openjtalk_create(string dict_path);
```

### 1.2 OpenJTalk iOS移植の課題と解決策

#### 技術的課題
1. **MeCab依存**: OpenJTalkはMeCabに依存しており、MeCabのiOS対応が必要
2. **辞書ファイルサイズ**: NAIST-JDIC辞書が約100MB（圧縮時約30MB）
3. **メモリ制約**: iOSアプリのメモリ使用量制限への対応
4. **ファイルシステム**: iOS固有のサンドボックス環境での辞書アクセス

#### 既存のiOS対応事例
- **iPhone-libmecab**: MeCabのiOS静的ライブラリ実装（最新版はSwift 5対応）
- **mecab-naist-jdic-utf-8**: CocoaPods/npmパッケージとして提供
- **成功事例**: 複数の日本語解析アプリでMeCab/OpenJTalkが動作実績あり

### 1.3 ビルドツールチェーン

#### CMake iOS クロスコンパイル
```bash
# オプション1: CMAKE_SYSTEM_NAME使用
cmake -B build \
  -G Xcode \
  -DCMAKE_SYSTEM_NAME=iOS \
  -DCMAKE_OSX_ARCHITECTURES=arm64 \
  -DCMAKE_OSX_DEPLOYMENT_TARGET=11.0

# オプション2: ios-cmake toolchain使用（推奨）
cmake -B build \
  -G Xcode \
  -DCMAKE_TOOLCHAIN_FILE=ios.toolchain.cmake \
  -DPLATFORM=OS64  # arm64デバイス用
```

#### ios-cmakeツールチェーン
- **GitHub**: leetal/ios-cmake
- **特徴**: 
  - iOS/tvOS/watchOS/macOS対応
  - シミュレータサポート
  - Universal Binary作成
  - Bitcodeサポート

## 2. 実現性評価

### 2.1 技術的実現性: ✅ 高い

#### 根拠
1. **先行事例**: MeCab/OpenJTalkのiOS実装が複数存在
2. **Unity対応**: Unity iOS Native Plugin仕様が明確
3. **ツールチェーン**: CMakeによるiOSビルドが確立
4. **アーキテクチャ**: arm64のみ対応で十分（iPhone 5s以降すべて）

### 2.2 実装難易度評価

| 項目 | 難易度 | 理由 |
|------|--------|------|
| OpenJTalkライブラリビルド | 中 | CMake設定の調整が必要 |
| Unity統合 | 低 | 標準的なiOSプラグイン実装 |
| 辞書管理 | 中 | StreamingAssetsからの読み込み実装 |
| メモリ最適化 | 高 | 辞書の部分読み込みが必要 |
| App Store対応 | 低 | 静的ライブラリなので問題なし |

### 2.3 リスクと対策

1. **辞書サイズ問題**
   - リスク: アプリサイズが100MB増加
   - 対策: 初回起動時にダウンロード、または軽量辞書作成

2. **メモリ使用量**
   - リスク: 辞書読み込みでメモリ不足
   - 対策: メモリマップドファイル使用、辞書の部分読み込み

3. **ビルド複雑性**
   - リスク: 依存ライブラリのビルド失敗
   - 対策: プリビルドバイナリの提供

## 3. 推奨アプローチ

### 3.1 段階的実装戦略

#### Phase 1: 最小構成での動作確認（2日）
1. MeCabのiOS静的ライブラリビルド
2. OpenJTalkの最小構成ビルド
3. 簡単なテストアプリで動作確認

#### Phase 2: Unity統合（3日）
1. 静的ライブラリのUnityプラグイン化
2. P/Invoke実装
3. 辞書ファイル管理

#### Phase 3: 最適化（2日）
1. メモリ使用量最適化
2. 辞書圧縮・展開
3. パフォーマンステスト

### 3.2 代替案

#### 軽量実装オプション
1. **辞書のクラウド化**: 辞書をサーバーに配置し、必要な部分のみ取得
2. **軽量辞書**: 頻出語のみの小規模辞書作成
3. **ハイブリッド**: 基本辞書＋オンデマンドダウンロード

## 4. 実装計画詳細

### 4.1 開発環境要件
- **macOS**: Monterey以降（Xcode 14+）
- **Xcode**: 14.0以降
- **CMake**: 3.20以降
- **iOS SDK**: 11.0以降
- **Unity**: 2021.3 LTS以降

### 4.2 成果物
1. **libopenjtalk_wrapper.a**: iOS用静的ライブラリ
2. **UnityプラグインWrapper**: C#バインディング
3. **ビルドスクリプト**: build_ios.sh
4. **ドキュメント**: iOS統合ガイド

### 4.3 テスト計画
1. **単体テスト**: ネイティブライブラリレベル
2. **統合テスト**: Unity Editor（iOS Simulator）
3. **実機テスト**: iPhone実機での動作確認
4. **パフォーマンステスト**: メモリ使用量、処理速度

## 5. Unity固有の実装要件

### 5.1 Unity AI Inference Engine（Sentis）iOS対応
- **対応状況**: ✅ 完全対応（2024-2025）
- **特徴**:
  - モバイルを含む全Unity対応プラットフォームで動作
  - オンデバイス推論（クラウド不要）
  - ローカル実行により低レイテンシ実現
  - ONNX形式のモデルサポート

### 5.2 iOS StreamingAssets アクセス

#### 技術的制約
- **読み取り専用**: StreamingAssetsは実行時に書き込み不可
- **特殊なアクセス方法**: iOSでは`UnityWebRequest`を使用する必要がある
- **パス指定**: `file://`プレフィックスが必要な場合あり

#### 実装例
```csharp
#if UNITY_IOS
IEnumerator LoadDictionary(string fileName)
{
    string filePath = Path.Combine(Application.streamingAssetsPath, fileName);
    
    // iOSではUnityWebRequestを使用
    using (UnityWebRequest request = UnityWebRequest.Get(filePath))
    {
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            byte[] dictionaryData = request.downloadHandler.data;
            // 辞書データの処理
        }
    }
}
#endif
```

### 5.3 メモリマネジメント戦略

#### 辞書ファイルの扱い
1. **初期読み込み**: StreamingAssetsから一時ディレクトリへコピー
2. **メモリマップド**: 大きな辞書ファイルはメモリマップドファイルとして扱う
3. **段階的読み込み**: 必要な部分のみメモリに展開

#### 実装アプローチ
```csharp
private async Task<string> PrepareDictionaryPath()
{
    string sourcePath = Path.Combine(Application.streamingAssetsPath, "naist_jdic");
    string destPath = Path.Combine(Application.persistentDataPath, "naist_jdic");
    
    // 初回起動時のみコピー
    if (!Directory.Exists(destPath))
    {
        await CopyDictionaryFromStreamingAssets(sourcePath, destPath);
    }
    
    return destPath;
}
```

## 6. 実装上の注意点

### 6.1 iOS固有の制約

1. **App Transport Security (ATS)**
   - ローカルファイルアクセスには影響なし
   - 将来的なクラウド辞書化時は考慮必要

2. **バックグラウンド制限**
   - 音声合成処理は前景でのみ実行
   - バックグラウンドタスクとして登録不可

3. **メモリ警告への対応**
   - `didReceiveMemoryWarning`での辞書解放
   - 必要時の再読み込み機構

### 6.2 ビルド設定

#### Info.plist設定
```xml
<key>UIFileSharingEnabled</key>
<false/>
<key>LSSupportsOpeningDocumentsInPlace</key>
<false/>
```

#### Player Settings
- **Architecture**: ARM64
- **Target minimum iOS Version**: 11.0
- **Api Compatibility Level**: .NET Standard 2.1

## 7. 結論

### 技術的実現性: ✅ 実現可能

OpenJTalkのiOS対応は技術的に実現可能です。主な根拠：

1. **先行事例の存在**: MeCab/OpenJTalkのiOS実装が実績あり
2. **明確な実装パス**: CMake + ios-cmake toolchainによる標準的なビルド
3. **Unity対応**: 静的ライブラリによる標準的なiOSプラグイン実装
4. **AI推論エンジン**: Unity AI Inference EngineがiOS完全対応
5. **リソース管理**: StreamingAssetsからの辞書読み込み方法が確立

### 推奨事項

1. **段階的アプローチ**: まず最小構成で動作確認後、最適化
2. **辞書管理**: 初期は完全バンドル、後日クラウド化検討
3. **プリビルドバイナリ**: 開発者向けにビルド済みライブラリ提供
4. **メモリ最適化**: 辞書の段階的読み込み実装

### 次のステップ

1. macOS環境でのビルド環境構築
2. ios-cmake toolchainのセットアップ
3. MeCab iOS静的ライブラリのビルド
4. OpenJTalk最小構成のビルド
5. Unityテストプロジェクトでの動作確認
6. StreamingAssetsからの辞書読み込み実装
7. メモリ管理とパフォーマンス最適化