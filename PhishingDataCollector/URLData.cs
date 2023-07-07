using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace PhishingDataCollector
{
    internal class URLData
    {
        private readonly string _URL;
        private readonly string _TLD;
        private readonly IPAddress _IP;
        private readonly string _hostName;  // Host name (e.g., "www.studenti.uniba.it")
        private readonly string _domainName;  // Domain name (e.g., "uniba.it")
        private readonly string _protocolHostName;  // Protocol + Host Name (e.g., https://www.studenti.uniba.it)
        private readonly string _protocol;
        private readonly string _port;
        private readonly string _path;  // URL path (e.g., /segreteria/libretto?q=123&p=true)
        private readonly string[] _comTLDs = { ".com", ".org", ".edu", ".gov", ".io", ".uk", ".net", ".ca", ".de", ".jp", ".fr", 
            ".au", ".us", ".ru", ".ch", ".it", ".nl", ".se", ".no", ".es", ".mil", ".info", ".tk", ".cn", ".xyz", ".top" };  // most common top-level domains
        private VirusTotalScan VTScan { get; set; }

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
        public bool domain_includes_dash;
        public int hostname_length;
        public int path_length;
        public bool out_of_position_TLD;
        public bool domain_in_path;
        public double entropy_chars_URL;
        public double kullback_leibler_divergence;
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
        public URLData(string uRL)
        {
            uRL = Regex.Replace(uRL, @"[\\""']+", "");
            _URL = uRL;
            Match urlMatch = Regex.Match(uRL, @"^(?:(\w+):\/\/)?(?:[^@\/\n]+@)?((?:www\.)?[^:\/?\n]+)\:?(\d)*(\/.*)?", RegexOptions.IgnoreCase);
            _protocol = urlMatch.Groups[1].Value;  // the protocol can be http, https, ftp, etc.
            _hostName = urlMatch.Groups[2].Value; // Host name (e.g., "www.studenti.uniba.it")
            _port = urlMatch.Groups[3].Value;  // Port (e.g., "8080")
            _path = urlMatch.Groups[4].Value;   // Path in the URL (e.g., "/segreteria/libretto?q=par&t=true")
            if (!string.IsNullOrEmpty(_hostName))
            {
                _protocolHostName = string.IsNullOrEmpty(_protocol) ? _hostName : _protocol + "://" + _hostName;  // e.g. https:://www.uniba.it
                Match domainMatch = Regex.Match(_hostName, @"\w+(\.\w+)$");  // Domain name (e.g., "uniba.it")
                _domainName = domainMatch.Groups[0].Value;  // the full match
                _TLD = domainMatch.Groups[1].Value;
                if (string.IsNullOrEmpty(_domainName))
                {
                    _domainName = _hostName;
                }
            }
        }

        public void ComputeURLFeatures()
        {
            // Feature n_dashes
            n_dashes = Regex.Matches(_URL, "-").Count;
            // Feature n_underscores
            n_underscores = Regex.Matches(_URL, "_").Count;
            // Feature n_dots
            n_dots = Regex.Matches(_URL, ".").Count;
            // Feature n_digits
            n_digits = Regex.Matches(_URL, "[0-9]").Count;
            // Fetaure digit_letter_ratio
            digit_letter_ratio = n_digits / Regex.Matches(_URL, "[A-z]").Count;

            ComputeProtocolPortMatchFeature();
            has_https = Regex.IsMatch(_protocol, "https", RegexOptions.IgnoreCase);

            // Fetaure n_slashes
            n_slashes = Regex.Matches(_URL, "/").Count;
            // Feature TLD
            TLD = _TLD;
            // Feature url_length
            url_length = _URL.Length;

            // Feature average_domain_token_length
            string _domain = Regex.Match(_TLD, @"([\w\-_]+\.)+\w+", RegexOptions.IgnoreCase).Value;
            string[] temp = _domain.Split('.');
            // Feature n_domains
            n_domains = temp.Length;
            foreach (string s in temp)
            {
                average_domain_token_length += s.Length;
            }
            average_domain_token_length = average_domain_token_length / n_domains;
            // Feature hostname_length
            hostname_length = temp[0].Length;
            // Feature domain_includes_dash
            domain_includes_dash = Regex.Match(_domain, @"\w\-\w").Success;
            // Feature path_length
            path_length = _TLD.Length - _domain.Length - 1;
            // Feature n_query_components
            n_query_components = Regex.Match(_URL, @"\?((\w+(=\w)*)+&?)+", RegexOptions.IgnoreCase).Value.Split('&').Length;
            // Feature out_of_position_TLD
            ComputeOutOfPositionTLDFeature();
            // Feature domain_in_path
            ComputeDomainInPathFeature();
            // Feature entropy_chars_URL
            ComputeEntropyCharsURLFeature();
            // Feature kullback_leibler_divergence
            ComputeKullbackLeiblerDivergenceFeature();
            // Feature entropy_NAN_chars_URL
            ComputeEntropyNANCharsURLFeature();
        }

        public void ComputeDomainFeatures()
        {
            // DNS Lookup
            DNSInfo dnsInfo = new DNSInfo(_domainName);  // try to see if domain.com is needed instead of sub.domain.com
            DNSInfo_API.PerformAPICall(dnsInfo);
            DNS_TTL = dnsInfo.GetFeatureTTL();
            DNS_info_exists_binary = dnsInfo.GetFeatureDNSInfoExists();

            // Page Rank
            PageRank pr = new PageRank(_protocolHostName);
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
        }

        public string GetHostName() { return _hostName; }
        public VirusTotalScan GetVTScan() { return VTScan; }
        public void SetVTScan(VirusTotalScan vt) { VTScan = vt; }

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
        private void ComputeKullbackLeiblerDivergenceFeature()
        {
            // The relative entropy of characters in the URL and standard English character
            Dictionary<char, float> letterFrequencyEnglish = new Dictionary<char, float>() {
                { 'E', 12.0f } , { 'T' , 9.10f }, { 'A' , 8.12f }, { 'O' , 7.68f }, { 'I' , 7.31f }, { 'N' , 6.95f }, { 'S' , 6.28f },
                { 'R' , 6.02f }, { 'H' , 5.92f }, { 'D' , 4.32f }, { 'L' , 3.98f }, { 'U' , 2.88f }, { 'C' , 2.71f }, { 'M' , 2.61f },
                { 'F' , 2.30f }, { 'Y' , 2.11f }, { 'W' , 2.09f }, { 'G' , 2.03f }, { 'P' , 1.82f }, { 'B' , 1.49f }, { 'V' , 1.11f },
                { 'K' , 0.69f }, { 'X' , 0.17f }, { 'Q' , 0.11f }, { 'J' , 0.10f }, { 'Z' , 0.07f }
            };
            // kullback_leibler_divergence -  DKL(P || Q) = \sum_{x \in X}p(x)*log{p(x)/q(x)}
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
            foreach (char c in charOccurences.Keys)
            {
                double px = (float)charOccurences[c] / lettersInURL;  // p(x)
                kullback_leibler_divergence += px * Math.Log(px / letterFrequencyEnglish[c]);  //p(x)*log(p(x)/q(x))
            }
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
            foreach (string common_tld in _comTLDs)  // OR if another TLD (than the one in the URL) appears at all in the URL
            {
                if (common_tld != _TLD && _URL.Contains(common_tld)) {
                    out_of_position_TLD = true;
                    return;
                } 
            }
            out_of_position_TLD = false;  // otherwise, the URL is fine
        }

        private void ComputeDomainInPathFeature()
        {
            domain_in_path = false;
            if (!string.IsNullOrEmpty(_path))
            {
                foreach (string common_tld in _comTLDs)
                {
                    if (_path.Contains(common_tld))
                    {
                        domain_in_path = true;
                        return;
                    }
                }
            }
        }
    }
}
