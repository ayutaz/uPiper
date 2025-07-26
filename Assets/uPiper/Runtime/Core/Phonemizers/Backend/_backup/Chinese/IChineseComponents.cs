using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Interface definitions for Chinese phonemizer components
    /// </summary>
    
    public interface IPinyinToPhonemeMapper
    {
        List<string> PinyinToIPA(string pinyin);
    }
    
    public interface IChineseTextSegmenter
    {
        List<string> Segment(string text);
    }
    
    public interface IChineseTextNormalizer
    {
        string Normalize(string text);
    }
}