using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Loading panel UI for displaying progress during WebGL initialization.
    /// Shows a progress bar with percentage text and a status message.
    /// </summary>
    public class WebGLLoadingPanel : MonoBehaviour
    {
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private GameObject _panel;

        /// <summary>
        /// Shows the loading panel.
        /// </summary>
        public void Show()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        /// <summary>
        /// Hides the loading panel.
        /// </summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>
        /// Updates the progress bar and status text.
        /// </summary>
        /// <param name="progress">Progress value from 0.0 to 1.0</param>
        /// <param name="status">Optional status message to display</param>
        public void SetProgress(float progress, string status = null)
        {
            if (_progressBar != null) _progressBar.value = progress;
            if (_progressText != null) _progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
            if (_statusText != null && status != null) _statusText.text = status;
        }
    }
}