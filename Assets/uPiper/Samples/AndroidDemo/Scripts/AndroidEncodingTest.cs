using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System;

namespace uPiper.Samples.AndroidDemo
{
    /// <summary>
    /// Test text encoding on Android
    /// </summary>
    public class AndroidEncodingTest : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text displayText;
        [SerializeField] private InputField inputField;
        [SerializeField] private Button testButton;

        private void Start()
        {
            if (testButton != null)
            {
                testButton.onClick.AddListener(TestEncoding);
            }

            // Set default test text
            if (inputField != null)
            {
                inputField.text = "こんにちは";
            }

            TestEncoding();
        }

        private void TestEncoding()
        {
            if (displayText == null) return;

            string testText = inputField != null ? inputField.text : "こんにちは";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Original text: {testText}");
            sb.AppendLine($"Length: {testText.Length}");
            sb.AppendLine();

            // Check each character
            sb.AppendLine("Character analysis:");
            for (int i = 0; i < testText.Length; i++)
            {
                char c = testText[i];
                sb.AppendLine($"  [{i}] '{c}' = U+{((int)c):X4}");
            }
            sb.AppendLine();

            // UTF-8 bytes
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(testText);
            sb.AppendLine($"UTF-8 bytes ({utf8Bytes.Length}):");
            sb.AppendLine($"  {BitConverter.ToString(utf8Bytes)}");
            sb.AppendLine();

            // Try to decode as different encodings
            try
            {
                // Decode UTF-8
                string utf8Decoded = Encoding.UTF8.GetString(utf8Bytes);
                sb.AppendLine($"UTF-8 decoded: {utf8Decoded}");
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"UTF-8 decode error: {e.Message}");
            }

            // System info
            sb.AppendLine();
            sb.AppendLine("System info:");
            sb.AppendLine($"  Default encoding: {Encoding.Default.EncodingName}");
            sb.AppendLine($"  Platform: {Application.platform}");
            sb.AppendLine($"  System language: {Application.systemLanguage}");

            displayText.text = sb.ToString();

            // Also log to console
            Debug.Log($"[AndroidEncodingTest]\n{sb}");
        }

        private void OnDestroy()
        {
            if (testButton != null)
            {
                testButton.onClick.RemoveListener(TestEncoding);
            }
        }
    }
}