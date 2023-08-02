using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PhishingDataCollector
{
    internal class URLData
    {
        private readonly string _URL;
        private readonly string _TLD;
        private readonly string _hostName;  // Host name (e.g., "www.studenti.uniba.it")
        private readonly string _domainName;  // Domain name (e.g., "uniba.it")
        private readonly string _protocol;
        private readonly string _port;
        private readonly string _fullPath;  // URL path including query string (e.g., /segreteria/libretto?q=123&p=true)
        private readonly string _path;  // URL path without query string (e.g. /segreteria/libretto)
        private readonly string _query;  // Query string in URL (e.g. ?val=321&q=hi)
        private readonly string[] _commonTLDs = { ".com", ".org", ".edu", ".gov", ".io", ".uk", ".net", ".ca", ".de", ".jp", ".fr",
            ".au", ".us", ".ru", ".ch", ".it", ".nl", ".se", ".no", ".es", ".mil", ".info", ".tk", ".cn", ".xyz", ".top" };  // most common top-level domains
        private readonly string[] _sensitiveWords = { "secure", "account", "webscr", "login", "ebayisapi", "signin", "banking", "confirm" };
        private readonly Dictionary<char, float> _letterFrequencyEnglish = new Dictionary<char, float>() {
            { 'E', 0.12f } , { 'T' , 0.091f }, { 'A' , 0.0812f }, { 'O' , 0.0768f }, { 'I' , 0.0731f }, { 'N' , 0.0695f }, { 'S' , 0.0628f },
            { 'R' , 0.0602f }, { 'H' , 0.0592f }, { 'D' , 0.0432f }, { 'L' , 0.0398f }, { 'U' , 0.0288f }, { 'C' , 0.0271f }, { 'M' , 0.0261f },
            { 'F' , 0.0230f }, { 'Y' , 0.0211f }, { 'W' , 0.0209f }, { 'G' , 0.0203f }, { 'P' , 0.0182f }, { 'B' , 0.0149f }, { 'V' , 0.0111f },
            { 'K' , 0.0069f }, { 'X' , 0.0017f }, { 'Q' , 0.0011f }, { 'J' , 0.0010f }, { 'Z' , 0.0007f }
        };  // contains the frequencies of the letters in the English language
        private readonly string[] _commonFreeDomains = { "000webhostapp.com", "weebly.com", "umbler.com", "16mb.com", "godaddysites.com",
            "webcindario.com", "ddns.net", "joomla.org", "webnode.com", "wordpress.com", "altervista.org", "wix.com", "hostinger.", "sites.google.com" };
        private readonly string[] shortenedDomains = { "t.co", "ow.ly", "bit.ly", "tinyurl.com", "rb.gy", "tiny.cc", "bit.do", "festyy.com", "cutt.ly", "goo.gl" };

        //private VirusTotalScan VTScan { get; set; }

        // Protocol + Host Name (e.g., https://www.studenti.uniba.it)
        public readonly string FullHostName;

        //URL features
        public int n_dashes;
        public int n_underscores;
        public int n_dots;
        public int n_digits;
        public float digit_letter_ratio;
        public bool has_https;
        public bool protocol_port_match_binary;
        public int n_slashes;
        public string TLD;
        public int url_length;
        public int n_domains;
        public float average_domain_token_length;
        public int n_query_components;
        public short n_sensitive_words;
        public float url_char_distance_w;
        public float url_char_distance_r;
        public int domain_length;
        public bool shortened_service;
        public bool IP_address;
        public bool at_symbol;
        public bool prefixes_suffixes;
        public int n_tld_in_paths;
        public int hostname_longest_number_length;
        public int hostname_longest_word_length;
        public int n_special_characters_URL;
        public bool exe_file;
        public bool slash_redirect;
        public bool embedded_domain;
        public bool domain_includes_dash;
        public int hostname_length;
        public int path_length;
        public bool out_of_position_TLD;
        public bool domain_in_path;
        public double entropy_chars_URL;
        public double kullback_leibler_divergence;
        public double euclidean_distance;
        public double entropy_nan_chars_URL;

        //Domain-based features
        public bool DNS_info_exists_binary;
        public int DNS_TTL;
        public byte page_rank;
        public int? website_traffic;
        public double domain_creation_date;
        public double domain_expiration_date;
        public double domain_reg_length;
        public bool abnormal_URL;
        public byte n_name_servers;
        public bool https_not_trusted;
        public bool free_hosting;

        public URLData(string uRL)
        {
            uRL = Regex.Replace(uRL, @"[\\""']+", "");  // URL cleaning
            _URL = uRL;
            Match urlMatch = Regex.Match(uRL, @"^(?:(\w+):\/\/)?(?:[^@\/\n]+@)?((?:www\.)?[^:\/?\n]+)\:?(\d*)(\/[^?]*)?(\?.*)?", RegexOptions.IgnoreCase);
            _protocol = urlMatch.Groups[1].Value;  // the protocol can be http, https, ftp, etc.
            _hostName = urlMatch.Groups[2].Value; // Host name (e.g., "www.studenti.uniba.it")
            _port = urlMatch.Groups[3].Value;  // Port (e.g., "8080")
            _path = urlMatch.Groups[4].Value;  // Path in the URL (e.g., "/segreteria/libretto")
            _query = urlMatch.Groups[5].Value;  // Query string in the URL (e.g., ?q=par&t=true")
            _fullPath = _path + _query;
            if (!string.IsNullOrEmpty(_hostName))
            {
                FullHostName = string.IsNullOrEmpty(_protocol) ? _hostName : _protocol + "://" + _hostName;  // e.g. https:://www.uniba.it
                Match domainMatch = Regex.Match(_hostName, @"\w+(\.\w+)$");  // Domain name (e.g., "uniba.it")
                _domainName = domainMatch.Groups[0].Value;  // the full match
                _TLD = domainMatch.Groups[1].Value;  // the first capturing group contains the top-level domain
                if (string.IsNullOrEmpty(_domainName))
                {
                    _domainName = _hostName;
                }
            }
            else
            {
                throw new ArgumentException("The provided URL is not a valid URL");
            }
        }

        public string GetURL()
        {
            return _URL;
        }

        public void ComputeURLFeatures()
        {
            // Feature n_dashes
            n_dashes = _URL.Count(c => c == '-');
            // Feature n_underscores
            n_underscores = _URL.Count(c => c == '_');
            // Feature n_dots
            n_dots = _URL.Count(c => c == '.');
            // Feature n_digits
            n_digits = _URL.Count(char.IsDigit);
            // Fetaure digit_letter_ratio
            var n_letters = _URL.Count(char.IsLetter);
            digit_letter_ratio = n_letters > 0 ? n_digits / (float) n_letters : 100;
            // Fetaure n_slashes
            n_slashes = _URL.Count(c => c == '/');

            ComputeProtocolPortMatchFeature();
            has_https = Regex.IsMatch(_protocol, "https", RegexOptions.IgnoreCase);

            // Feature TLD
            TLD = _TLD;
            // Feature url_length
            url_length = _URL.Length;

            // Feature average_domain_token_length
            ComputeAvgDomainTokenLength();

            // Feature n_sensitive_words
            n_sensitive_words = 0;
            foreach (string word in _sensitiveWords)
            {
                n_sensitive_words += (short)Regex.Matches(_URL, word).Count;
            }
            // Feature url_char_distance_w
            url_char_distance_w = ((float)_URL.Split(new char[] { 'w', 'W' }).Length / _URL.Length) - _letterFrequencyEnglish['W'];  // frequency of w in the URL - frequency of w in the English language
            // Feature url_char_distance_r
            url_char_distance_r = ((float)_URL.Split(new char[] { 'r', 'R' }).Length / _URL.Length) - _letterFrequencyEnglish['R'];  // frequency of r in the URL - frequency of r in the English language

            // Feature hostname_length
            hostname_length = _hostName.Length;
            // Feature domain_includes_dash
            domain_includes_dash = _domainName.Contains('-');
            // Feature path_length
            path_length = _fullPath.Length;
            // Feature n_query_components
            n_query_components = Regex.Matches(_query, @"[^=?]+(=[^=&]+)?&?").Count;

            //Feature domain_length
            domain_length = _domainName.Length;
            //Feature IP_address
            IP_address = Regex.Match(_hostName, @"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}").Success;
            //Feature exe_file
            exe_file = Regex.Match(_path, @"\.exe$", RegexOptions.IgnoreCase).Success;
            //Feature slash_redirect
            slash_redirect = Regex.Match(_fullPath, @":\/\/").Success;
            //Feature at_symbol
            at_symbol = _URL.Contains('@');
            //Feature prefixes_suffixes
            prefixes_suffixes = Regex.Match(_URL, @"\w+\-\w+").Success;
            //Feature n_tld_in_paths
            ComputeNTLDInPaths();
            //Feature n_special_characters_URL
            n_special_characters_URL = 0;
            foreach (char c in _URL)
            {
                if (! char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
                {
                    n_special_characters_URL++;
                }
            }

            //Features hostname_longest_number_length, hostname_longest_word_length
            ComputeLongestLengthFeatures();
            //Feauture embedded_domain
            ComputeEmbeddedDomainFeature();
            // Feature out_of_position_TLD
            ComputeOutOfPositionTLDFeature();
            // Feature domain_in_path
            ComputeDomainInPathFeature();
            // Feature entropy_chars_URL
            ComputeEntropyCharsURLFeature();
            // Features: kullback_leibler_divergence + euclidean_distance
            ComputeDistanceFeatures();
            // Feature entropy_NAN_chars_URL
            ComputeEntropyNANCharsURLFeature();

            //Feature shortened_service
            ComputeShortenedServiceFeature();
            //Feature free_hosting
            ComputeFreeHostingFeature();
        }

        /* These features will be computed later in batch 
        public void ComputeDomainFeatures()
        {
            // DNS Lookup
            DNSInfo dnsInfo = new DNSInfo(_domainName);  // try to see if domain.com is needed instead of sub.domain.com
            DNSInfo_API.PerformAPICall(dnsInfo);
            DNS_TTL = dnsInfo.GetFeatureTTL();
            DNS_info_exists_binary = dnsInfo.GetFeatureDNSInfoExists();

            // Page Rank
            PageRank pr = new PageRank(FullHostName);
            PageRank_API.PerformAPICall(pr);
            page_rank = pr.GetFeaturePageRank();
            website_traffic = pr.GetFeatureWebsiteTraffic();

            // WhoIS Data
            WhoIS whois = new WhoIS(_domainName);
            WhoIS_API.PerformAPICall(whois);
            domain_creation_date = whois.GetFeatureCreationDate();
            domain_expiration_date = whois.GetFeatureExpirationDate();
            domain_reg_length = whois.GetFeatureDomainRegLength();
            abnormal_URL = whois.GetFeatureAbnormalURL();
            n_name_servers = whois.GetFeatureNumNameServers();
            // Certificate 
            https_not_trusted = whois.GetFeatureSelfSignedHTTPS();

            return;
        }*/

        public string GetHostName() { return _hostName; }
        //public VirusTotalScan GetVTScan() { return VTScan; }
        //public void SetVTScan(VirusTotalScan vt) { VTScan = vt; }

        private void ComputeAvgDomainTokenLength ()
        {
            string[] domainTokens = _hostName.Split('.');
            // Feature n_domains
            n_domains = domainTokens.Length;
            foreach (var token in domainTokens)
            {
                average_domain_token_length += token.Length;
            }
            average_domain_token_length = (float) average_domain_token_length / n_domains;
        }
        private void ComputeLongestLengthFeatures()
        {
            hostname_longest_number_length = 0;
            hostname_longest_word_length = 0;
            int digit_counter = 0, letter_counter = 0;
            for (int i=0; i<_hostName.Length; i++)
            {
                if (char.IsDigit(_hostName[i])) { 
                    digit_counter++;
                    letter_counter = 0; // word ends
                }
                else if (char.IsLetter(_hostName[i])) { 
                    letter_counter++;
                    digit_counter = 0;  // number ends
                }
                else {  // word/number (if any) ends
                    if (digit_counter > hostname_longest_number_length) { hostname_longest_number_length = digit_counter; }
                    if (letter_counter > hostname_longest_word_length) { hostname_longest_word_length = letter_counter; }
                    digit_counter = 0; // reset the counters
                    letter_counter = 0;
                }
            }
        }
        private void ComputeEmbeddedDomainFeature() 
        {
            embedded_domain = false;
            // Check if the path contains a dot-separated domain/hostname
            string[] parts = _path.Split('/');
            foreach (string part in parts)
            {
                // Exclude empty parts and check if the part contains dots
                if (!string.IsNullOrEmpty(part) && part.Contains("."))
                {
                    embedded_domain = true;
                    break;
                }
            }
        }
        private void ComputeNTLDInPaths () 
        {
            n_tld_in_paths = 0;
            foreach (string tld in _commonTLDs)  // foreach common top-level domain
            {
                int pos = 0;
                while ((pos < _URL.Length) && (pos = _URL.IndexOf(tld, pos)) != -1)  // count the occurences in the URL
                {
                    n_tld_in_paths++;
                    pos += tld.Length;
                }
            }
        }
        private void ComputeShortenedServiceFeature() //Feature shortened_service
        {
            bool ret = false;
            foreach (string s in shortenedDomains)
            {
                if (ret = Regex.Match(_URL, s, RegexOptions.IgnoreCase).Success)
                    break;
            }

            shortened_service = ret;
        }
        private void ComputeFreeHostingFeature() //Feature free_hosting
        {
            free_hosting = false;
            foreach (string service in _commonFreeDomains)
            {
                if (Regex.Match(_domainName, service, RegexOptions.IgnoreCase).Success)
                {
                    free_hosting = true;
                    break;
                }
            }

        }
        private void ComputeEntropyCharsURLFeature()
        {
            // Entropy H(X) = - \sum_{x \in X}(p(x)*logp(x))
            Dictionary<char, int> charOccurences = new Dictionary<char, int>();
            foreach (char c in _URL)
            {
                if (!charOccurences.ContainsKey(c)) { charOccurences[c] = 0; }
                charOccurences[c] += 1;
            }
            entropy_chars_URL = 0;
            foreach (char c in charOccurences.Keys)
            {
                double px = (double)charOccurences[c] / _URL.Length;  // p(x)
                entropy_chars_URL += (px * Math.Log(px));
            }
            entropy_chars_URL = -entropy_chars_URL;
        }

        private void ComputeEntropyNANCharsURLFeature()
        {
            string nan_chars = Regex.Replace(_URL, @"[\w]", "");
            // Entropy H(X) = - \sum_{x \in X}(p(x)*logp(x))
            Dictionary<char, int> charOccurences = new Dictionary<char, int>();
            foreach (char c in nan_chars)
            {
                if (!charOccurences.ContainsKey(c)) { charOccurences[c] = 0; }
                charOccurences[c] += 1;
            }
            double cum_sum = 0;  // temporary var for the cumulative sum
            foreach (char c in charOccurences.Keys)
            {
                double px = (double)charOccurences[c] / nan_chars.Length;  // p(x)
                cum_sum += (px * Math.Log(px));  // + p(x) * log(p(x))
            }
            entropy_nan_chars_URL = -cum_sum;
        }
        private void ComputeDistanceFeatures()
        {
            // The relative entropy of characters in the URL and standard English character
            // kullback_leibler_divergence: DKL(P || Q) = \sum_{x \in X}p(x)*log{p(x)/q(x)}
            // euclidean_distance: D (p,q) = \sqrt( \sum_{x \in X}(p(x)-q(x))^2 )
            int lettersInURL = 0;
            Dictionary<char, int> charOccurences = new Dictionary<char, int>();
            foreach (char c in _URL)
            {
                if (char.IsLetter(c))
                {
                    lettersInURL++;
                    char letter = char.ToUpper(c);
                    if (!charOccurences.ContainsKey(letter)) { charOccurences[letter] = 0; }
                    charOccurences[letter] += 1;
                }
            }
            kullback_leibler_divergence = 0;
            euclidean_distance = 0;
            foreach (char c in charOccurences.Keys)
            {
                var px = (double)charOccurences[c] / lettersInURL;  // p(x)
                kullback_leibler_divergence += px * Math.Log(px / _letterFrequencyEnglish[c]);  // p(x)*log(p(x)/q(x))
                euclidean_distance += Math.Pow(px - _letterFrequencyEnglish[c], 2); // (p(x) - q(x))^2
            }
            euclidean_distance = Math.Sqrt(euclidean_distance);
        }
        private void ComputeProtocolPortMatchFeature()
        {
            protocol_port_match_binary = true;
            if (string.IsNullOrEmpty(_protocol))
            {
                return;
            }
            if (_protocol == "http")
            {
                protocol_port_match_binary = string.IsNullOrEmpty(_port) ||
                    _port == "80" || _port == "8000" || _port == "8080" || _port == "8081";
            }
            else if (_protocol == "https")
            {
                protocol_port_match_binary = string.IsNullOrEmpty(_port) || _port == "443";
            }
            else if (_protocol == "file" || _protocol == "mailto" || _protocol == "news" ||
                _protocol == "sms" || _protocol == "callto" || _protocol == "tel")
            {
                protocol_port_match_binary = string.IsNullOrEmpty(_port);
            }
            else if (_protocol == "ftp")
            {
                protocol_port_match_binary = _port == "21";
            }
            else if (_protocol == "ssh")
            {
                protocol_port_match_binary = _port == "22";
            }
            else if (_protocol == "telnet")
            {
                protocol_port_match_binary = _port == "23";
            }
            else if (_protocol == "gopher")
            {
                protocol_port_match_binary = _port == "70";
            }
            else if (_protocol == "rdp")  // Remote Desktop Protocol
            {
                protocol_port_match_binary = _port == "3389";
            }
            else if (_protocol == "ldap")  // Lightweight Directory Access Protocol
            {
                protocol_port_match_binary = _port == "389";
            }
            else if (_protocol == "nntp")  // Network News Transfer Protocol
            {
                protocol_port_match_binary = _port == "119";
            }
        }

        private void ComputeOutOfPositionTLDFeature()
        {
            // Check if a common top level domain (e.g., .com) appears in the URL (outside of its position) 
            // We can simply check:
            if (Regex.Matches(_URL, _TLD).Count > 1)  // if the TLD appears more than once 
            {
                out_of_position_TLD = true;
                return;
            }
            foreach (string common_tld in _commonTLDs)  // OR if another TLD (than the one in the URL) appears at all in the URL
            {
                if (common_tld != _TLD && _URL.Contains(common_tld))
                {
                    out_of_position_TLD = true;
                    return;
                }
            }
            out_of_position_TLD = false;  // otherwise, the URL is fine
        }

        private void ComputeDomainInPathFeature()
        {
            domain_in_path = false;
            if (!string.IsNullOrEmpty(_fullPath))
            {
                foreach (string common_tld in _commonTLDs)
                {
                    if (_fullPath.Contains(common_tld))
                    {
                        domain_in_path = true;
                        return;
                    }
                }
            }
        }
    }
}
