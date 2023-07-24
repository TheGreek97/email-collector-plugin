using com.sun.tools.doclets.@internal.toolkit.util;
using LanguageDetection;
using NHunspell;
using OpenNLP.Tools.PosTagger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Python.Runtime;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using com.sun.istack.@internal.logging;

namespace PhishingDataCollector
{
    internal static class BodyFeatures
    {
        private static readonly string[] _languages = { "en", "es", "fr", "pt", "it", "de" };
        private static readonly string[] _phishyWords = { "account", "security", "user", "verify", "service", "valid", "required", "credentials",
            "attention", "request", "suspended", "company", "bank", "deposit", "post", "money", "bank", "update", "verify"};
        private static readonly string[] _scammyWords = { "€", "£", "$", "customer", "prize", "donate", "buy", "pay", "congratulations", "death", "please",
            "response", "dollar", "looking", "urgent", "warning", "win", "offer", "risk", "money", "transaction", "sex", "nude" };
        private static readonly char[] _specialCharacters = { '@', '#', '_', '°', '[', ']', '{', '}', '$', '-', '+', '&', '%' };

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
            if (string.IsNullOrEmpty(htmlText))
            {
                return "";
            }
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

        public static string GetLanguage(string body_text)
        {
            LanguageDetector detector = new LanguageDetector();
            detector.AddAllLanguages();
            var language = detector.Detect(body_text);
            return language;
        }

        public static int GetNSpecialChars(string body_text)
        {
            int n = 0;
            foreach (char c in body_text)
            {
                if (_specialCharacters.Contains(c))
                {
                    n++;
                }
            }
            return n;
        }


        /* This function computes 9 features:
           n_misspelled_words, n_phishy (words), n_scammy (words), vdb_adjectives_rate, vdb_verbs_rate, vdb_nouns_rate, vdb_articles_rate, voc_rate, vdb_rate
         */
        public static (int n_misspelled_words, int n_phishy, int n_scammy, 
            float vdb_adjectives_rate, float vdb_verbs_rate, float vdb_nouns_rate, float vdb_articles_rate, 
            float voc_rate, float vdb_rate) 
            GetWordsFeatures(string body_text, string language="")
        {
            if (string.IsNullOrEmpty(body_text))
            {
                return (0, 0, 0, 0, 0, 0, 0, 0, 0);
            }
            if (language == "") { language = GetLanguage(body_text); }
            int n_misspelled_words = 0, n_phishy = 0, n_scammy = 0;
            float vdb_adjectives_rate, vdb_verbs_rate, vdb_nouns_rate, vdb_articles_rate;
            float voc_rate, vdb_rate;
            string wordsInBody = Regex.Replace(body_text, @"[^\w $€£]", " ");  // remove all non-words (keeps currency symbols)
            string[] bodyTokens = wordsInBody.Split(' ');
            string dictPath = GetDictionaryPath(language);
            using (Hunspell spellChecker = new Hunspell(dictPath + ".aff", dictPath + ".dic"))
            {
                foreach (string word in bodyTokens)
                {
                    if (!string.IsNullOrEmpty(word))
                    {
                        if (!spellChecker.Spell(word)) { n_misspelled_words++; }
                        if (_phishyWords.Contains(word)) { n_phishy++; }
                        if (_scammyWords.Contains(word)) { n_scammy++; }
                    }
                }
            }
            // POS tagging

            string base_path_pos_files = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "POS");
            if (language == "en")  // ENGLISH 
            {
                string modelPath = Path.Combine(base_path_pos_files, "Models", "EnglishPOS.nbin");
                //string tagDictDir = Path.Combine(base_path_pos_files, "WordNet", "dict");
                EnglishMaximumEntropyPosTagger posTagger = new EnglishMaximumEntropyPosTagger(modelPath);
                string[] pos_tags = posTagger.Tag(bodyTokens);
                // pos tags are defined this way: https://www.ling.upenn.edu/courses/Fall_2003/ling001/penn_treebank_pos.html
                int n_adjectives = 0, n_verbs = 0, n_nouns = 0, n_articles = 0;
                foreach (string tag in pos_tags)
                {
                    if (tag.StartsWith("J")) { n_adjectives++; }  // tag == JJ or JJR or JJS
                    else if (tag.StartsWith("VB")) { n_verbs++; }  // tag == VB or VBD or VBG or VBN or VBP or VBZ  
                    else if (tag.StartsWith("NN")) { n_nouns++; }  // tag == NN or NNS or NNP or NNPS
                }
                foreach (string word in bodyTokens)  // we don't have a tag for articles only
                {
                    string w = word.ToLower();
                    if (w == "the" || w == "a" || w == "an") { n_articles++; }
                }
                int n_words = pos_tags.Length;
                vdb_adjectives_rate = n_adjectives / n_words;
                vdb_verbs_rate = n_verbs / n_words;
                vdb_nouns_rate = n_nouns / n_words;
                vdb_articles_rate = n_articles / n_words;

                // Rateos of words in basic and full vocabulary
                string base_path_wordlists = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "wordList");
                string basic_dict_path = Path.Combine(base_path_wordlists, "en_basic.txt");
                //string full_dict_path = Path.Combine(base_path_wordlists, "en_full.txt");
                //string[] full_dict = File.ReadAllLines(full_dict_path);
                //int voc_words = 0;
                string[] basic_dict = File.ReadAllLines(basic_dict_path);
                int n_basic_voc_words = 0;

                foreach (string word in bodyTokens)
                {
                    if (basic_dict.Contains(word)) { n_basic_voc_words++; }
                    //if (full_dict.Contains(word)) { voc_words++; }
                }
                voc_rate = (float)(n_words - n_misspelled_words) / n_words;  // n_mispelled_words are the number of words that are not in the dictionary
                vdb_rate = (float)n_basic_voc_words / n_words;
            }
            else if (language == "it") 
            {
                string[] pos_tags;
                pos_tags = new string[1]; // TODELETE
                string py_file = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "python", "posTagger_it.py");

                // Python.net implementation to run the python script for POS tagging
                using (Py.GIL())
                {
                    ThisAddIn.Logger.Info($"Processing POS tagging, body length: {body_text.Length} chars.");
                    using (var scope = Py.CreateScope())
                    {
                        //scope.Set("bodyTxt", body_text.ToPython());
                        dynamic os = Py.Import("os");
                        dynamic sys = Py.Import("sys");
                        sys.path.append(os.path.dirname(os.path.expanduser(py_file)));
                        PyObject fromFile = Py.Import(Path.GetFileNameWithoutExtension(py_file));
                        dynamic result = fromFile.InvokeMethod("GetPOSTags", new PyObject[1] { body_text.ToPython() });
                        pos_tags = result;
                    }
                    ThisAddIn.Logger.Info($"Processed POS tagging.");
                }

                /* TODO: make this work - ChatGPT generated code for IronPython (IronPython exploits multi-threading, differently from Python.net) 
                var engine = IronPython.Hosting.Python.CreateEngine();
                var pyScope = engine.CreateScope();
                pyScope.SetVariable("bodyTxt", body_text);
                pyScope = engine.ExecuteFile(py_file, pyScope);  // module "spacy" not found 
                
                // ExecuteFile has returned the updated scope, where we can find the results (POSTags variable)
                dynamic result = pyScope.GetVariable("POSTags");

                // Convert the IronPython result to .NET type if necessary
                IList<object> posTagsList = (result).AsList();
                pos_tags = posTagsList.Cast<string>().ToArray();
                */

                int n_adjectives = 0, n_verbs = 0, n_nouns = 0, n_articles = 0;
                foreach (var tag in pos_tags)
                {
                    if (tag == "ADJ" || tag == "A" || tag == "NO" || tag == "AP") { n_adjectives++; }  // A = adjective, NO = ordinal number, AP = possessive adjective
                    else if (tag == "VERB" || tag == "V" || tag == "AUX") { n_verbs++; }  // V = verb, AUX = auxiliary   
                    else if (tag == "NOUN" || tag == "PROPN" || tag == "SP" || tag == "S") { n_nouns++; }  // SP = proper noun, S = common noun
                    else if (tag == "DET" || tag == "RD" || tag == "RI" ) { n_articles++; } // RD = definite article, RI = indefinite article
                }
                int n_words = pos_tags.Length;
                vdb_adjectives_rate = n_adjectives / n_words;
                vdb_verbs_rate = n_verbs / n_words;
                vdb_nouns_rate = n_nouns / n_words;
                vdb_articles_rate = n_articles / n_words;

                // Rateos of words in basic and full vocabulary
                string base_path_wordlists = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "wordList");
                string basic_dict_path = Path.Combine(base_path_wordlists, "it_basic.txt");
                string[] basic_dict = File.ReadAllLines(basic_dict_path);
                int n_basic_voc_words = 0;
                foreach (string word in bodyTokens)
                {
                    if (basic_dict.Contains(word)) { n_basic_voc_words++; }
                }
                voc_rate = (float)(n_words - n_misspelled_words) / n_words;  // n_mispelled_words are the number of words that are not in the dictionary
                vdb_rate = (float)n_basic_voc_words / n_words;
            } else
            {
                voc_rate = 0;
                vdb_rate = 0;
                vdb_adjectives_rate = 0;
                vdb_verbs_rate = 0;
                vdb_nouns_rate = 0;
                vdb_articles_rate = 0;
            }

            return (n_misspelled_words, n_phishy, n_scammy, 
                vdb_adjectives_rate, vdb_verbs_rate, vdb_nouns_rate, vdb_articles_rate,
                voc_rate, vdb_rate);
        }
        private static string GetDictionaryPath(string language)
        {
            string dictPath = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "dict");
            if (_languages.Contains(language))  // Loads additional dictionary
            {
                try
                {
                    dictPath = Path.Combine(dictPath, language);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
            else  // Default language is English
            {
                dictPath = Path.Combine(dictPath, "en");
            }
            return dictPath;
        }
    }
}
