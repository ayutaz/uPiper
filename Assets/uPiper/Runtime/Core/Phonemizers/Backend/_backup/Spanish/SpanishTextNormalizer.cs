using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Spanish
{
    /// <summary>
    /// Spanish text normalizer for preprocessing text before phonemization.
    /// Handles numbers, abbreviations, and Spanish-specific punctuation.
    /// </summary>
    public class SpanishTextNormalizer
    {
        private readonly Dictionary<string, string> abbreviations;
        private readonly Dictionary<string, string> numberWords;
        
        public SpanishTextNormalizer()
        {
            InitializeAbbreviations();
            InitializeNumberWords();
        }
        
        public string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            // 1. Handle Spanish punctuation (¿ ¡)
            text = HandleSpanishPunctuation(text);
            
            // 2. Expand abbreviations
            text = ExpandAbbreviations(text);
            
            // 3. Convert numbers to words
            text = NormalizeNumbers(text);
            
            // 4. Handle special characters
            text = HandleSpecialCharacters(text);
            
            // 5. Normalize whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();
            
            return text;
        }
        
        private string HandleSpanishPunctuation(string text)
        {
            // Remove inverted punctuation marks (they don't affect pronunciation)
            text = text.Replace("¿", "");
            text = text.Replace("¡", "");
            
            // Handle other punctuation
            text = text.Replace("—", " ");  // em dash
            text = text.Replace("–", " ");  // en dash
            
            return text;
        }
        
        private string ExpandAbbreviations(string text)
        {
            foreach (var abbr in abbreviations)
            {
                // Match abbreviation with word boundaries
                string pattern = $@"\b{Regex.Escape(abbr.Key)}\b";
                text = Regex.Replace(text, pattern, abbr.Value, RegexOptions.IgnoreCase);
            }
            
            return text;
        }
        
        private string NormalizeNumbers(string text)
        {
            // Match numbers (including decimals)
            text = Regex.Replace(text, @"\b(\d+)\.(\d+)\b", match =>
            {
                var integerPart = match.Groups[1].Value;
                var decimalPart = match.Groups[2].Value;
                
                return $"{ConvertNumberToSpanishWords(int.Parse(integerPart))} coma {ConvertDigitsToWords(decimalPart)}";
            });
            
            // Match whole numbers
            text = Regex.Replace(text, @"\b\d+\b", match =>
            {
                if (int.TryParse(match.Value, out int number))
                {
                    return ConvertNumberToSpanishWords(number);
                }
                return match.Value;
            });
            
            return text;
        }
        
        private string ConvertNumberToSpanishWords(int number)
        {
            if (number == 0)
                return "cero";
                
            if (numberWords.ContainsKey(number.ToString()))
                return numberWords[number.ToString()];
                
            // Handle larger numbers
            if (number < 0)
            {
                return "menos " + ConvertNumberToSpanishWords(-number);
            }
            else if (number < 20)
            {
                return GetBasicNumber(number);
            }
            else if (number < 100)
            {
                int tens = number / 10 * 10;
                int units = number % 10;
                
                if (units == 0)
                    return GetBasicNumber(tens);
                else if (number < 30)
                    return "veinti" + GetBasicNumber(units);
                else
                    return GetBasicNumber(tens) + " y " + GetBasicNumber(units);
            }
            else if (number < 1000)
            {
                int hundreds = number / 100;
                int remainder = number % 100;
                
                string result = "";
                if (hundreds == 1)
                {
                    result = (remainder == 0) ? "cien" : "ciento";
                }
                else
                {
                    result = GetBasicNumber(hundreds) + "cientos";
                    // Special cases
                    if (hundreds == 5) result = "quinientos";
                    else if (hundreds == 7) result = "setecientos";
                    else if (hundreds == 9) result = "novecientos";
                }
                
                if (remainder > 0)
                    result += " " + ConvertNumberToSpanishWords(remainder);
                    
                return result;
            }
            else if (number < 1000000)
            {
                int thousands = number / 1000;
                int remainder = number % 1000;
                
                string result = "";
                if (thousands == 1)
                    result = "mil";
                else
                    result = ConvertNumberToSpanishWords(thousands) + " mil";
                    
                if (remainder > 0)
                    result += " " + ConvertNumberToSpanishWords(remainder);
                    
                return result;
            }
            else
            {
                // For very large numbers, just spell out digits
                return ConvertDigitsToWords(number.ToString());
            }
        }
        
        private string GetBasicNumber(int number)
        {
            switch (number)
            {
                case 1: return "uno";
                case 2: return "dos";
                case 3: return "tres";
                case 4: return "cuatro";
                case 5: return "cinco";
                case 6: return "seis";
                case 7: return "siete";
                case 8: return "ocho";
                case 9: return "nueve";
                case 10: return "diez";
                case 11: return "once";
                case 12: return "doce";
                case 13: return "trece";
                case 14: return "catorce";
                case 15: return "quince";
                case 16: return "dieciséis";
                case 17: return "diecisiete";
                case 18: return "dieciocho";
                case 19: return "diecinueve";
                case 20: return "veinte";
                case 30: return "treinta";
                case 40: return "cuarenta";
                case 50: return "cincuenta";
                case 60: return "sesenta";
                case 70: return "setenta";
                case 80: return "ochenta";
                case 90: return "noventa";
                default: return number.ToString();
            }
        }
        
        private string ConvertDigitsToWords(string digits)
        {
            var result = new StringBuilder();
            foreach (char digit in digits)
            {
                if (result.Length > 0)
                    result.Append(" ");
                    
                switch (digit)
                {
                    case '0': result.Append("cero"); break;
                    case '1': result.Append("uno"); break;
                    case '2': result.Append("dos"); break;
                    case '3': result.Append("tres"); break;
                    case '4': result.Append("cuatro"); break;
                    case '5': result.Append("cinco"); break;
                    case '6': result.Append("seis"); break;
                    case '7': result.Append("siete"); break;
                    case '8': result.Append("ocho"); break;
                    case '9': result.Append("nueve"); break;
                    default: result.Append(digit); break;
                }
            }
            return result.ToString();
        }
        
        private string HandleSpecialCharacters(string text)
        {
            // Currency symbols
            text = text.Replace("$", " dólares ");
            text = text.Replace("€", " euros ");
            text = text.Replace("£", " libras ");
            text = text.Replace("¥", " yenes ");
            
            // Mathematical symbols
            text = text.Replace("+", " más ");
            text = text.Replace("-", " menos ");
            text = text.Replace("*", " por ");
            text = text.Replace("/", " entre ");
            text = text.Replace("=", " igual a ");
            text = text.Replace("%", " por ciento ");
            
            // Other symbols
            text = text.Replace("&", " y ");
            text = text.Replace("@", " arroba ");
            text = text.Replace("#", " número ");
            
            return text;
        }
        
        private void InitializeAbbreviations()
        {
            abbreviations = new Dictionary<string, string>
            {
                // Titles
                ["Sr."] = "señor",
                ["Sra."] = "señora",
                ["Srta."] = "señorita",
                ["Dr."] = "doctor",
                ["Dra."] = "doctora",
                ["Prof."] = "profesor",
                ["Ing."] = "ingeniero",
                ["Lic."] = "licenciado",
                
                // Common abbreviations
                ["etc."] = "etcétera",
                ["pág."] = "página",
                ["págs."] = "páginas",
                ["tel."] = "teléfono",
                ["núm."] = "número",
                ["apdo."] = "apartado",
                ["avda."] = "avenida",
                ["c/"] = "calle",
                ["p.ej."] = "por ejemplo",
                ["aprox."] = "aproximadamente",
                
                // Organizations
                ["S.A."] = "sociedad anónima",
                ["S.L."] = "sociedad limitada",
                ["EE.UU."] = "Estados Unidos",
                ["UE"] = "Unión Europea",
                
                // Measurements
                ["km"] = "kilómetros",
                ["m"] = "metros",
                ["cm"] = "centímetros",
                ["kg"] = "kilogramos",
                ["g"] = "gramos",
                ["l"] = "litros",
                ["ml"] = "mililitros",
                
                // Time
                ["h"] = "horas",
                ["min"] = "minutos",
                ["seg"] = "segundos",
                ["a.m."] = "antes del mediodía",
                ["p.m."] = "después del mediodía"
            };
        }
        
        private void InitializeNumberWords()
        {
            numberWords = new Dictionary<string, string>
            {
                ["0"] = "cero",
                ["100"] = "cien",
                ["200"] = "doscientos",
                ["300"] = "trescientos",
                ["400"] = "cuatrocientos",
                ["500"] = "quinientos",
                ["600"] = "seiscientos",
                ["700"] = "setecientos",
                ["800"] = "ochocientos",
                ["900"] = "novecientos",
                ["1000"] = "mil",
                ["1000000"] = "un millón",
                ["1000000000"] = "mil millones"
            };
        }
    }
}