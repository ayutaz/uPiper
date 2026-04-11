using System;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// モデルのケイパビリティ（推論能力）を問い合わせるインターフェース。
    /// <see cref="IInferenceAudioGenerator"/> から分離し、デコレータの委譲コストを削減する。
    /// </summary>
    internal interface IModelCapabilities
    {
        /// <summary>モデルがprosody_featuresをサポートするかどうか</summary>
        bool SupportsProsody { get; }

        /// <summary>モデルがマルチスピーカー（sid入力）をサポートするかどうか</summary>
        bool SupportsMultiSpeaker { get; }

        /// <summary>モデルが多言語（lid入力）をサポートするかどうか</summary>
        bool SupportsLanguageId { get; }

        /// <summary>モデルが durations 出力テンソルをサポートするかどうか</summary>
        bool SupportsDurations { get; }

        /// <summary>現在のモデルのサンプルレート</summary>
        int SampleRate { get; }
    }
}
