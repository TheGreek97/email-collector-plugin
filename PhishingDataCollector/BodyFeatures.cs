using LanguageDetection;
using NHunspell;
using OpenNLP.Tools.PosTagger;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace PhishingDataCollector
{
    internal static class BodyFeatures
    {
        private static readonly string[] _languages = { "en", "es", "fr", "pt", "it", "de" };
        private static readonly Dictionary<string, string[]> _phishyWords = new Dictionary<string, string[]>() {
            { "en", new string[]{ "account", "security", "user", "verify", "service", "valid", "required", "credentials",
            "attention", "request", "suspended", "company", "bank", "deposit", "post", "money", "update", "verify"} },
            {"it", new string[] { "account", "conto", "sicurezza", "utente", "verifica", "servizio", "valido", "richiesto", "credenziali",
            "attenzione", "richiesta", "sospeso", "azienda", "banca", "deposito", "posta", "soldi", "aggiorna", "verifica"} }
        };
        private static readonly Dictionary<string, string[]> _scammyWords = new Dictionary<string, string[]>() {
            { "en", new string[] { "€", "£", "$", "customer", "prize", "donate", "buy", "pay", "congratulations", "death", "please",
            "response", "dollar", "looking", "urgent", "warning", "win", "offer", "risk", "money", "transaction", "sex", "nude" } },
            { "it", new string[] { "€",  "£", "$", "cliente", "premio", "donare", "dona", "comprare", "compra", "pagare", "paga", "congratulazioni", "morte", "prego", "favore",
            "risposta", "dollari", "cerchiamo", "urgente", "attenzione", "vinto", "offerta", "rischio", "soldi", "transazione", "sesso", "nuda", "nude"} }
        };
        private static readonly Dictionary<string, string[]> _sensitiveWords = new Dictionary<string, string[]>() {
            { "en", new string[] { "unsubscribe", "wrote", "click", "pm", "dear", "remove", "contribution", "mailbox", "receive" } }
        };
        private static readonly Dictionary<string, string> bankTranslations = new Dictionary<string, string>() { 
            { "en", "bank" }, { "es", "banco" }, { "fr", "banque" }, { "pt", "bank" }, { "it", "banca" }, { "de", "bank" } 
        };
        private static readonly Dictionary<string, string> accountTranslations = new Dictionary<string, string>() { 
            { "en", "account" }, { "it", "conto|account" } 
        };

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
            if (body_text == "") return "en";
            LanguageDetector detector = new LanguageDetector();
            detector.AddAllLanguages();
            var language = detector.Detect(body_text);
            language = string.IsNullOrEmpty(language) ? "en" : language;
            return language;
        }

        public static List<char> GetSpecialChars(string body_text)
        {
            var ret = new List<char>();

            foreach (char c in body_text)
            {
                if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
                {
                    ret.Add(c);
                }
            }
            return ret;
        }

        public static int GetBankCountFeature (string body_text, string language)
        {
            string translation = bankTranslations.ContainsKey(language) ? bankTranslations[language] : bankTranslations["en"];
            return Regex.Matches(body_text, translation.ToString(), RegexOptions.IgnoreCase).Count;
        }
        public static int GetOutboundCountAverageFeature (string body_text, string language)
        {
            //string translation = bankTranslations.ContainsKey(language) ? bankTranslations[language] : bankTranslations["en"];
            return Regex.Matches(body_text, "outbound", RegexOptions.IgnoreCase).Count;
        }
        public static int GetAccountCountFeature (string body_text, string language)
        {
            string translation = accountTranslations.ContainsKey(language) ? bankTranslations[language] : bankTranslations["en"];
            return Regex.Matches(body_text, translation.ToString(), RegexOptions.IgnoreCase).Count;
        }
        /* Returns the term frequency of the sensitive words (defined in this class) to compute a posteriori the feature sensitive_words_body_TFIDF
         */
        public static Dictionary<string, float> GetSensitiveWordsTFs (string plain_text_body, int? body_count=null)
        {
            var tfs = new Dictionary<string, float> ();  //dictionary containing the term frequencies for each sensitive word
            if (body_count == null)
            {
                body_count = Regex.Matches(plain_text_body, @"(\w+)").Count;
            }
            /*if (! _sensitiveWords.ContainsKey(language))
            {
                language = "en";
            }*/
            foreach (string word in _sensitiveWords["en"])
            {
                int tf = Regex.Matches(plain_text_body, word, RegexOptions.IgnoreCase).Count;
                if (body_count == 0) { body_count = 1; }  // avoid division by 0
                tfs[word] = tf / (float) body_count;
            }
            return tfs;
        }

        /* This function computes 9 features:
           n_misspelled_words, n_phishy (words), n_scammy (words), vdb_adjectives_rate, vdb_verbs_rate, vdb_nouns_rate, vdb_articles_rate, voc_rate, vdb_rate
         */
        public static (int n_misspelled_words, int n_phishy, int n_scammy,
            float vdb_adjectives_rate, float vdb_verbs_rate, float vdb_nouns_rate, float vdb_articles_rate,
            float voc_rate, float vdb_rate)
            GetWordsFeatures(string body_text, string language = "")
        {
            if (string.IsNullOrEmpty(body_text))
            {
                return (0, 0, 0, 0, 0, 0, 0, 0, 0);
            }
            if (language == "") { language = GetLanguage(body_text); }
            int n_misspelled_words = 0, n_phishy = 0, n_scammy = 0;
            float vdb_adjectives_rate = 0, vdb_verbs_rate = 0, vdb_nouns_rate = 0, vdb_articles_rate = 0;
            float voc_rate = 0, vdb_rate = 0;
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
                        if (_phishyWords.ContainsKey(language) && _phishyWords[language].Contains(word)) { n_phishy++; }
                        if (_scammyWords.ContainsKey(language) && _scammyWords[language].Contains(word)) { n_scammy++; }
                    }
                }
            }
            // POS tagging
            string[] pos_tags = new string[] { };
            string base_path_pos_files = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "POS");
            string[] basic_dict = new string[] { };
            int n_adjectives = 0, n_verbs = 0, n_nouns = 0, n_articles = 0;

            if (language == "en")  // ENGLISH 
            {
                string modelPath = Path.Combine(base_path_pos_files, "Models", "EnglishPOS.nbin");
                //string tagDictDir = Path.Combine(base_path_pos_files, "WordNet", "dict");
                EnglishMaximumEntropyPosTagger posTagger = new EnglishMaximumEntropyPosTagger(modelPath);
                pos_tags = posTagger.Tag(bodyTokens);
                // pos tags are defined this way: https://www.ling.upenn.edu/courses/Fall_2003/ling001/penn_treebank_pos.html
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
                // Get basic dictionary
                string base_path_wordlists = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "wordList");
                string basic_dict_path = Path.Combine(base_path_wordlists, "en_basic.txt");
                //string full_dict_path = Path.Combine(base_path_wordlists, "en_full.txt");
                //string[] full_dict = File.ReadAllLines(full_dict_path);
                //int voc_words = 0;
                basic_dict = File.ReadAllLines(basic_dict_path);

            }
            else if (language == "it")
            {
                // POS Tagging
                var pos_tags_list = new List<string>();
                string py_file_path = Path.Combine(ThisAddIn.POS_PATH, "posTagger_it.exe");
                
                var StartInfo = new ProcessStartInfo();
                StartInfo.FileName = py_file_path;
                StartInfo.Arguments = ThisAddIn.POS_PATH + " \""+body_text+"\"";  //Pass here the location of the it_core_news_sm (and update the py file)
                StartInfo.UseShellExecute = false;
                StartInfo.RedirectStandardOutput = true;
                StartInfo.RedirectStandardError = true;
                StartInfo.CreateNoWindow = true;
                try
                {
                    var p = Process.Start(StartInfo);
                    StreamReader errors = p.StandardError;
                    StreamReader reader = p.StandardOutput;
                    /*if (errors != null) This can lead to deadlock - it should be read asynchronously (https://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why)
                    {
                        var err_string = errors.ReadToEnd();
                        if (! string.IsNullOrEmpty(err_string))
                        {
                            Debug.WriteLine(err_string);
                            ThisAddIn.Logger.Error(err_string);
                        }
                    }*/
                    string output = reader.ReadToEnd();
                    p.WaitForExit();
                    MatchCollection tag_matches = Regex.Matches(output, @"\'([^\']+)\'");
                    foreach (Match match in tag_matches)
                    {
                        pos_tags_list.Add(match.Groups[1].Value);  // Groups[1] contains the group match (without '')
                    }
                } catch (Exception e)
                {
                    //Debug.WriteLine(e);
                    ThisAddIn.Logger.Error("Error during POS tagging - " + e.Message);
                }
                
                /* Python.net implementation to run the python script for POS tagging 
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
                }*/
                /* IronPython does not include external libraries. 
                 * It would be nice to have it, cause IronPython multi-threading, differently from Python.Net
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

                pos_tags = pos_tags_list.ToArray();
                foreach (var tag in pos_tags)
                {
                    if (tag == "ADJ" || tag == "A" || tag == "NO" || tag == "AP") { n_adjectives++; }  // A = adjective, NO = ordinal number, AP = possessive adjective
                    else if (tag == "VERB" || tag == "V" || tag == "AUX") { n_verbs++; }  // V = verb, AUX = auxiliary   
                    else if (tag == "NOUN" || tag == "PROPN" || tag == "SP" || tag == "S") { n_nouns++; }  // SP = proper noun, S = common noun
                    else if (tag == "DET" || tag == "RD" || tag == "RI") { n_articles++; } // RD = definite article, RI = indefinite article
                }

                // Get basic dictionary
                string base_path_wordlists = Path.Combine(Environment.GetEnvironmentVariable("RESOURCE_FOLDER"), "wordList");
                string basic_dict_path = Path.Combine(base_path_wordlists, "it_basic.txt");
                basic_dict = File.ReadAllLines(basic_dict_path);
            }

            int n_words = pos_tags.Length;
            if (n_words > 0)
            {
                // Rateos of adjectives, verbs, nouns, and articles to all words 
                vdb_adjectives_rate = n_adjectives / (float)n_words;
                vdb_verbs_rate = n_verbs / (float)n_words;
                vdb_nouns_rate = n_nouns / (float)n_words;
                vdb_articles_rate = n_articles / (float)n_words;

                int n_basic_voc_words = 0;

                foreach (string word in bodyTokens)
                {
                    if (basic_dict.Contains(word)) { n_basic_voc_words++; }
                    //if (full_dict.Contains(word)) { voc_words++; }
                }

                // Rateos of words in basic and full vocabulary
                voc_rate = (n_words - n_misspelled_words) / (float)n_words;  // n_mispelled_words are the number of words that are not in the dictionary
                vdb_rate = n_basic_voc_words / (float)n_words;
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
                    ThisAddIn.Logger.Error(e);
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
