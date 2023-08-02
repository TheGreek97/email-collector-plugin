using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace PhishingDataCollector
{
    internal class MailData
    {
        /* Data to save */
        public List<string> ServersInReceivedHeaders = new List<string>(); // will contain the servers in the Received headers    
        public string OriginServer;
        public string EmailFolderName;  // additional information
        public bool UserReadEmail;  // additional information
        public char[] SpecialCharactersBody;
        public DateTime EmailDate;  // additional information
        public Dictionary<string, float> SensitiveWordsTFs;

        /* Features */
        // Header features
        public int n_recipients;
        public bool plain_text;
        public int n_hops;
        public short n_smtp_servers_blacklist;
        public string email_origin_location;
        public readonly int mail_size;

        // Subject features
        public int n_words_subject;
        public int n_char_subject;
        public bool is_non_ASCII_subject;
        public sbyte is_re_fwd_subject;

        // Body features
        public int n_html_comments_tag;
        public int n_words_body;
        public int n_images;
        public int body_size;
        public float proportion_words_no_vowels;
        public int n_href_attr;
        public int account_count_in_body;
        public float outbound_count_average;
        public int n_table_tag;
        public float automated_readability_index;
        public int n_link_mismatch;
        public int n_links;
        public int n_links_IP;
        public float cap_ratio;
        public int n_links_ASCII;
        public bool binary_URL_bag_of_words;
        public int bank_count_in_body;

        public string language;
        public float voc_rate;
        public float vdb_rate;
        public float vdb_adjectives_rate;
        public float vdb_verbs_rate;
        public float vdb_nouns_rate;
        public float vdb_articles_rate;
        public int n_disguisy;
        public int n_phishy;
        public int n_scammy;
        public int n_misspelled_words;
        public int n_special_characters_body;

        public float vt_l_rate;
        public short vt_l_maximum;
        public short vt_l_positives;
        public short vt_l_clean;
        public short vt_l_unknown;

        // URL features
        public List<URLData> MailURLs = new List<URLData>();  // additional information

        // Attachments Features
        public byte n_attachments;
        public byte n_image_attachments;
        public byte n_application_attachments;
        public byte n_message_attachments;
        public byte n_text_attachments;
        public byte n_video_attachments;
        public double attachments_size;
        public float vt_a_rate;
        public int vt_a_maximum;
        public byte vt_a_positives;
        public byte vt_a_clean;
        public byte vt_a_unknown;
        //public byte vt_a_vulnerable;  These involve considering the Corporate anti-virus - we don't have this information
        //public byte vt_a_partial;
        //public byte vt_a_protected;

        /* Private Fields */
        private string ID => _mailID;
        private readonly int _num_recipients;
        private readonly string _mailID, _mailSubject, _mailBody, _HTMLBody, _emailSender, _plainTextBody;
        private readonly string[] _mailHeaders;
        private readonly AttachmentData[] _mailAttachments;

        // Utility regexes
        private Regex _ip_address_regex = new Regex(@"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
        private Regex _url_address_regex = new Regex(@"(https?:\/\/|www\.)[-a-zA-Z0-9@:%._\-\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b[-a-zA-Z0-9()@:%_+.~#?&\/=\-]*", RegexOptions.IgnoreCase);
        private Regex _domain_regex = new Regex(@"(https?:\\/\\/|www\\.)?[-a-zA-Z0-9@:%._\\-\\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b", RegexOptions.IgnoreCase);

        /* Collections of already processed urls using APIs
        private OriginIPCollection EmailOriginIPs = new OriginIPCollection();
        private BlacklistURLsCollection BlacklistedURLs = new BlacklistURLsCollection();
        private VirusTotalScansCollection VirusTotalScans = new VirusTotalScansCollection();
        */

        public MailData(RawMail mail)
        {
            // Set private fields
            _mailID = mail.EntryID;
            mail_size = mail.Size;
            _mailHeaders = mail.Headers;
            _mailSubject = mail.Subject;
            _mailBody = mail.Body;
            _HTMLBody = mail.HTMLBody;
            _plainTextBody = BodyFeatures.GetPlainTextFromHtml(_mailBody);
            _emailSender = mail.Sender;
            _mailAttachments = mail.Attachments;
            _num_recipients = mail.NumRecipients;
            EmailFolderName = mail.Folder;
            UserReadEmail = mail.IsRead;
            EmailDate = mail.Date;
        }

        public string GetID() { return _mailID; }
        public void ComputeFeatures()
        {
            // Compute the email features
            // -- Header features
            n_recipients = _num_recipients;
            plain_text = _mailBody == _HTMLBody;
            ComputeHeaderFeatures();

            // -- Subject features
            ComputeSubjectFeatures();

            // -- Body features
            ComputeBodyFeatures();

            // ---- Body features that involve links
            ComputeLinkBodyFeatures();  // Side-effect: This also sets MailURLs

            // -- URL features 
            for (int i=0; i < MailURLs.Count; i++)
            {
                bool urlAlreadyComputed = false;
                for (int k = i-1; k >= 0; k--)
                {
                    if (MailURLs[i].GetURL() == MailURLs[k].GetURL()) { 
                        urlAlreadyComputed = true;
                        MailURLs[i] = MailURLs[k];
                        break; 
                    }
                }
                if (urlAlreadyComputed) { break; }
                MailURLs[i]?.ComputeURLFeatures();
            }

            // -- Attachment features
            ComputeAttachmentsFeatures();

            //Debug.WriteLine("Features computed for mail with ID: " + _mailID);
            return;
        }

        private void ComputeBodyFeatures()
        {
            //Feature n_html_comments_tag
            n_html_comments_tag = Regex.Matches(_HTMLBody, @"<!--\b").Count;
            //Feature n_words_body
            n_words_body = Regex.Matches(_plainTextBody, @"(\w+)").Count;
            //Feature n_images
            n_images = Regex.Matches(_HTMLBody, @"<img", RegexOptions.IgnoreCase).Count;
            //Feature proportion_words_no_vowels
            proportion_words_no_vowels = Regex.Matches(_mailBody, @"\b([^aeiou\s]+)\b", RegexOptions.IgnoreCase).Count / (float) n_words_body;
            //Feature n_href_attr
            n_href_attr = Regex.Matches(_HTMLBody, @"href\s*=", RegexOptions.IgnoreCase).Count;
            //Feature n_table_tag
            n_table_tag = Regex.Matches(_HTMLBody, @"<\s*table[^>]*>[^<]*<\s*\\table\s*>", RegexOptions.IgnoreCase).Count;

            //Feature cap_ratio
            var n_lowercase_chars_body = Regex.Matches(_plainTextBody, "[a-z]").Count;
            cap_ratio = n_lowercase_chars_body > 0 ? Regex.Matches(_plainTextBody, "[A-Z]").Count / (float) n_lowercase_chars_body : 0;

            //Feature n_special_characters_body
            SpecialCharactersBody = BodyFeatures.GetSpecialChars(_plainTextBody).ToArray();
            n_special_characters_body = SpecialCharactersBody.Distinct().Count();

            //Feature language
            language = BodyFeatures.GetLanguage(_plainTextBody);

            //Feature automated_readability_index (based on the mail's detected language)
            automated_readability_index = BodyFeatures.GetReadabilityIndex(_plainTextBody, language);

            //Feature body_size
            body_size = System.Text.Encoding.Unicode.GetByteCount(_HTMLBody);

            //Feature bank_count_in_body
            bank_count_in_body = BodyFeatures.GetBankCountFeature(_mailBody, language);
            //Feature outbound_count_average
            outbound_count_average = BodyFeatures.GetOutboundCountAverageFeature(_mailBody, language);
            //Feature account_count_in_body
            account_count_in_body = BodyFeatures.GetAccountCountFeature(_mailBody, language);
            //Needed for sensitive_words_body_TFIDF
            SensitiveWordsTFs = BodyFeatures.GetSensitiveWordsTFs(_plainTextBody, n_words_body);

            //Features: n_misspelled_words, n_phishy, n_scammy, vdb_adjectives_rate, vdb_verbs_rate, vdb_nouns_rate, vdb_articles_rate, voc_rate, vdb_rate
            var word_features = BodyFeatures.GetWordsFeatures(_plainTextBody, language);
            n_misspelled_words = word_features.n_misspelled_words;
            n_phishy = word_features.n_phishy;
            n_scammy = word_features.n_scammy;
            vdb_adjectives_rate = word_features.vdb_adjectives_rate;
            vdb_verbs_rate = word_features.vdb_verbs_rate;
            vdb_nouns_rate = word_features.vdb_nouns_rate;
            vdb_articles_rate = word_features.vdb_articles_rate;
            voc_rate = word_features.voc_rate;
            vdb_rate = word_features.vdb_rate;
        }

        /**
         * Computes the features of the body that refer to links in the email: 
         * binary_URL_bag_of_words, vt_l_maximum, vt_l_positives, vt_l_clean, vt_l_unknown
         * It also sets the most dangerous URL of the email (URLMail) based on these features 
         */
        private void ComputeLinkBodyFeatures()
        {
            var links= new List<string>();
            var visibleLinks = new List<string>();  // if the email is in HTML, we store here the actually visible links
            if (plain_text)  // If the email is not in HTML
            {
                MatchCollection linksMatch = _url_address_regex.Matches(_mailBody);
                foreach (Match lMatch in linksMatch)
                {
                    string link = lMatch.Value;
                    links.Add(link);
                }
            }
            else
            {
                MatchCollection _anchors = Regex.Matches(_HTMLBody, @"<a [^>]*href\s*=\s*(\'[^\']*\'|""[^""]*"").*>\s*([^<\s]*)\s*<\s*\/a\s*>", RegexOptions.IgnoreCase);
                foreach (Match anchorLink in _anchors)
                {
                    string link = anchorLink.Groups[1].Value;  // the quoted URL is found in the second? matching group
                    link = link.Trim(new[] { '\'', '"' });  // removes the "" or '' around the link
                    string visibleLink = anchorLink.Groups[2].Value;
                    visibleLinks.Add(visibleLink);
                    links.Add(link);
                }
            }
            var visibleLinksIndexed = visibleLinks.ToArray(); // links and visibleLinks are ordered with the same indexes
            //List<URLData> urls_in_mail = new List<URLData>();  // We store here the scans for each URL in the email
            for (int i=0; i< links.Count; i++)
            {
                string link = links[i];
                if (!Regex.IsMatch(link, @"^(?:phone|mailto|tel|sms|callto):") && !string.IsNullOrEmpty(link))
                {
                    URLData url = new URLData(link);
                    // Will be executed in batch after the data collection 
                    /*
                    VirusTotalScan alreadyAnalyzed = (VirusTotalScan)VirusTotalScans.Find(url.GetHostName());   // Checks if the link's hostname has already been analyzed
                    if (alreadyAnalyzed == null)
                    {
                        VirusTotalScan link_scan = new VirusTotalScan(url.GetHostName());
                        VirusTotal_API.PerformAPICall(link_scan);
                        
                        VirusTotalScans.Add(link_scan);
                        url.SetVTScan(link_scan);
                    }
                    else
                    {
                        url.SetVTScan(alreadyAnalyzed);
                    }
                    */
                    
                    //Feature n_link_mismatch
                    if (!plain_text && i < visibleLinksIndexed.Length)
                    {
                        if (link != visibleLinksIndexed[i])
                        {
                            n_link_mismatch++;
                        }
                    }
                    //Feature n_links_IP
                    if (_ip_address_regex.Match(link).Success)
                    {
                        n_links_IP++;
                    }
                    //Feature n_links_ASCII
                    if (Regex.Match(link, @"[^[:ascii:]]").Success)
                    {
                        n_links_ASCII++;
                    }
                    MailURLs.Add(url);
                    //urls_in_mail.Add(url);
                    if (!binary_URL_bag_of_words)  // This feature is true if at least one link contains one of the keywords
                    {
                        binary_URL_bag_of_words = Regex.IsMatch(link, @"click|here|login|update");
                    }
                }
                //Feature n_links
                n_links = MailURLs.Count;
            }
            /*
            vt_l_maximum = 0;
            vt_l_positives = 0;
            vt_l_clean = 0;
            vt_l_unknown = 0;
            URLData secondCandidate = null;
            foreach (URLData _u in urls_in_mail)
            {
                VirusTotalScan vt = _u.GetVTScan();
                if (vt.IsUnkown)
                {
                    vt_l_unknown++;
                    secondCandidate = _u;
                }
                else
                {
                    if (vt.NMalicious == 0)
                    {
                        vt_l_clean++;
                    }
                    else if (vt.NMalicious > 0)
                    {
                        vt_l_positives++;
                        if (vt_l_maximum < vt.NMalicious)
                        {
                            vt_l_maximum = vt.NMalicious;
                            MailURL = _u;  // Take _u as the most dangerous link
                        }
                    }
                }
            }
            if (urls_in_mail.Count > 0)
            {
                vt_l_rate = vt_l_positives / urls_in_mail.Count;
            } else
            {
                vt_l_rate = 0;
            }
            // Based on these 5 features, we take the most dangerous URL and compute the URL feature on that URL
            if (MailURL == null && urls_in_mail.Count > 0)
            {
                if (secondCandidate == null) {  // take one link at random
                    Random random = new Random();
                    MailURL = urls_in_mail[random.Next(0, urls_in_mail.Count - 1)];                   
                }
                else { MailURL = secondCandidate; }
            }*/
        }

        private void ComputeSubjectFeatures()
        {
            if (!string.IsNullOrEmpty(_mailSubject))
            {
                n_words_subject = Regex.Matches(_mailSubject, @"(\w+)").Count;
                n_char_subject = _mailSubject.Length;
                is_non_ASCII_subject = Regex.IsMatch(_mailSubject, @"[^\x00-\x7F]");
                if (Regex.IsMatch(_mailSubject, @"fwd:", RegexOptions.IgnoreCase))
                {
                    is_re_fwd_subject = Regex.IsMatch(_mailSubject, @"re:", RegexOptions.IgnoreCase) ? (sbyte)3 : (sbyte)2; // 3 = re+fwd, 2 = fwd
                }
                else
                {
                    is_re_fwd_subject = Regex.IsMatch(_mailSubject, @"re:", RegexOptions.IgnoreCase) ? (sbyte)1 : (sbyte)0; // 1 = re, 0 = none
                }
            }
            else
            {
                n_words_subject = 0;
                n_char_subject = 0;
                is_non_ASCII_subject = false;
                is_re_fwd_subject = 0;
            }
        }

        private void ComputeHeaderFeatures()
        {
            n_hops = 0;
            Regex header_rx = new Regex(@"^(X-)?Received:", RegexOptions.IgnoreCase);  //"Received" or "X-Received" headers
  
            int x_originating_ip_idx = -1; //, x_originating_email_idx=-1;
            for (int i = 0; i < _mailHeaders.Length; i++)
            {
                if (header_rx.Match(_mailHeaders[i]).Success)
                {
                    n_hops++;
                    Match match_ip = _ip_address_regex.Match(_mailHeaders[i]);
                    if (match_ip.Success)
                    {  //  try to match an IP address  
                        ServersInReceivedHeaders.Add(match_ip.Value);
                    }
                    else
                    {  //  try to match a domain URL
                        Match match_url = _domain_regex.Match(_mailHeaders[i]);
                        if (match_url.Success) { ServersInReceivedHeaders.Add(match_url.Value); }
                    }
                }
                else if (_mailHeaders[i].StartsWith("X-Originating-IP"))
                {
                    x_originating_ip_idx = i;
                }
            }
            //  Blacklists check of the traversed mailservers  -n_smtp_servers_blacklist-
            /* To be performed in batch
             * foreach (string mail_server in servers_in_received_headers)
            {
                // API call to check the mail_server against more than 100 blacklists
                BlacklistURL alreadyAnalyzedURL = (BlacklistURL)BlacklistedURLs.Find(mail_server);    // Checks if the IP has already been analyzed
                if (alreadyAnalyzedURL == null)
                {
                    BlacklistURL blacklistsResult = new BlacklistURL(mail_server);
                    BlacklistURL_API.PerformAPICall(blacklistsResult);
                    BlacklistedURLs.Add(blacklistsResult);  // Adds the server and its result to the list of already analyzed servers
                    if (blacklistsResult.GetFeature() > 0) { n_smtp_servers_blacklist++; }  // If the server appears in at least 1 blacklist, we increase the feature by 1
                }
                else  // The mailserver has already been analyzed, so we take the available result
                {
                    if (alreadyAnalyzedURL.NBlacklists > 0) { n_smtp_servers_blacklist++; }
                }
            }*/

            // email_origin_location - Email Origin Location
            if (x_originating_ip_idx >= 0)  // If "X-Originating-IP" has a value, use it!
            {
                string header_to_consider = _mailHeaders[x_originating_ip_idx];
                Regex origin_rx = new Regex(@"\[(.*)\]");  // Gets the orginating IP address
                Match origin_match = origin_rx.Match(header_to_consider);
                if (origin_match.Success) { OriginServer = origin_match.Groups[1].Value; }
            }
            else if (ServersInReceivedHeaders.Count > 0)  // Else, try to use the last "Received" header
            {
                OriginServer = ServersInReceivedHeaders.Last();
            }
            /*
             * To be performed in batch
            OriginIP alreadyAnalyzedIP = (OriginIP)EmailOriginIPs.Find(origin_server);    // Checks if the IP has already been analyzed
            if (alreadyAnalyzedIP == null)
            {
                OriginIP originResult = new OriginIP(origin_server);
                OriginIP_API.PerformAPICall(originResult);
                EmailOriginIPs.Add(originResult);  // Adds the IP and its result to the list of already analyzed IPs
                email_origin_location = originResult.GetFeature();
            }
            else
            {
                email_origin_location = alreadyAnalyzedIP.GetFeature();  // If the IP has already been analyzed, take the available result
            }
            */
            return;
        }

        private void ComputeAttachmentsFeatures()
        {
            n_attachments = (byte)_mailAttachments.Count();
            n_image_attachments = 0;
            n_application_attachments = 0;
            n_message_attachments = 0;
            n_text_attachments = 0;
            n_video_attachments = 0;
            vt_a_positives = 0;
            vt_a_clean = 0;
            vt_a_maximum = 0;
            vt_a_unknown = 0;
            long[] att_sizes = new long[n_attachments];  // we don't directly sum up the sizes to avoid overflow
            int i = 0;
            if (n_attachments > 0)
            {
                foreach (AttachmentData att in _mailAttachments)
                {
                    // VirusTotalScan link_scan = new VirusTotalScan(att.SHA256, isAttachment: true);  Not computed realtime 
                    // VirusTotal_API.PerformAPICall(link_scan);  Not computed realtime 
                    switch (att.GetAttachmentType())
                    {
                        case "image": n_image_attachments++; break;
                        case "application": n_application_attachments++; break;
                        case "message": n_message_attachments++; break;
                        case "text": n_text_attachments++; break;
                        case "video": n_video_attachments++; break;
                    }
                    att_sizes[i] = att.Size;
                    /* Not computed realtime 
                    if (link_scan.NMalicious > 0) 
                    {
                        vt_a_positives++;
                        vt_a_maximum = link_scan.NMalicious > vt_a_maximum ? link_scan.NMalicious : vt_a_maximum;
                    }
                    else
                    {
                        if (link_scan.NHarmless > 0) { vt_a_clean++; }  // if there's at least 1 "harmless" vote, we can consider it clean
                        else { vt_a_unknown++; }  // otherwise, the attachment is unknown to VirusTotal
                    }*/
            i++;
                }
                attachments_size = att_sizes.Average();
                // vt_a_rate = vt_a_positives / n_attachments;  Not computed realtime 
            }
        }
    }
}
