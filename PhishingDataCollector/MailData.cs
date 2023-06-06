using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Outlook = Microsoft.Office.Interop.Outlook;
using Office = Microsoft.Office.Core;

using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Specialized;

namespace PhishingDataCollector
{

    class MailData
    {
        private int mailSize;
        private string mailID, mailSubject, mailBody, HTMLBody, senderEmail;

        // Features

        // Header features
        public int n_recipients;
        public bool plain_text;

        // Body
        public int n_html_comments_tag;

        public MailData (string id, int size, string subject, string body, string htmlBody,
            string sender, int num_recipients)
        {
            // Set private fields (if needed)
            mailID = id;
            mailSize = size;
            mailSubject = subject;
            mailBody = body;
            HTMLBody = htmlBody;
            senderEmail = sender;

            // Compute email features
            // -- Header features
            n_recipients = num_recipients;
            plain_text = mailBody == HTMLBody;
            Valorize_Number_of_html_comments_tag();

            // -- Domain features

            // -- Subject features

            // -- Body features

            // -- URL features

            // -- Attachment features

        }


        private void Valorize_Number_of_html_comments_tag()
        {
            Regex rx = new Regex(@"<!--\b");

            n_html_comments_tag = rx.Matches(HTMLBody).Count;
        }


       
    }
}
