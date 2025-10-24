using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.CustomEnglishPhonemizer
{
    public class DigitsToWords : MonoBehaviour
    {
        // Converts all standalone numeric sequences in text into words
        public static string ReplaceNumbersWithWords(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            return Regex.Replace(input, @"\b\d+\b", match =>
            {
                if (long.TryParse(match.Value, out long number))
                    return NumberToWords(number);
                return match.Value;
            });
        }

        // Converts integers up to billions into spoken English
        private static string NumberToWords(long number)
        {
            if (number == 0)
                return "zero";

            if (number < 0)
                return "minus " + NumberToWords(Math.Abs(number));

            var parts = new List<string>();

            if ((number / 1_000_000_000) > 0)
            {
                parts.Add(NumberToWords(number / 1_000_000_000) + " billion");
                number %= 1_000_000_000;
            }

            if ((number / 1_000_000) > 0)
            {
                parts.Add(NumberToWords(number / 1_000_000) + " million");
                number %= 1_000_000;
            }

            if ((number / 1_000) > 0)
            {
                parts.Add(NumberToWords(number / 1_000) + " thousand");
                number %= 1_000;
            }

            if ((number / 100) > 0)
            {
                parts.Add(NumberToWords(number / 100) + " hundred");
                number %= 100;
            }

            if (number > 0)
            {
                if (parts.Count != 0)
                    parts.Add("and");

                var unitsMap = new[]
                {
                    "zero","one","two","three","four","five","six","seven","eight","nine",
                    "ten","eleven","twelve","thirteen","fourteen","fifteen","sixteen",
                    "seventeen","eighteen","nineteen"
                };

                var tensMap = new[]
                {
                    "zero","ten","twenty","thirty","forty","fifty","sixty","seventy","eighty","ninety"
                };

                if (number < 20)
                    parts.Add(unitsMap[number]);
                else
                {
                    parts.Add(tensMap[number / 10]);
                    if ((number % 10) > 0)
                        parts.Add(unitsMap[number % 10]);
                }
            }

            return string.Join(" ", parts);
        }
    }
}
