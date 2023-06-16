using Microsoft.Office.Interop.Outlook;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DnsClient;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using System.Windows.Forms;
using System;
using System.Diagnostics;

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
        public int n_smtp_servers_blacklist;
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

            // DELETEME List<int> received_idxs = new List<int>(); // will contain the indeces of the matching headers
            List<string> servers_in_received_headers= new List<string>(); // will contain the servers in the Received headers
           
            int x_originating_ip_idx = -1; //, x_originating_email_idx=-1;
            for (int i = 0; i < _mailHeaders.Length; i++)
            {
                if (header_rx.Match(_mailHeaders[i]).Success)
                {
                    n_hops++;
                    //DELETEME received_idxs.Add(i);
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
                }/* DELETEME else if (_mailHeaders[i].StartsWith("X-Originating-Email"))
                {
                    x_originating_email_idx = i;
                }*/
            }

            // Blacklist check of the traversed mailservers 
            n_smtp_servers_blacklist = 0;
            foreach (string mail_server in servers_in_received_headers)
            {
                // TODO API call to check the mail_server against the blacklists
                // if (mail_server is in blacklist) {  n_smtp_servers_blacklist++;  }
            }

            // Email Origin Location
            string origin_server = "";
            if (x_originating_ip_idx >= 0)  // If "X-Originating-IP" has a value, use it!
            {
                string header_to_consider = _mailHeaders[x_originating_ip_idx];
                Regex origin_rx = new Regex(@"\[(.*)\]");  // Gets the orginating IP address
                Match origin_match = origin_rx.Match(header_to_consider);
                if (origin_match.Success) {  origin_server = origin_match.Groups[1].Value;  }          
            }
            /* DELETEME else if (x_originating_email_idx >= 0)  //Otherwise, try to use the Originating email
            {
                string header_to_consider = _mailHeaders[x_originating_email_idx];
                Regex origin_rx = new Regex(@"(?<=@)[^\]]*");  // Gets the orginating IP address

            }*/
            else if (servers_in_received_headers.Count > 0)  // Else, try to use the last "Received" header
            {
                //DELETEME string header_to_consider = _mailHeaders[received_idxs.Last()];  // The origin of the email is found in the last Received header
                origin_server = servers_in_received_headers.Last();
            }

            // Email Origin
            OriginIP alreadyAnalyzedIP = EmailOriginIPs.Find(origin_server);    // Checks if the IP has already been analyzed
            if ( alreadyAnalyzedIP == null) { 
                IPLocalization originResult = new IPLocalization(origin_server);
                originResult.PerformAPICall();
                email_origin_location = originResult.GetFeature();
                EmailOriginIPs.Add(new OriginIP(origin_server, email_origin_location));  // Adds the IP and its result to the list of already analyzed IPs
            } else {
                email_origin_location = alreadyAnalyzedIP.origin;  // If the IP has already been analyzed, take the available result
            }
        }

    }
}
