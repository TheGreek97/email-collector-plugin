using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhishingDataCollector
{
    internal static class BodyFeatures
    {
        public static float GetReadabilityIndex(string mailBody, string language = "en")
        {
            int n_letters = Regex.Matches(mailBody, @"\w").Count;
            MatchCollection wordsInText = Regex.Matches(mailBody, @"\w+");
            int n_words = wordsInText.Count;
            n_words = n_words <= 0 ? 1 : n_words;
            int n_sentences = Regex.Matches(mailBody, @"\w+[^.?!;:]+").Count;
            n_sentences = n_sentences <= 0 ? 1 : n_sentences;
            if (language == "it")
            {
                //Gulpease Index (For Italian texts)
                float lp = (float)n_letters * 100 / n_words;
                float fr = n_sentences * 100 / n_words;
                float gulpease = 89 - (lp / 10) + (3 * fr);
                return gulpease;
            }
            else
            {
                // Flesch Reading Ease Score
                int n_syllables = 0;
                foreach (Match wordMatch in wordsInText)
                {
                    string word = wordMatch.Value.ToLower();
                    n_syllables += Syllabify.GetNSyllables(word);
                    /*
                    word = Regex.Replace(word, @"(?:[^laeiouy]|ed|[^laeiouy]e)$", "");
                    word = Regex.Replace(word, @"^y", "");
                    n_syllables += Regex.Matches(word, @"[aeiouy]{1,2}").Count;
                    */
                }
                float s = n_words / n_sentences;  // average sentence length
                float w = n_syllables / n_words;  // average number of syllables per word
                float flesch = 206.853f - 1.015f * s - 84.6f * w;
                return flesch;
            }
        }

        public static string GetPlainTextFromHtml(string htmlText)
        {
            string strippedString = "";
            bool insideTag = false;
            foreach (char let in htmlText)
            {
                if (let == '<')
                {
                    insideTag = true;
                    continue;
                }
                if (let == '>')
                {
                    insideTag = false;
                    continue;
                }
                if (!insideTag)
                {
                    strippedString += let;
                }
            }
            strippedString = Regex.Replace(strippedString, @"\n|\t|\r", "");  // strip escape characters \n \t \r 
            return strippedString;
        }
    }
}
