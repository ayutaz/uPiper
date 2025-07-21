# OpenJTalk Native Library Build Investigation

## Phase 1.7.1 調査結果

### 1. OpenJTalk概要

OpenJTalkは日本語テキスト解析・音声合成システムで、以下のコンポーネントから構成されています：

- **Mecab**: 形態素解析エンジン
- **mecab-naist-jdic**: 日本語辞書
- **HTS Engine**: 音声合成エンジン（今回は音素化のみ使用）

### 2. uPiper統合での要件

#### 必要な機能
- テキストから音素列への変換
- 音素タイミング情報の取得
- メモリ効率的な実装

#### 不要な機能
- 音声合成機能（HTS Engine API）
- 音声波形生成
- F0/スペクトル解析

### 3. 軽量化戦略

#### アプローチ1: 最小限のOpenJTalk実装
```c
// 必要な機能のみを抽出したラッパー
typedef struct {
    void* mecab;
    void* njd;
    void* jpcommon;
} OpenJTalkPhonemizer;

// シンプルなAPI
const char* openjtalk_phonemize(OpenJTalkPhonemizer* phonemizer, const char* text);
```

#### アプローチ2: Mecab直接利用
- OpenJTalkの上位レイヤーをバイパス
- Mecabの解析結果から直接音素を生成
- サイズを大幅に削減可能

### 4. プラットフォーム別考慮事項

#### Windows
- Visual Studio 2019/2022でのビルド
- 文字コードはUTF-8で統一
- DLL export/importマクロの定義が必要

#### Linux
- GCC/Clangでのビルド
- POSIXスレッド対応
- 共有ライブラリ（.so）として出力

#### macOS（将来）
- Universal Binary対応
- Code signing考慮

### 5. 依存関係

#### 必須
- Mecab（形態素解析）
- naist-jdic（辞書データ）

#### オプション（除外可能）
- HTS Engine API
- Speech Signal Processing Toolkit (SPTK)

### 6. ビルドシステム設計

```
Native/OpenJTalk/
├── CMakeLists.txt          # メインビルド設定
├── src/
│   ├── openjtalk_wrapper.c # C API実装
│   └── phonemizer.c        # 音素化ロジック
├── include/
│   └── openjtalk_wrapper.h # 公開API
├── build/
│   ├── windows/            # Windows用ビルド出力
│   └── linux/              # Linux用ビルド出力
└── third_party/
    ├── mecab/              # Mecabソース（サブモジュール）
    └── dict/               # 辞書データ
```

### 7. API設計

```c
// openjtalk_wrapper.h
#ifdef __cplusplus
extern "C" {
#endif

// 初期化・終了
void* openjtalk_create(const char* dict_path);
void openjtalk_destroy(void* handle);

// 音素化
typedef struct {
    char* phonemes;      // 音素列（スペース区切り）
    int* phoneme_ids;    // 音素ID配列
    int phoneme_count;   // 音素数
    float* durations;    // 各音素の継続時間（秒）
} PhonemeResult;

PhonemeResult* openjtalk_phonemize(void* handle, const char* text);
void openjtalk_free_result(PhonemeResult* result);

// エラーハンドリング
const char* openjtalk_get_last_error(void* handle);

#ifdef __cplusplus
}
#endif
```

### 8. メモリ管理戦略

- 内部でメモリプールを使用
- 頻繁なmalloc/freeを避ける
- 結果は呼び出し側で明示的に解放

### 9. サイズ目標

- Windows DLL: < 5MB
- Linux SO: < 5MB
- 辞書データ（圧縮後）: < 10MB

### 10. 次のステップ

1. CMakeLists.txt作成（Phase 1.7.2）
2. スタブ実装作成（Phase 1.7.3）
3. ビルドスクリプト作成（Phase 1.7.4-5）
4. テストプログラム作成（Phase 1.7.6）