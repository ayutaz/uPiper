using UnityEngine;

namespace uPiper.Core
{
    /// <summary>
    /// PiperConfig の ScriptableObject ラッパー。
    /// プロジェクトアセットとして永続化し、Inspector で編集可能にする。
    /// </summary>
    [CreateAssetMenu(
        fileName = "PiperConfigAsset",
        menuName = "uPiper/Config Asset",
        order = 100)]
    public sealed class PiperConfigAsset : ScriptableObject
    {
        [SerializeField]
        private PiperConfig _config = new();

        /// <summary>内部 PiperConfig への読み取り専用アクセス。</summary>
        public PiperConfig Config => _config;

        /// <summary>バリデーション済み不変スナップショットを返す。</summary>
        public ValidatedPiperConfig ToValidated()
        {
            return _config.ToValidated();
        }

        /// <summary>ランタイム用ディープコピーを返す。</summary>
        public PiperConfig CreateRuntimeCopy()
        {
            return _config.Clone();
        }

        private void Reset()
        {
            _config = PiperConfig.CreateDefault();
        }
    }
}