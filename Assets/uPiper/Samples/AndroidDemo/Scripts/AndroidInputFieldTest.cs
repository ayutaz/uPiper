using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace uPiper.Samples.AndroidDemo
{
    /// <summary>
    /// Test InputField behavior on Android
    /// </summary>
    public class AndroidInputFieldTest : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private readonly InputField testInputField;
        [SerializeField] private readonly Text displayText;
        [SerializeField] private readonly Button setTextButton;
        [SerializeField] private readonly Button readTextButton;

        private void Start()
        {
            setTextButton?.onClick.AddListener(SetTestText);

            readTextButton?.onClick.AddListener(ReadInputText);

            if (testInputField != null)
            {
                testInputField.onValueChanged.AddListener(OnInputValueChanged);
                testInputField.onEndEdit.AddListener(OnInputEndEdit);
            }

            // Initial test
            StartCoroutine(DelayedTest());
        }

        private IEnumerator DelayedTest()
        {
            yield return new WaitForSeconds(1f);
            SetTestText();
        }

        private void SetTestText()
        {
            if (testInputField == null) return;

            var testText = "こんにちは";
            Debug.Log($"[AndroidInputFieldTest] Setting text: {testText}");

            testInputField.text = testText;

            UpdateDisplay($"Set text: {testText}");
        }

        private void ReadInputText()
        {
            if (testInputField == null) return;

            var currentText = testInputField.text;
            Debug.Log($"[AndroidInputFieldTest] Current text: {currentText}");
            Debug.Log($"[AndroidInputFieldTest] Text length: {currentText.Length}");

            // Analyze encoding
            var utf8Bytes = Encoding.UTF8.GetBytes(currentText);
            Debug.Log($"[AndroidInputFieldTest] UTF-8 bytes: {BitConverter.ToString(utf8Bytes)}");

            var sb = new StringBuilder();
            sb.AppendLine($"Current text: {currentText}");
            sb.AppendLine($"Length: {currentText.Length} chars");
            sb.AppendLine($"UTF-8 bytes: {utf8Bytes.Length}");
            sb.AppendLine($"Hex: {BitConverter.ToString(utf8Bytes)}");

            // Check each character
            sb.AppendLine("\nCharacter analysis:");
            for (var i = 0; i < currentText.Length && i < 10; i++)
            {
                var c = currentText[i];
                sb.AppendLine($"[{i}] '{c}' = U+{((int)c):X4}");
            }

            UpdateDisplay(sb.ToString());
        }

        private void OnInputValueChanged(string value)
        {
            Debug.Log($"[AndroidInputFieldTest] Value changed: {value} (length: {value.Length})");
        }

        private void OnInputEndEdit(string value)
        {
            Debug.Log($"[AndroidInputFieldTest] End edit: {value}");
            ReadInputText();
        }

        private void UpdateDisplay(string message)
        {
            if (displayText != null)
            {
                displayText.text = message;
            }
        }

        private void OnDestroy()
        {
            setTextButton?.onClick.RemoveListener(SetTestText);

            readTextButton?.onClick.RemoveListener(ReadInputText);

            if (testInputField != null)
            {
                testInputField.onValueChanged.RemoveListener(OnInputValueChanged);
                testInputField.onEndEdit.RemoveListener(OnInputEndEdit);
            }
        }
    }
}