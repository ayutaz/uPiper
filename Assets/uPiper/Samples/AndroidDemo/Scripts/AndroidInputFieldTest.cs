using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections;

namespace uPiper.Samples.AndroidDemo
{
    /// <summary>
    /// Test InputField behavior on Android
    /// </summary>
    public class AndroidInputFieldTest : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private InputField testInputField;
        [SerializeField] private Text displayText;
        [SerializeField] private Button setTextButton;
        [SerializeField] private Button readTextButton;
        
        private void Start()
        {
            if (setTextButton != null)
            {
                setTextButton.onClick.AddListener(SetTestText);
            }
            
            if (readTextButton != null)
            {
                readTextButton.onClick.AddListener(ReadInputText);
            }
            
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
            
            string testText = "こんにちは";
            Debug.Log($"[AndroidInputFieldTest] Setting text: {testText}");
            
            testInputField.text = testText;
            
            UpdateDisplay($"Set text: {testText}");
        }
        
        private void ReadInputText()
        {
            if (testInputField == null) return;
            
            string currentText = testInputField.text;
            Debug.Log($"[AndroidInputFieldTest] Current text: {currentText}");
            Debug.Log($"[AndroidInputFieldTest] Text length: {currentText.Length}");
            
            // Analyze encoding
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(currentText);
            Debug.Log($"[AndroidInputFieldTest] UTF-8 bytes: {BitConverter.ToString(utf8Bytes)}");
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Current text: {currentText}");
            sb.AppendLine($"Length: {currentText.Length} chars");
            sb.AppendLine($"UTF-8 bytes: {utf8Bytes.Length}");
            sb.AppendLine($"Hex: {BitConverter.ToString(utf8Bytes)}");
            
            // Check each character
            sb.AppendLine("\nCharacter analysis:");
            for (int i = 0; i < currentText.Length && i < 10; i++)
            {
                char c = currentText[i];
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
            if (setTextButton != null)
            {
                setTextButton.onClick.RemoveListener(SetTestText);
            }
            
            if (readTextButton != null)
            {
                readTextButton.onClick.RemoveListener(ReadInputText);
            }
            
            if (testInputField != null)
            {
                testInputField.onValueChanged.RemoveListener(OnInputValueChanged);
                testInputField.onEndEdit.RemoveListener(OnInputEndEdit);
            }
        }
    }
}