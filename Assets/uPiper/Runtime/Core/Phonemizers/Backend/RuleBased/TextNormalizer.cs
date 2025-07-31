using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace uPiper.Core.Phonemizers.Backend.RuleBased
{
    /// <summary>
    /// Text normalizer for preprocessing text before phonemization.
    /// Handles numbers, abbreviations, and special characters.
    /// </summary>
    public class TextNormalizer
    {
        private Dictionary<string, string> abbreviations;
        private Dictionary<string, string> contractions;
        private readonly NumberToWords numberConverter;

        public TextNormalizer()
        {
            InitializeAbbreviations();
            InitializeContractions();
            numberConverter = new NumberToWords();
        }

        /// <summary>
        /// Normalizes text for phonemization.
        /// </summary>
        /// <param name="text">Input text.</param>
        /// <returns>Normalized text.</returns>
        public string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Convert to consistent casing (preserve for now, convert later)
            var result = text;

            // Expand contractions
            result = ExpandContractions(result);

            // Expand abbreviations
            result = ExpandAbbreviations(result);

            // Convert numbers to words
            result = ConvertNumbersToWords(result);

            // Handle special characters
            result = HandleSpecialCharacters(result);

            // Normalize whitespace
            result = NormalizeWhitespace(result);

            return result;
        }

        private string ExpandContractions(string text)
        {
            foreach (var contraction in contractions)
            {
                // Case-insensitive replacement
                text = Regex.Replace(text,
                    @"\b" + Regex.Escape(contraction.Key) + @"\b",
                    contraction.Value,
                    RegexOptions.IgnoreCase);
            }
            return text;
        }

        private string ExpandAbbreviations(string text)
        {
            // Handle common patterns
            text = Regex.Replace(text, @"\bDr\.", "Doctor", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bMr\.", "Mister", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bMrs\.", "Missus", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bMs\.", "Miss", RegexOptions.IgnoreCase);

            // Handle other abbreviations
            foreach (var abbr in abbreviations)
            {
                text = Regex.Replace(text,
                    @"\b" + Regex.Escape(abbr.Key) + @"\b",
                    abbr.Value,
                    RegexOptions.IgnoreCase);
            }

            return text;
        }

        private string ConvertNumbersToWords(string text)
        {
            // Match various number formats
            var patterns = new[]
            {
                @"\b\d{1,3}(,\d{3})*(\.\d+)?\b", // Numbers with commas and decimals
                @"\b\d+(\.\d+)?\b",               // Simple numbers
                @"\$\d+(\.\d{2})?",               // Currency
                @"\d+%",                          // Percentages
                @"\d{1,2}:\d{2}",                 // Time
                @"\d{1,2}/\d{1,2}(/\d{2,4})?",   // Dates
            };

            foreach (var pattern in patterns)
            {
                text = Regex.Replace(text, pattern, match =>
                {
                    var number = match.Value;

                    // Handle special cases
                    if (number.StartsWith("$"))
                        return HandleCurrency(number);
                    if (number.EndsWith("%"))
                        return HandlePercentage(number);
                    if (number.Contains(":"))
                        return HandleTime(number);
                    if (number.Contains("/"))
                        return HandleDate(number);

                    // Regular number
                    return numberConverter.Convert(number);
                });
            }

            return text;
        }

        private string HandleCurrency(string currency)
        {
            var amount = currency[1..];
            var words = numberConverter.Convert(amount);

            if (amount.Contains("."))
            {
                var parts = amount.Split('.');
                if (parts[1] == "00")
                    return words + " dollars";
                else
                    return words.Replace(" point ", " dollars and ") + " cents";
            }

            return words + (amount == "1" ? " dollar" : " dollars");
        }

        private string HandlePercentage(string percentage)
        {
            var number = percentage[..^1];
            return numberConverter.Convert(number) + " percent";
        }

        private string HandleTime(string time)
        {
            var parts = time.Split(':');
            var hours = int.Parse(parts[0]);
            var minutes = int.Parse(parts[1]);

            var result = numberConverter.Convert(hours.ToString());

            if (minutes == 0)
                result += " o'clock";
            else if (minutes < 10)
                result += " oh " + numberConverter.Convert(minutes.ToString());
            else
                result += " " + numberConverter.Convert(minutes.ToString());

            return result;
        }

        private string HandleDate(string date)
        {
            // Simple date handling - can be expanded
            return date.Replace("/", " ");
        }

        private string HandleSpecialCharacters(string text)
        {
            // Replace special characters with words or spaces
            text = text.Replace("&", " and ");
            text = text.Replace("+", " plus ");
            text = text.Replace("=", " equals ");
            text = text.Replace("@", " at ");
            text = text.Replace("#", " number ");

            // Remove or replace other special characters
            text = Regex.Replace(text, @"[^\w\s\.\,\!\?\-\']", " ");

            return text;
        }

        private string NormalizeWhitespace(string text)
        {
            // Replace multiple spaces with single space
            text = Regex.Replace(text, @"\s+", " ");

            // Trim
            text = text.Trim();

            return text;
        }

        private void InitializeAbbreviations()
        {
            abbreviations = new Dictionary<string, string>
            {
                ["USA"] = "U S A",
                ["UK"] = "U K",
                ["EU"] = "E U",
                ["UN"] = "U N",
                ["CEO"] = "C E O",
                ["PhD"] = "P H D",
                ["AI"] = "A I",
                ["API"] = "A P I",
                ["CPU"] = "C P U",
                ["GPU"] = "G P U",
                ["RAM"] = "ram",
                ["ROM"] = "rom",
                ["USB"] = "U S B",
                ["WWW"] = "world wide web",
                ["etc"] = "et cetera",
                ["vs"] = "versus",
                ["ft"] = "feet",
                ["inc"] = "incorporated",
                ["ltd"] = "limited",
                ["st"] = "street",
                ["ave"] = "avenue",
                ["blvd"] = "boulevard",
                ["jr"] = "junior",
                ["sr"] = "senior"
            };
        }

        private void InitializeContractions()
        {
            contractions = new Dictionary<string, string>
            {
                ["can't"] = "cannot",
                ["won't"] = "will not",
                ["n't"] = " not", // General case
                ["'re"] = " are",
                ["'ve"] = " have",
                ["'ll"] = " will",
                ["'d"] = " would",
                ["'m"] = " am",
                ["'s"] = " is", // Could also be possessive
                ["let's"] = "let us",
                ["it's"] = "it is",
                ["that's"] = "that is",
                ["what's"] = "what is",
                ["where's"] = "where is",
                ["who's"] = "who is",
                ["there's"] = "there is",
                ["here's"] = "here is",
                ["I'm"] = "I am",
                ["you're"] = "you are",
                ["we're"] = "we are",
                ["they're"] = "they are",
                ["I've"] = "I have",
                ["you've"] = "you have",
                ["we've"] = "we have",
                ["they've"] = "they have",
                ["I'll"] = "I will",
                ["you'll"] = "you will",
                ["we'll"] = "we will",
                ["they'll"] = "they will",
                ["I'd"] = "I would",
                ["you'd"] = "you would",
                ["we'd"] = "we would",
                ["they'd"] = "they would"
            };
        }
    }

    /// <summary>
    /// Converts numbers to words.
    /// </summary>
    internal class NumberToWords
    {
        private readonly string[] ones =
        {
            "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
            "seventeen", "eighteen", "nineteen"
        };

        private readonly string[] tens =
        {
            "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
        };

        private readonly string[] thousands =
        {
            "", "thousand", "million", "billion", "trillion"
        };

        public string Convert(string numberStr)
        {
            // Handle decimal numbers
            if (numberStr.Contains("."))
            {
                var parts = numberStr.Split('.');
                var whole = Convert(parts[0]);
                var decimals = ConvertDecimals(parts[1]);
                return whole + " point " + decimals;
            }

            // Remove commas
            numberStr = numberStr.Replace(",", "");

            // Parse as long
            if (!long.TryParse(numberStr, out var number))
                return numberStr; // Return as-is if not a valid number

            if (number == 0)
                return "zero";

            if (number < 0)
                return "negative " + Convert((-number).ToString());

            return ConvertNumber(number).Trim();
        }

        private string ConvertDecimals(string decimals)
        {
            var result = new List<string>();
            foreach (var digit in decimals)
            {
                if (char.IsDigit(digit))
                {
                    result.Add(ones[digit - '0']);
                }
            }
            return string.Join(" ", result);
        }

        private string ConvertNumber(long number)
        {
            if (number < 20)
                return ones[number];

            if (number < 100)
                return tens[number / 10] + (number % 10 > 0 ? " " + ones[number % 10] : "");

            if (number < 1000)
                return ones[number / 100] + " hundred" +
                    (number % 100 > 0 ? " " + ConvertNumber(number % 100) : "");

            // Handle larger numbers
            var result = new List<string>();
            var groupIndex = 0;

            while (number > 0)
            {
                var group = number % 1000;
                if (group > 0)
                {
                    var groupStr = ConvertNumber(group);
                    if (groupIndex > 0)
                        groupStr += " " + thousands[groupIndex];
                    result.Insert(0, groupStr);
                }
                number /= 1000;
                groupIndex++;
            }

            return string.Join(" ", result);
        }
    }
}