using Microsoft.Office.Interop.Outlook;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace PhishingDataCollector
{

    internal class MailData
    {
        public string ID => _mailID;

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

        // Body
        public int n_html_comments_tag;


        public URLData[] urls_in_mail;  // contains the features for each url in the email
        // public AttachmentsData[] attachments_in_mail;  // the same shall be done for attachments

        private HttpClient _httpClient;
        private readonly int _mailSize;
        private readonly string _mailID, _mailSubject, _mailBody, _HTMLBody, _emailSender;
        private readonly string [] _mailHeaders;
        private readonly Attachments _mailAttachments;
        
        // Utility regexes
        private Regex ip_address_regex = new Regex (@"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
        private Regex url_address_regex = new Regex (@"(http(s)?:\/\/.)?[-a-zA-Z0-9@:%._\+~#=]{2,256}\.[a-z]{2,6}\b([-a-zA-Z0-9@:%_\+.~#?&//=]*)");

        private OriginIPCollection EmailOriginIPs = new OriginIPCollection ();
        private BlacklistURLsCollection BlacklistedURLs = new BlacklistURLsCollection();

        public MailData(string id, int size, string subject, string body, string htmlBody,
            string sender, int num_recipients, string [] headers, Attachments attachments)
        {
            // Set private fields
            _httpClient = new HttpClient();
            _mailID = id;
            _mailSize = size;
            _mailHeaders = headers;
            _mailSubject = subject;
            _mailBody = body;
            _HTMLBody = htmlBody;
            _emailSender = sender;
            _mailAttachments = attachments;

            // Compute email features
            // -- Header features
            n_recipients = num_recipients;
            plain_text = _mailBody == _HTMLBody;
            ComputeHeaderFeatures();

            // -- Domain features


            // -- Subject features
            ComputeSubjectFeatures();

            // -- Body features
            Valorize_n_html_comments_tag();

            // -- URL features

            // -- Attachment features

        }

        private void Valorize_n_html_comments_tag()
        {
            Regex rx = new Regex(@"<!--\b");

            n_html_comments_tag = rx.Matches(_HTMLBody).Count;
        }

        private void ComputeSubjectFeatures() 
        {
            n_words_subject = Regex.Split(_mailSubject, @"\b\s").Length; // @"[\s[:punct:]]+").Length;
            n_char_subject = _mailSubject.Length;
            is_non_ASCII_subject = Regex.IsMatch(_mailSubject, @"[^\x00-\x7F]");
            if (Regex.IsMatch(_mailSubject, @"fwd:", RegexOptions.IgnoreCase))
            { 
                is_re_fwd_subject = Regex.IsMatch(_mailSubject, @"re:") ? (sbyte) 3 : (sbyte) 2; // 3 = re+fwd, 2 = fwd
            } else
            {
                is_re_fwd_subject = Regex.IsMatch(_mailSubject, @"re:") ? (sbyte) 1 : (sbyte) 0; // 1 = re, 0 = none
            }
        }
       

        private async void ComputeHeaderFeatures()
        {
            n_hops = 0;
            Regex header_rx = new Regex(@"^(X-)?Received:");  //"Received" or "X-Received" headers

            List<string> servers_in_received_headers= new List<string>(); // will contain the servers in the Received headers     
            int x_originating_ip_idx = -1; //, x_originating_email_idx=-1;
            for (int i = 0; i < _mailHeaders.Length; i++)
            {
                if (header_rx.Match(_mailHeaders[i]).Success)
                {
                    n_hops++;
                    Match match_ip = ip_address_regex.Match(_mailHeaders[i]);
                    if (match_ip.Success)
                    {  //  try to match an IP address  
                        servers_in_received_headers.Add(match_ip.Value);
                    }
                    else
                    {  //  try to match a domain URL
                        Match match_url = url_address_regex.Match(_mailHeaders[i]);
                        if (match_url.Success) { servers_in_received_headers.Add(match_url.Value); }
                    }
                } else if (_mailHeaders[i].StartsWith("X-Originating-IP")) {
                    x_originating_ip_idx = i;
                }
            }

            // n_smtp_servers_blacklist - Blacklists check of the traversed mailservers 
            foreach (string mail_server in servers_in_received_headers)
            {
                // API call to check the mail_server against more than 100 blacklists
                BlacklistURL alreadyAnalyzedURL = (BlacklistURL)BlacklistedURLs.Find(mail_server);    // Checks if the IP has already been analyzed
                if (alreadyAnalyzedURL == null)
                {
                    BlacklistURL_API blacklistsResult = new BlacklistURL_API(mail_server);
                    blacklistsResult.PerformAPICall();
                    short n_blacklists_mailserver = blacklistsResult.GetFeature();  // the number of blacklists in which the mail server was found in
                    BlacklistedURLs.Add(new BlacklistURL(mail_server, n_blacklists_mailserver));  // Adds the server and its result to the list of already analyzed servers
                    if (n_blacklists_mailserver > 0) { n_smtp_servers_blacklist++; }  // If the server appears in at least 1 blacklist, we increase the feature by 1
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
                OriginIP_API originResult = new OriginIP_API(origin_server);
                originResult.PerformAPICall();
                email_origin_location = originResult.GetFeature();
                EmailOriginIPs.Add(new OriginIP(origin_server, email_origin_location));  // Adds the IP and its result to the list of already analyzed IPs
            } else {
                email_origin_location = alreadyAnalyzedIP.Origin;  // If the IP has already been analyzed, take the available result
            }
        }
    }
}
