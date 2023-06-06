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
        public bool plain_Text;
        public int number_of_html_comments_tag;

        // Header features
        public int num_recipients;

        public MailData (string id, int size, string subject, string body, string htmlBody,
            string sender, int n_recipients)
        {
            mailID = id;
            mailSize = size;
            mailSubject = subject;
            mailBody = body;
            HTMLBody = htmlBody;
            senderEmail = sender;
            num_recipients = n_recipients;

            Valorize_plain_Text();
            Valorize_Number_of_html_comments_tag();
        }


        private void Valorize_plain_Text()
        {
            plain_Text = mailBody == HTMLBody;
        }

        private void Valorize_Number_of_html_comments_tag()
        {
            Regex rx = new Regex(@"<!--\b");

            number_of_html_comments_tag = rx.Matches(HTMLBody).Count;
        }


       
    }
}
