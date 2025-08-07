using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// WebGL用の簡易日本語音素化実装
    /// OpenJTalkのWebAssemblyモジュールが正しく動作しない場合の代替実装
    /// </summary>
    public class WebGLJapanesePhonemizer : BasePhonemizer
    {
        private static readonly Dictionary<char, string> KanaToPhoneme = new Dictionary<char, string>
        {
            // ひらがな
            {'あ', "a"}, {'い', "i"}, {'う', "u"}, {'え', "e"}, {'お', "o"},
            {'か', "k a"}, {'き', "k i"}, {'く', "k u"}, {'け', "k e"}, {'こ', "k o"},
            {'が', "g a"}, {'ぎ', "g i"}, {'ぐ', "g u"}, {'げ', "g e"}, {'ご', "g o"},
            {'さ', "s a"}, {'し', "s i"}, {'す', "s u"}, {'せ', "s e"}, {'そ', "s o"},
            {'ざ', "z a"}, {'じ', "z i"}, {'ず', "z u"}, {'ぜ', "z e"}, {'ぞ', "z o"},
            {'た', "t a"}, {'ち', "t i"}, {'つ', "ts u"}, {'て', "t e"}, {'と', "t o"},
            {'だ', "d a"}, {'ぢ', "d i"}, {'づ', "d u"}, {'で', "d e"}, {'ど', "d o"},
            {'な', "n a"}, {'に', "n i"}, {'ぬ', "n u"}, {'ね', "n e"}, {'の', "n o"},
            {'は', "h a"}, {'ひ', "h i"}, {'ふ', "h u"}, {'へ', "h e"}, {'ほ', "h o"},
            {'ば', "b a"}, {'び', "b i"}, {'ぶ', "b u"}, {'べ', "b e"}, {'ぼ', "b o"},
            {'ぱ', "p a"}, {'ぴ', "p i"}, {'ぷ', "p u"}, {'ぺ', "p e"}, {'ぽ', "p o"},
            {'ま', "m a"}, {'み', "m i"}, {'む', "m u"}, {'め', "m e"}, {'も', "m o"},
            {'や', "y a"}, {'ゆ', "y u"}, {'よ', "y o"},
            {'ら', "r a"}, {'り', "r i"}, {'る', "r u"}, {'れ', "r e"}, {'ろ', "r o"},
            {'わ', "w a"}, {'を', "w o"}, {'ん', "N"},
            {'ー', ":"}, {'っ', "cl"},
            
            // カタカナ
            {'ア', "a"}, {'イ', "i"}, {'ウ', "u"}, {'エ', "e"}, {'オ', "o"},
            {'カ', "k a"}, {'キ', "k i"}, {'ク', "k u"}, {'ケ', "k e"}, {'コ', "k o"},
            {'ガ', "g a"}, {'ギ', "g i"}, {'グ', "g u"}, {'ゲ', "g e"}, {'ゴ', "g o"},
            {'サ', "s a"}, {'シ', "s i"}, {'ス', "s u"}, {'セ', "s e"}, {'ソ', "s o"},
            {'ザ', "z a"}, {'ジ', "z i"}, {'ズ', "z u"}, {'ゼ', "z e"}, {'ゾ', "z o"},
            {'タ', "t a"}, {'チ', "t i"}, {'ツ', "ts u"}, {'テ', "t e"}, {'ト', "t o"},
            {'ダ', "d a"}, {'ヂ', "d i"}, {'ヅ', "d u"}, {'デ', "d e"}, {'ド', "d o"},
            {'ナ', "n a"}, {'ニ', "n i"}, {'ヌ', "n u"}, {'ネ', "n e"}, {'ノ', "n o"},
            {'ハ', "h a"}, {'ヒ', "h i"}, {'フ', "h u"}, {'ヘ', "h e"}, {'ホ', "h o"},
            {'バ', "b a"}, {'ビ', "b i"}, {'ブ', "b u"}, {'ベ', "b e"}, {'ボ', "b o"},
            {'パ', "p a"}, {'ピ', "p i"}, {'プ', "p u"}, {'ペ', "p e"}, {'ポ', "p o"},
            {'マ', "m a"}, {'ミ', "m i"}, {'ム', "m u"}, {'メ', "m e"}, {'モ', "m o"},
            {'ヤ', "y a"}, {'ユ', "y u"}, {'ヨ', "y o"},
            {'ラ', "r a"}, {'リ', "r i"}, {'ル', "r u"}, {'レ', "r e"}, {'ロ', "r o"},
            {'ワ', "w a"}, {'ヲ', "w o"}, {'ン', "N"},
            {'ッ', "cl"}
        };
        
        // 複数文字音素のPUAマッピング
        private static readonly Dictionary<string, char> MultiCharPhonemes = new Dictionary<string, char>
        {
            {"br", '\ue000'},
            {"ch", '\ue001'},
            {"cl", '\ue002'},
            {"dy", '\ue003'},
            {"gy", '\ue004'},
            {"hy", '\ue005'},
            {"ky", '\ue006'},
            {"my", '\ue007'},
            {"ny", '\ue008'},
            {"py", '\ue009'},
            {"ry", '\ue00a'},
            {"sh", '\ue00b'},
            {"ts", '\ue00c'},
            {"ty", '\ue00d'}
        };

        public override string Name => "WebGL Japanese Phonemizer";
        
        public override string Version => "1.0.0";
        
        public override string[] SupportedLanguages => new[] { "ja" };
        
        protected override async Task<List<string>> PhonemizeInternalAsync(string text, string language, CancellationToken cancellationToken)
        {
            var phonemes = new List<string> { "^" }; // BOS marker
            
            // 簡易的な音素化（実際のOpenJTalkより精度は低い）
            foreach (char c in text)
            {
                if (KanaToPhoneme.TryGetValue(c, out string phonemeStr))
                {
                    var parts = phonemeStr.Split(' ');
                    foreach (var part in parts)
                    {
                        // 複数文字音素をPUA文字に変換
                        if (MultiCharPhonemes.TryGetValue(part, out char puaChar))
                        {
                            phonemes.Add(puaChar.ToString());
                        }
                        else if (part != "pau") // pauseはスキップ
                        {
                            phonemes.Add(part);
                        }
                    }
                }
                else if (char.IsLetter(c))
                {
                    // 漢字やその他の文字は仮名に変換できないため、スキップ
                    Debug.LogWarning($"[WebGLJapanesePhonemizer] Cannot phonemize character: {c}");
                }
                // 句読点や記号は無視
            }
            
            phonemes.Add("$"); // EOS marker
            
            return await Task.FromResult(phonemes);
        }
        
        public override void Initialize()
        {
            // 初期化処理は特に必要なし
        }
    }
}