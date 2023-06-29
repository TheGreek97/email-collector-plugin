using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Policy;
using System;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
using NHunspell;

namespace PhishingDataCollector
{
    internal class MailData
    {
        /* Features */
        // Header features
        public int n_recipients;
        public bool plain_text;
        public int n_hops;
        public short n_smtp_servers_blacklist;
        public string email_origin_location;

        // Subject features
        public int n_words_subject;
        public int n_char_subject;
        public bool is_non_ASCII_subject;
        public sbyte is_re_fwd_subject;

        // Body features
        public int n_html_comments_tag;
        public int n_words_body;
        public int n_images;
        public float proportion_words_no_vowels;
        public int n_href_tag;
        public int n_account_in_body;
        public int n_table_tag;
        public float automated_readability_index;
        public int n_link_mismatch;
        public int n_links;
        public int n_links_IP;
        public float cap_ratio;
        public int n_links_ASCII;
        public int n_misspelled_words;
        public string language;
        public bool binary_URL_bag_of_words;

        public float vt_l_rate;
        public short vt_l_maximum;
        public short vt_l_positives;
        public short vt_l_clean;
        public short vt_l_unknown;

        // URL eatures
        public URLData MailURL;

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

        private readonly int _mailSize, _num_recipients;
        private readonly string _mailID, _mailSubject, _mailBody, _HTMLBody, _emailSender, _plainTextBody;
        private readonly string [] _mailHeaders;
        private readonly AttachmentData[] _mailAttachments;
        private readonly string[] Languages = { "EN", "ES", "FR", "PT", "IT", "DE"};

        // Utility regexes
        private Regex _ip_address_regex = new Regex (@"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
        private Regex _url_address_regex = new Regex (@"(https?:\/\/|www\.)[-a-zA-Z0-9@:%._\-\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b[-a-zA-Z0-9()@:%_+.~#?&\/=\-]*", RegexOptions.IgnoreCase);

        // Collections of already processed urls using APIs
        private OriginIPCollection EmailOriginIPs = new OriginIPCollection ();
        private BlacklistURLsCollection BlacklistedURLs = new BlacklistURLsCollection();
        private VirusTotalScansCollection VirusTotalScans = new VirusTotalScansCollection();


        public MailData(RawMail mail)
        {
            // Set private fields
            _mailID = mail.EntryID;
            _mailSize = mail.Size;
            _mailHeaders = mail.Headers;
            _mailSubject = mail.Subject;
            _mailBody = mail.Body;
            _HTMLBody = mail.HTMLBody;
            _plainTextBody = BodyFeatures.GetPlainTextFromHtml(_mailBody);
            _emailSender = mail.Sender;
            _mailAttachments = mail.Attachments;
            _num_recipients = mail.NumRecipients;
        }

        public MailData(string id, int size, string subject, string body, string htmlBody,
            string sender, int num_recipients, string [] headers, AttachmentData[] attachments)
        {
            // Set private fields
            _mailID = id;
            _mailSize = size;
            _mailHeaders = headers;
            _mailSubject = subject;
            _mailBody = body;
            _HTMLBody = htmlBody;
            _plainTextBody = BodyFeatures.GetPlainTextFromHtml(_mailBody);
            _emailSender = sender;
            _mailAttachments = attachments;
            _num_recipients = num_recipients;
        }
        public string GetID() { return  _mailID; }
        public void ComputeFeatures ()
        {
            // Compute the email features
            // -- Header features
            n_recipients = _num_recipients;
            plain_text = _mailBody == _HTMLBody;
            /* 
             * Disabled for testing 
             * 
            ComputeHeaderFeatures();
            */
            /**/

            // -- Subject features
            ComputeSubjectFeatures();

            // -- Body features
            ComputeBodyFeatures();

            // ---- Body features that involve links
            ComputeLinkBodyFeatures();  // This also sets MailURL

            // -- URL features 
            /* Disabled for testing */
            if (MailURL != null)
            {
                MailURL.ComputeURLFeatures();
                // ---- URL Domain features
                //MailURL.ComputeDomainFeatures();
            }

            // -- Attachment features
            ComputeAttachmentsFeatures();

            //Debug.WriteLine("Features computed for mail with ID: " + _mailID);
            return;
        }

        private void ComputeBodyFeatures()
        {
            Regex rx;
            //Feature n_html_comments_tag
            n_html_comments_tag = Regex.Matches(_HTMLBody, @"<!--\b").Count;
            //Feature n_words_body
            rx = new Regex(@"(\w+)");
            n_words_body = Regex.Matches(_plainTextBody, @"(\w+)").Count;  // dovremmo considerare il body senza HTML, ovvero _plainTextBody (che è diverso da _mailBody)
            //Feature n_images
            rx = new Regex(@"<img", RegexOptions.IgnoreCase);
            n_images = rx.Matches(_HTMLBody).Count;
            //Feature proportion_words_no_vowels
            rx = new Regex(@"\b([^aeiou\s]+)\b", RegexOptions.IgnoreCase);
            proportion_words_no_vowels = rx.Matches(_mailBody).Count / n_words_body;
            //Feature n_href_tag
            rx = new Regex(@"href\s*=", RegexOptions.IgnoreCase);
            n_href_tag = rx.Matches(_HTMLBody).Count;
            //Feature n_account_in_body
            rx = new Regex(@"account", RegexOptions.IgnoreCase);
            n_account_in_body = rx.Matches(_mailBody).Count;
            //Feature n_table_tag
            rx = new Regex(@"<table", RegexOptions.IgnoreCase);
            n_table_tag = rx.Matches(_HTMLBody).Count;
            //Feature automated_readability_index
            automated_readability_index = BodyFeatures.GetReadabilityIndex(_plainTextBody, "it");
            //Feature with link in page
            rx = new Regex("<a\\s+(?:[^>]*?\\s+)?href=([\"\'])(.*?)([\"\'])[A-z0 - 9,.\\/= \'\" \\-_]*>\\s*(.*)\\s*<\\/a>", RegexOptions.IgnoreCase);
            foreach (Match m in rx.Matches(_HTMLBody))
            {
                //Feature n_link_mismatch
                if(Regex.Match(m.Value, "href=([\"\'])(.*?)([\"\'])", RegexOptions.IgnoreCase) == Regex.Match(m.Value, ">\\s*(.*)\\s*<", RegexOptions.IgnoreCase))
                {
                    n_link_mismatch++;
                }
                //Feature n_links_IP
                if (Regex.Match(m.Value, "href=([\"\'])(.*?)((25[0-5]|(2[0-4]|1\\d|[1-9]|)\\d)\\.?\\b)(.*?)([\"\'])").Success)
                {
                    n_links_IP++;
                }
                //Feature n_links_ASCII
                if (Regex.Match(m.Value, "[^[:ascii:]]").Success)
                {
                    n_links_ASCII++;
                }
            }
            //Feature n_links
            n_links = rx.Matches(_HTMLBody).Count;

            //Feature cap_ratio
            cap_ratio = Regex.Matches(_plainTextBody, "[A-Z]").Count / Regex.Matches(_plainTextBody, "[a-z]").Count;

            //Feature language
            // TODO
            language = "IT";  //test value
            // Mapping : Italian => IT, German => DE, English => EN, Spanish => ES, French => FR, Portuguese => PT

            // n_misspelled_words
            n_misspelled_words = 0;
            string wordsInBody = Regex.Replace(_plainTextBody, @"[^\w ]", " ");  //remove all non-words 
            string dictPath = Environment.GetEnvironmentVariable("DICTIONARIES_PATH");
            if (Languages.Contains(language))  // Loads additional dictionary
            {
                try
                {
                    dictPath = Path.Combine(dictPath, language);
                } catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            } else  // Default language is English
            {
                dictPath = Path.Combine(dictPath, "EN");
            }
            using (Hunspell spellChecker = new Hunspell(dictPath + ".aff", dictPath + ".dic"))
            {
                foreach (string word in wordsInBody.Split(' '))
                {
                    if (!string.IsNullOrEmpty(word) && !spellChecker.Spell(word))
                    {
                        n_misspelled_words++;
                    }
                }
            }
            
        }

        /**
         * Computes the features of the body that refer to links in the email: 
         * binary_URL_bag_of_words, vt_l_maximum, vt_l_positives, vt_l_clean, vt_l_unknown
         * It also sets the most dangerous URL of the email (URLMail) based on these features 
         */
        private void ComputeLinkBodyFeatures()
        {

            List<string> links = new List<string>();
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
                MatchCollection _anchors = Regex.Matches(_HTMLBody, @"<a [^>]*href\s*=\s*(\'[^\']*\'|""[^""]*"").*>[^<]*<\s*\/a\s*>", RegexOptions.IgnoreCase);
                foreach (Match anchorLink in _anchors)
                {
                    string link = anchorLink.Groups[1].Value;  // the quoted URL is found in the second? matching group
                    link = link.Trim(new[] { '\'', '"' });  // removes the "" or '' around the link
                    //string link = Regex.Match(anchorLink.Value, "href\\s*=\\s*(\\'[^\\']*\\')|(\\\"[^\"]*\\\")", RegexOptions.IgnoreCase).Groups[0].Value;
                    links.Add(link);
                }
            }

            List<URLData> urls_in_mail = new List<URLData>();  // We store here the scans for each URL in the email
            foreach (string link in links)
            {
                if (!Regex.IsMatch(link, @"^(?:phone|mailto|tel|sms|callto):") && !string.IsNullOrEmpty(link))
                {
                    URLData url = new URLData(link);
                    VirusTotalScan alreadyAnalyzed = (VirusTotalScan)VirusTotalScans.Find(url.GetHostName());   // Checks if the link's hostname has already been analyzed
                    if (alreadyAnalyzed == null)
                    {
                        VirusTotalScan link_scan = new VirusTotalScan(url.GetHostName());
                        /*
                         * Disabled for testing 
                         * VirusTotal_API.PerformAPICall(link_scan);
                        */
                        VirusTotalScans.Add(link_scan);
                        url.SetVTScan(link_scan);
                    }
                    else
                    {
                        url.SetVTScan(alreadyAnalyzed);
                    }
                    urls_in_mail.Add(url);
                    if (!binary_URL_bag_of_words)  // This feature is true if at least one link contains one of the keywords
                    {
                        binary_URL_bag_of_words = Regex.IsMatch(link, @"click|here|login|update");
                    }
                }
            }
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
            }
        }

        private void ComputeSubjectFeatures() 
        {
            n_words_subject = Regex.Split(_mailSubject, @"\b\s").Length; // @"[\s[:punct:]]+").Length;
            n_char_subject = _mailSubject.Length;
            is_non_ASCII_subject = Regex.IsMatch(_mailSubject, @"[^\x00-\x7F]");
            if (Regex.IsMatch(_mailSubject, @"fwd:", RegexOptions.IgnoreCase))
            { 
                is_re_fwd_subject = Regex.IsMatch(_mailSubject, @"re:", RegexOptions.IgnoreCase) ? (sbyte) 3 : (sbyte) 2; // 3 = re+fwd, 2 = fwd
            } else
            {
                is_re_fwd_subject = Regex.IsMatch(_mailSubject, @"re:", RegexOptions.IgnoreCase) ? (sbyte) 1 : (sbyte) 0; // 1 = re, 0 = none
            }
        }

        private void ComputeHeaderFeatures()
        {
            n_hops = 0;
            Regex header_rx = new Regex(@"^(X-)?Received:", RegexOptions.IgnoreCase);  //"Received" or "X-Received" headers

            List<string> servers_in_received_headers= new List<string>(); // will contain the servers in the Received headers     
            int x_originating_ip_idx = -1; //, x_originating_email_idx=-1;
            for (int i = 0; i < _mailHeaders.Length; i++)
            {
                if (header_rx.Match(_mailHeaders[i]).Success)
                {
                    n_hops++;
                    Match match_ip = _ip_address_regex.Match(_mailHeaders[i]);
                    if (match_ip.Success)
                    {  //  try to match an IP address  
                        servers_in_received_headers.Add(match_ip.Value);
                    }
                    else
                    {  //  try to match a domain URL
                        Match match_url = _url_address_regex.Match(_mailHeaders[i]);
                        if (match_url.Success) { servers_in_received_headers.Add(match_url.Value); }
                    }
                } else if (_mailHeaders[i].StartsWith("X-Originating-IP")) {
                    x_originating_ip_idx = i;
                }
            }
            //  Blacklists check of the traversed mailservers  -n_smtp_servers_blacklist-
            foreach (string mail_server in servers_in_received_headers)
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
            }

            // email_origin_location - Email Origin Location
            string origin_server = "";
            if (x_originating_ip_idx >= 0)  // If "X-Originating-IP" has a value, use it!
            {
                string header_to_consider = _mailHeaders[x_originating_ip_idx];
                Regex origin_rx = new Regex(@"\[(.*)\]");  // Gets the orginating IP address
                Match origin_match = origin_rx.Match(header_to_consider);
                if (origin_match.Success) {  origin_server = origin_match.Groups[1].Value;  }          
            }
            else if (servers_in_received_headers.Count > 0)  // Else, try to use the last "Received" header
            {
                origin_server = servers_in_received_headers.Last();
            }
            OriginIP alreadyAnalyzedIP = (OriginIP)EmailOriginIPs.Find(origin_server);    // Checks if the IP has already been analyzed
            if ( alreadyAnalyzedIP == null) {
                OriginIP originResult = new OriginIP(origin_server);
                OriginIP_API.PerformAPICall(originResult);
                EmailOriginIPs.Add(originResult);  // Adds the IP and its result to the list of already analyzed IPs
                email_origin_location = originResult.GetFeature();
            } else {
                email_origin_location = alreadyAnalyzedIP.GetFeature();  // If the IP has already been analyzed, take the available result
            }
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
                    VirusTotalScan link_scan = new VirusTotalScan(att.SHA256, isAttachment: true);
                    VirusTotal_API.PerformAPICall(link_scan);
                    switch (att.GetAttachmentType())
                    {
                        case "image": n_image_attachments++; break;
                        case "application": n_application_attachments++; break;
                        case "message": n_message_attachments++; break;
                        case "text": n_text_attachments++; break;
                        case "video": n_video_attachments++; break;
                    }
                    att_sizes[i] = att.Size;
                    if (link_scan.NMalicious > 0)
                    {
                        vt_a_positives++;
                        vt_a_maximum = link_scan.NMalicious > vt_a_maximum ? link_scan.NMalicious : vt_a_maximum;
                    }
                    else
                    {
                        if (link_scan.NHarmless > 0) { vt_a_clean++; }  // if there's at least 1 "harmless" vote, we can consider it clean
                        else { vt_a_unknown++; }  // otherwise, the attachment is unknown to VirusTotal
                    }
                    i++;
                }
                attachments_size = att_sizes.Average();
                vt_a_rate = vt_a_positives / n_attachments;
            }
        }
    }
}
