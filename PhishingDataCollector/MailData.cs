using Microsoft.Office.Interop.Outlook;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PhishingDataCollector
{

    class MailData
    {
        private readonly int mailSize;
        private readonly string mailID, mailSubject, mailBody, HTMLBody, senderEmail;
        private readonly string [] mailHeaders;
        private readonly Attachments mailAttachments;
        
        // Utility regexes
        private Regex ip_address_regex = new Regex (@"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
        private Regex url_address_regex = new Regex (@"(http(s)?:\/\/.)?[-a-zA-Z0-9@:%._\+~#=]{2,256}\.[a-z]{2,6}\b([-a-zA-Z0-9@:%_\+.~#?&//=]*)");

        // Features

        // Header features
        public int n_recipients;
        public bool plain_text;
        public int n_hops;
        public int n_smtp_servers_blacklist;
        public string email_origin_location;

        // Body
        public int n_html_comments_tag;

        public MailData(string id, int size, string subject, string body, string htmlBody,
            string sender, int num_recipients, string [] headers, Attachments attachments)
        {
            // Set private fields (if needed)
            mailID = id;
            mailSize = size;
            mailHeaders = headers;
            mailSubject = subject;
            mailBody = body;
            HTMLBody = htmlBody;
            senderEmail = sender;
            mailAttachments = attachments;

            // Compute email features
            // -- Header features
            n_recipients = num_recipients;
            plain_text = mailBody == HTMLBody;
            ComputeReceivedHeaderFeatures();

            // -- Domain features

            // -- Subject features

            // -- Body features
            Valorize_n_html_comments_tag();

            // -- URL features

            // -- Attachment features

        }


        private void Valorize_n_html_comments_tag()
        {
            Regex rx = new Regex(@"<!--\b");

            n_html_comments_tag = rx.Matches(HTMLBody).Count;
        }

        private void ComputeReceivedHeaderFeatures()
        {
            n_hops = 0;
            Regex header_rx = new Regex(@"^(X-)?Received:");  //"Received" or "X-Received" headers

            // DELETEME List<int> received_idxs = new List<int>(); // will contain the indeces of the matching headers
            List<string> servers_in_received_headers= new List<string>(); // will contain the servers in the Received headers
           
            int x_originating_ip_idx = -1; //, x_originating_email_idx=-1;
            for (int i = 0; i < mailHeaders.Length; i++)
            {
                if (header_rx.Match(mailHeaders[i]).Success)
                {
                    n_hops++;
                    //DELETEME received_idxs.Add(i);
                    Match match_ip = ip_address_regex.Match(mailHeaders[i]);
                    if (match_ip.Success)
                    {  //  try to match an IP address  
                        servers_in_received_headers.Add(match_ip.Value);
                    }
                    else
                    {  // try to match a domain URL
                        Match match_url = url_address_regex.Match(mailHeaders[i]);
                        if (match_url.Success) { servers_in_received_headers.Add(match_url.Value); }
                    }
                } else if (mailHeaders[i].StartsWith("X-Originating-IP")) {
                    x_originating_ip_idx = i;
                }/* DELETEME else if (mailHeaders[i].StartsWith("X-Originating-Email"))
                {
                    x_originating_email_idx = i;
                }*/
            }
            n_smtp_servers_blacklist = 0;
            // Blacklist check of the traversed mailservers 
            foreach (string mail_server in servers_in_received_headers)
            {
                // TODO API call to check the mail_server against the blacklists
                // if (mail_server is in blacklist) {  n_smtp_servers_blacklist++;  }
            }

            // Email Origin Location
            string origin_server = "";
            if (x_originating_ip_idx >= 0)  // If "X-Originating-IP" has a value, use it!
            {
                string header_to_consider = mailHeaders[x_originating_ip_idx];
                Regex origin_rx = new Regex(@"\[(.*)\]");  // Gets the orginating IP address
                Match origin_match = origin_rx.Match(header_to_consider);
                if (origin_match.Success) {  origin_server = origin_match.Groups[1].Value;  }          
            }
            /* DELETEME else if (x_originating_email_idx >= 0)  //Otherwise, try to use the Originating email
            {
                string header_to_consider = mailHeaders[x_originating_email_idx];
                Regex origin_rx = new Regex(@"(?<=@)[^\]]*");  // Gets the orginating IP address

            }*/
            else if (servers_in_received_headers.Count > 0)  // Else, try to use the last "Received" header
            {
                //DELETEME string header_to_consider = mailHeaders[received_idxs.Last()];  // The origin of the email is found in the last Received header
                origin_server = servers_in_received_headers.Last();
            } 

            if (origin_server != "")
            {
                //TODO perform API call to discover the origin (https://www.bigdatacloud.com/docs/ip-geolocation)
                email_origin_location = "unknown";  // API_call(origin_server)
            } else {  email_origin_location = "unkwnown";  }
        }

    }
}
