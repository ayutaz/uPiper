# Phase 2: Android実装技術調査レポート

## エグゼクティブサマリー

Phase 2のAndroid実装に向けた技術調査を完了しました。基本的なビルド環境は既に準備されており、Unity側のプラットフォーム対応基盤も存在します。主な作業は、既存コンポーネントのAndroid対応と、Android固有の制約への対応です。

## 調査結果

### 1. 現在の実装状況

#### ✅ 既に実装済み
- **ビルドインフラ**
  - Dockerベースのビルド環境（Dockerfile.android）
  - Android NDKツールチェーン（toolchain-android.cmake）
  - マルチABIビルドスクリプト（build_android.sh/bat）
  
- **Unity側の基盤**
  - PlatformHelper.csによるプラットフォーム判定
  - Android用ライブラリ拡張子（.so）の定義
  - 基本的なアーキテクチャ判定（arm64-v8aデフォルト）

#### ❌ 未実装・要対応
- **ネイティブライブラリ**
  - CMakeLists.txtのAndroid対応
  - libopenjtalk_wrapper.soの生成
  - Unityプラグインディレクトリへの配置

- **Unity統合**
  - P/InvokeマクロへのAndroid追加
  - StreamingAssetsからの辞書読み込み
  - AndroidManifest.xml（必要に応じて）

### 2. 技術的課題と解決策

#### 課題1: P/Invoke vs JNI
- **現状**: P/InvokeはAndroidで無効化されている
- **解決策**: Unity 2018.2以降、AndroidでもP/Invokeが使用可能
- **実装方針**: 既存のP/Invokeコードを活用し、Android対応を追加

#### 課題2: 辞書ファイルアクセス
- **現状**: ファイルシステム直接アクセスを前提
- **制約**: AndroidのStreamingAssetsは圧縮されAPK内に格納
- **解決策**: 
  ```csharp
  // Android用の辞書読み込み実装
  #if UNITY_ANDROID && !UNITY_EDITOR
  using (UnityWebRequest www = UnityWebRequest.Get(streamingAssetsPath))
  {
      yield return www.SendWebRequest();
      // バイト配列として辞書データを取得
  }
  #endif
  ```

#### 課題3: メモリ管理
- **制約**: モバイルデバイスの限られたメモリ
- **現状**: 辞書データ約50MB（非圧縮）
- **解決策**: 
  - 辞書データの圧縮（20MB程度に削減可能）
  - 遅延ロード実装
  - 不要時のアンロード機構

### 3. 実装推奨事項

#### 優先度: 高
1. **CMakeLists.txtのAndroid対応追加**
   ```cmake
   elseif(ANDROID)
       set(PLATFORM_NAME "android")
       set(CMAKE_POSITION_INDEPENDENT_CODE ON)
       # Android固有の設定
   ```

2. **P/Invokeマクロの更新**
   ```csharp
   #define ENABLE_PINVOKE
   ```
   条件にUNITY_ANDROIDを追加

3. **辞書ローダーのAndroid対応**
   - 非同期読み込みインターフェースの追加
   - プラットフォーム別実装の分離

#### 優先度: 中
1. **プラグイン配置構造**
   ```
   Assets/uPiper/Plugins/Android/
   ├── libs/
   │   ├── arm64-v8a/
   │   │   └── libopenjtalk_wrapper.so
   │   ├── armeabi-v7a/
   │   │   └── libopenjtalk_wrapper.so
   │   └── x86_64/
   │       └── libopenjtalk_wrapper.so
   └── dictionary/
   ```

2. **メモリ最適化**
   - 辞書データの圧縮実装
   - キャッシュサイズの動的調整

#### 優先度: 低
1. **AndroidManifest.xml**
   - 現時点では不要（特別なパーミッション不要）
   - 将来的に外部ストレージアクセスが必要な場合に追加

### 4. リスク評価

| リスク | 影響度 | 発生確率 | 対策 |
|--------|--------|----------|------|
| JNI呼び出しオーバーヘッド | 中 | 低 | P/Invoke使用で回避 |
| メモリ不足 | 高 | 中 | 段階的ロード実装 |
| デバイス互換性 | 中 | 低 | API Level 21以上に限定 |
| APKサイズ増加 | 低 | 高 | ProGuard/R8での最適化 |

### 5. 実装スケジュール見積もり

既存のビルドインフラとUnity基盤を活用することで、当初の15人日から**10人日**に短縮可能：

1. **ビルド環境構築**: 1人日（既に大部分完了）
2. **ネイティブライブラリビルド**: 2人日
3. **Unity統合**: 3人日
4. **最適化**: 2人日
5. **テスト・CI/CD**: 2人日

## 結論

Phase 2のAndroid実装は、既存の基盤を活用することで効率的に進められます。主な技術的課題は解決可能であり、実装リスクは低いと評価します。

## 次のアクション

1. CMakeLists.txtへのAndroid設定追加
2. build_android.shを使用したテストビルド実行
3. Unity側のP/Invoke有効化とテスト
4. CI/CDパイプラインへの統合

準備が整い次第、実装フェーズに移行可能です。