using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Services;

namespace uPiper.Core.Phonemizers.Unity
{
    /// <summary>
    /// Unity-specific phonemizer service with coroutine support
    /// </summary>
    public class UnityPhonemizerService : MonoBehaviour
    {
        private static UnityPhonemizerService instance;
        public static UnityPhonemizerService Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[UnityPhonemizerService]");
                    instance = go.AddComponent<UnityPhonemizerService>();
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private PhonemizerService phonemizerService;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            phonemizerService = PhonemizerService.Instance;
        }

        /// <summary>
        /// Phonemize text using coroutine
        /// </summary>
        public void Phonemize(string text, string language, Action<PhonemeResult> callback)
        {
            StartCoroutine(PhonemizeCoroutine(text, language, callback));
        }

        private IEnumerator PhonemizeCoroutine(string text, string language, Action<PhonemeResult> callback)
        {
            PhonemeResult result = null;
            Exception error = null;

            // Run on background thread
            var task = Task.Run(async () =>
            {
                try
                {
                    result = await phonemizerService.PhonemizeAsync(text, language);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            // Wait for completion
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (error != null)
            {
                Debug.LogError($"Phonemization error: {error.Message}");
                callback?.Invoke(new PhonemeResult
                {
                    Success = false,
                    Error = error.Message,
                    Phonemes = new string[0]
                });
            }
            else
            {
                callback?.Invoke(result);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                phonemizerService?.Dispose();
                instance = null;
            }
        }
    }
}