# Android パフォーマンス最適化計画

## 概要

Phase 2完了後のAndroidパフォーマンス最適化を実施します。現在の基本実装は動作していますが、モバイルデバイス向けに更なる最適化が必要です。

## 現状分析

### 現在のパフォーマンス指標
- **ライブラリサイズ**: 全ABI合計約20MB（各ABI 4.9-5.5MB）
- **辞書データ**: 展開後約50MB
- **初期化時間**: 初回起動時は辞書展開のため数秒かかる
- **メモリ使用量**: 辞書ロード後約50-70MB追加

### ボトルネック
1. **辞書データのサイズと展開時間**
   - 50MBの辞書データ展開に時間がかかる
   - メモリに全て保持している

2. **ネイティブライブラリサイズ**
   - 各ABIで約5MBは大きい
   - APKサイズへの影響が大きい

3. **文字列処理のオーバーヘッド**
   - UTF-8/UTF-16変換が頻繁
   - P/Invoke境界での文字列マーシャリング

## 最適化戦略

### 1. 辞書データの最適化（優先度: 高）

#### 1.1 辞書データの圧縮
- **手法**: 
  - ZIP圧縮での配布（StreamingAssets）
  - 展開時の段階的処理
  - 必要な部分のみメモリロード
- **期待効果**: 
  - APKサイズ20-30MB削減
  - 初期化時間の短縮

#### 1.2 辞書の遅延ロード
- **手法**:
  - 必要な辞書エントリのみロード
  - LRUキャッシュの実装
  - メモリマップファイルの検討
- **期待効果**:
  - メモリ使用量50%削減
  - 初期化時間を1秒以内に

### 2. ネイティブライブラリの最適化（優先度: 中）

#### 2.1 ビルドフラグの最適化
```cmake
# サイズ最適化フラグ
set(CMAKE_CXX_FLAGS_RELEASE "-Os -ffunction-sections -fdata-sections")
set(CMAKE_SHARED_LINKER_FLAGS "-Wl,--gc-sections -Wl,--strip-all")

# LTOの有効化
set(CMAKE_INTERPROCEDURAL_OPTIMIZATION TRUE)
```

#### 2.2 不要な機能の削除
- HTSEngine関連コードの完全削除
- デバッグシンボルの除去
- 未使用関数の削除

### 3. P/Invoke最適化（優先度: 中）

#### 3.1 文字列処理の最適化
```csharp
// Before: 文字列マーシャリング
[DllImport("openjtalk_wrapper")]
private static extern IntPtr openjtalk_analyze(string text);

// After: バイト配列で直接渡す
[DllImport("openjtalk_wrapper")]
private static extern IntPtr openjtalk_analyze_utf8(byte[] utf8Text, int length);
```

#### 3.2 バッチ処理の実装
- 複数のテキストを一度に処理
- ネイティブ側でのメモリプール

### 4. Unity側の最適化（優先度: 高）

#### 4.1 非同期処理の改善
```csharp
public class OptimizedAndroidPathResolver
{
    private static Task<bool> _dictionaryExtractionTask;
    
    // アプリ起動時に非同期で辞書展開を開始
    public static void PreloadDictionaryAsync()
    {
        if (_dictionaryExtractionTask == null)
        {
            _dictionaryExtractionTask = Task.Run(() => ExtractDictionaryFiles());
        }
    }
    
    // 実際に使用する時に完了を待つ
    public static async Task<string> GetDictionaryPathAsync()
    {
        if (_dictionaryExtractionTask != null)
        {
            await _dictionaryExtractionTask;
        }
        return GetDictionaryPath();
    }
}
```

#### 4.2 キャッシュの強化
- 音素化結果のキャッシュサイズ最適化
- WeakReferenceを使用したメモリ効率化

### 5. ARM NEON最適化（優先度: 低）

#### 5.1 文字列処理のSIMD化
- UTF-8/UTF-16変換の高速化
- 音素配列処理の並列化

## 実装計画

### Phase 1: 測定とプロファイリング（0.5日）
1. Unity Profilerでの詳細測定
2. Android Studio Profilerでのネイティブ分析
3. ベンチマークテストの作成

### Phase 2: 辞書最適化（1日）
1. 辞書圧縮の実装
2. 遅延ロードの実装
3. メモリ使用量の削減

### Phase 3: ビルド最適化（0.5日）
1. CMakeフラグの調整
2. 不要コードの削除
3. サイズ測定と検証

### Phase 4: 実行時最適化（1日）
1. P/Invoke最適化
2. 非同期処理の改善
3. キャッシュ戦略の実装

### Phase 5: 検証とチューニング（0.5日）
1. パフォーマンステスト
2. 実機での検証
3. 最終調整

## 成功基準

- ✅ APKサイズ増加を50MB以下に抑える
- ✅ 初期化時間を2秒以内（2回目以降）
- ✅ メモリ使用量を50MB以下に削減
- ✅ 音素化処理を50ms以内で完了
- ✅ 低スペックデバイス（2GB RAM）での安定動作

## リスクと対策

### リスク1: 辞書圧縮による処理時間増加
- **対策**: 事前展開とキャッシュの組み合わせ
- **代替案**: 部分的な圧縮（頻出語のみ非圧縮）

### リスク2: メモリ不足によるクラッシュ
- **対策**: OutOfMemoryの適切なハンドリング
- **代替案**: 品質を落とした軽量辞書の提供

### リスク3: 古いデバイスでの互換性問題
- **対策**: Android 5.0以上を最小要件に
- **代替案**: レガシーモードの実装